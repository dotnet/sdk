// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC;

internal sealed class HttpTestHostGateway : IDisposable
{
    internal const int MaximumFrameSize = 256 * 1024 * 1024;
    private const string BinaryContentType = "application/octet-stream";

    private readonly Func<IRequest, Task<IResponse>> _callback;
    private readonly HttpListener _listener;
    private readonly ProtocolMessageSerializer _serializer = new();
    private readonly CancellationTokenRegistration _cancellationRegistration;
    private readonly Task _listenerTask;
    private readonly Lock _originLock = new();
    private string? _allowedOrigin;
    private bool _disposed;

    public HttpTestHostGateway(
        Func<IRequest, Task<IResponse>> callback,
        CancellationToken cancellationToken,
        string? allowedOrigin = null)
    {
        _callback = callback;
        _allowedOrigin = NormalizeOrigin(allowedOrigin);
        _serializer.RegisterAllSerializers();

        Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        (_listener, Endpoint) = StartListener();
        _cancellationRegistration = cancellationToken.Register(
            static state => ((HttpListener)state!).Close(),
            _listener);
        _listenerTask = ListenAsync(cancellationToken);
    }

    public Uri Endpoint { get; }

    public string Token { get; }

    private static (HttpListener Listener, Uri Endpoint) StartListener()
    {
        const int maximumAttempts = 10;
        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            int port = GetAvailableLoopbackPort();
            string path = $"dotnettest/{Guid.NewGuid():N}/";
            var endpoint = new Uri($"http://127.0.0.1:{port}/{path}");
            var listener = new HttpListener();
            listener.Prefixes.Add(endpoint.AbsoluteUri);

            try
            {
                listener.Start();
                return (listener, endpoint);
            }
            catch (HttpListenerException) when (attempt + 1 < maximumAttempts)
            {
                listener.Close();
            }
        }

        throw new InvalidOperationException("Unable to start the dotnet test HTTP gateway on loopback.");
    }

    private static int GetAvailableLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
                await HandleRequestAsync(context, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (HttpListenerException) when (cancellationToken.IsCancellationRequested || !_listener.IsListening)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested || !_listener.IsListening)
        {
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        try
        {
            if (!string.Equals(request.Url?.AbsolutePath, Endpoint.AbsolutePath, StringComparison.Ordinal))
            {
                await CompleteErrorResponseAsync(response, HttpStatusCode.NotFound, cancellationToken);
                return;
            }

            if (string.Equals(request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryApplyCorsHeaders(request, response, pinOrigin: true))
                {
                    await CompleteErrorResponseAsync(response, HttpStatusCode.Forbidden, cancellationToken);
                    return;
                }

                response.Headers["Access-Control-Allow-Methods"] = "POST";
                response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type";
                if (string.Equals(request.Headers["Access-Control-Request-Private-Network"], "true", StringComparison.OrdinalIgnoreCase))
                {
                    response.Headers["Access-Control-Allow-Private-Network"] = "true";
                }

                response.StatusCode = (int)HttpStatusCode.NoContent;
                response.ContentLength64 = 0;
                response.Close();
                return;
            }

            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                TryApplyCorsHeaders(request, response, pinOrigin: false);
                response.Headers["Allow"] = "POST, OPTIONS";
                await CompleteErrorResponseAsync(response, HttpStatusCode.MethodNotAllowed, cancellationToken);
                return;
            }

            if (!IsAuthorized(request))
            {
                TryApplyCorsHeaders(request, response, pinOrigin: false);
                response.Headers["WWW-Authenticate"] = "Bearer";
                await CompleteErrorResponseAsync(response, HttpStatusCode.Unauthorized, cancellationToken);
                return;
            }

            if (!TryApplyCorsHeaders(request, response, pinOrigin: true))
            {
                await CompleteErrorResponseAsync(response, HttpStatusCode.Forbidden, cancellationToken);
                return;
            }

            if (!MediaTypeHeaderValue.TryParse(request.ContentType, out MediaTypeHeaderValue? contentType) ||
                !string.Equals(contentType.MediaType, BinaryContentType, StringComparison.OrdinalIgnoreCase))
            {
                await CompleteErrorResponseAsync(response, HttpStatusCode.UnsupportedMediaType, cancellationToken);
                return;
            }

            byte[] frame;
            try
            {
                frame = await ReadFrameAsync(request, cancellationToken);
            }
            catch (InvalidDataException)
            {
                await CompleteErrorResponseAsync(response, HttpStatusCode.BadRequest, cancellationToken);
                return;
            }

            IRequest protocolRequest;
            try
            {
                protocolRequest = (IRequest)_serializer.Deserialize(frame, skipUnknownMessages: true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogTrace($"The dotnet test HTTP gateway rejected a malformed protocol frame of type '{ex.GetType().FullName}'.");
                await CompleteErrorResponseAsync(response, HttpStatusCode.BadRequest, cancellationToken);
                return;
            }

            IResponse protocolResponse = await _callback(protocolRequest);
            byte[] responseFrame = _serializer.Serialize(protocolResponse);

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = BinaryContentType;
            response.ContentLength64 = responseFrame.Length;
            await response.OutputStream.WriteAsync(responseFrame, cancellationToken);
            response.Close();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogTrace($"The dotnet test HTTP gateway failed to process a request: {ex}");
            try
            {
                if (response.OutputStream.CanWrite)
                {
                    await CompleteErrorResponseAsync(response, HttpStatusCode.InternalServerError, cancellationToken);
                }
            }
            catch (Exception responseException)
            {
                Logger.LogTrace($"The dotnet test HTTP gateway failed to send an error response: {responseException}");
            }
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        string? authorization = request.Headers["Authorization"];
        if (!AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            header.Parameter is null)
        {
            return false;
        }

        byte[] providedToken = Encoding.UTF8.GetBytes(header.Parameter);
        byte[] expectedToken = Encoding.UTF8.GetBytes(Token);
        return providedToken.Length == expectedToken.Length &&
            CryptographicOperations.FixedTimeEquals(providedToken, expectedToken);
    }

    private bool TryApplyCorsHeaders(
        HttpListenerRequest request,
        HttpListenerResponse response,
        bool pinOrigin)
    {
        string? requestOrigin = NormalizeOrigin(request.Headers["Origin"]);
        if (requestOrigin is null)
        {
            return request.Headers["Origin"] is null;
        }

        lock (_originLock)
        {
            if (pinOrigin)
            {
                _allowedOrigin ??= requestOrigin;
            }

            if (!string.Equals(_allowedOrigin, requestOrigin, StringComparison.Ordinal))
            {
                Logger.LogTrace($"The dotnet test HTTP gateway rejected origin '{requestOrigin}' because it does not match the origin established for this run.");
                return false;
            }
        }

        response.Headers["Access-Control-Allow-Origin"] = requestOrigin;
        response.Headers["Vary"] = "Origin, Access-Control-Request-Private-Network";
        return true;
    }

    private static string? NormalizeOrigin(string? origin)
    {
        if (origin is null ||
            !Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme is not ("http" or "https") ||
            uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 ||
            uri.Fragment.Length != 0 ||
            uri.AbsolutePath != "/")
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static async Task<byte[]> ReadFrameAsync(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength64 > MaximumFrameSize)
        {
            throw new InvalidDataException("The dotnet test HTTP request is too large.");
        }

        using var buffer = request.ContentLength64 is >= 0 and <= 1024 * 1024
            ? new MemoryStream((int)request.ContentLength64)
            : new MemoryStream();

        byte[] bytes = new byte[81920];
        int totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await request.InputStream.ReadAsync(bytes, cancellationToken)) != 0)
        {
            totalBytes = checked(totalBytes + bytesRead);
            if (totalBytes > MaximumFrameSize)
            {
                throw new InvalidDataException("The dotnet test HTTP request is too large.");
            }

            await buffer.WriteAsync(bytes.AsMemory(0, bytesRead), cancellationToken);
        }

        if (request.ContentLength64 >= 0 && totalBytes != request.ContentLength64)
        {
            throw new InvalidDataException("The dotnet test HTTP request ended before its declared content length.");
        }

        return buffer.ToArray();
    }

    private static async Task CompleteErrorResponseAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        CancellationToken cancellationToken)
    {
        response.StatusCode = (int)statusCode;
        response.ContentLength64 = 0;
        await response.OutputStream.FlushAsync(cancellationToken);
        response.Close();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cancellationRegistration.Dispose();
        _listener.Close();
        try
        {
            _listenerTask.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogTrace($"The dotnet test HTTP gateway listener failed during shutdown: {ex}");
        }

        _disposed = true;
    }
}
