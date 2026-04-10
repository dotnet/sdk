// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Microsoft.NET.Sdk.Publish.Tasks;

/// <summary>
/// Extension methods for <see cref="IHttpClient"/> and related types.
/// </summary>
internal static class HttpClientExtensions
{
    private static readonly string s_azureADUserName = Guid.Empty.ToString();
    private static readonly JsonSerializerOptions s_defaultSerializerOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private const string BearerAuthenticationScheme = "Bearer";
    private const string BasicAuthenticationScheme = "Basic";

    /// <summary>
    /// Sends an HTTP POST request.
    /// </summary>
    /// <param name="uri">uri to send the request to</param>
    /// <param name="username">user name</param>
    /// <param name="password">user password</param>
    /// <param name="contentType">content type header value</param>
    /// <param name="userAgent">'User-Agent' header value</param>
    /// <param name="encoding">encoding</param>
    /// <param name="messageBody">message payload</param>
    /// <returns>HTTP response</returns>
    public static async Task<IHttpResponse?> PostRequestAsync(
        this IHttpClient client,
        Uri uri,
        string? username,
        string? password,
        string contentType,
        string? userAgent,
        Encoding encoding,
        Stream messageBody)
    {
        if (client is null)
        {
            return null;
        }

        AddAuthenticationHeader(username, password, client);
        client.DefaultRequestHeaders.Add("User-Agent", userAgent);

        StreamContent content = new(messageBody ?? new MemoryStream())
        {
            Headers =
            {
                ContentType = new MediaTypeHeaderValue(contentType)
                {
                    CharSet = encoding.WebName
                },
                ContentEncoding =
                {
                    encoding.WebName
                }
            }
        };

        try
        {
            HttpResponseMessage responseMessage = await client.PostAsync(uri, content);
            return new HttpResponseMessageWrapper(responseMessage);
        }
        catch (TaskCanceledException)
        {
            return new HttpResponseMessageForStatusCode(HttpStatusCode.RequestTimeout);
        }
    }

    /// <summary>
    /// Sends an HTTP PUT request.
    /// </summary>
    /// <param name="uri">uri to send the request to</param>
    /// <param name="username">user name</param>
    /// <param name="password">user password</param>
    /// <param name="contentType">content type header value</param>
    /// <param name="userAgent">'User-Agent' header value</param>
    /// <param name="encoding">encoding</param>
    /// <param name="messageBody">message payload</param>
    /// <returns>HTTP response</returns>
    public static async Task<IHttpResponse?> PutRequestAsync(
        this IHttpClient client,
        Uri uri,
        string? username,
        string? password,
        string contentType,
        string? userAgent,
        string? fileName,
        Encoding encoding,
        Stream messageBody,
        CancellationToken cancellationToken)
    {
        if (client is null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        AddAuthenticationHeader(username, password, client);
        client.DefaultRequestHeaders.Add("User-Agent", userAgent);

        StreamContent content = new(messageBody ?? new MemoryStream())
        {
            Headers =
            {
                ContentType = new MediaTypeHeaderValue(contentType),
                ContentEncoding =
                {
                    encoding.WebName
                },
                ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = fileName
                }
            }
        };

        try
        {
            HttpResponseMessage responseMessage = await client.PutAsync(uri, content, cancellationToken);
            return new HttpResponseMessageWrapper(responseMessage);
        }
        catch (TaskCanceledException)
        {
            return new HttpResponseMessageForStatusCode(HttpStatusCode.RequestTimeout);
        }
    }

    /// <summary>
    /// Sends an HTTP GET request.
    /// </summary>
    /// <param name="uri">uri to send the request to</param>
    /// <param name="username">user name</param>
    /// <param name="password">user password</param>
    /// <param name="userAgent">'User-Agent' header value</param>
    /// <param name="cancellationToken"></param>
    /// <returns>HTTP response</returns>
    public static async Task<IHttpResponse?> GetRequestAsync(
        this IHttpClient client,
        Uri uri,
        string? username,
        string? password,
        string userAgent,
        CancellationToken cancellationToken)
    {
        if (client is null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        AddAuthenticationHeader(username, password, client);
        client.DefaultRequestHeaders.Add("User-Agent", userAgent);

        try
        {
            HttpResponseMessage responseMessage = await client.GetAsync(uri, cancellationToken);
            return new HttpResponseMessageWrapper(responseMessage);
        }
        catch (TaskCanceledException)
        {
            return new HttpResponseMessageForStatusCode(HttpStatusCode.RequestTimeout);
        }
    }

    /// <summary>
    /// Sends HTTP GET request a maximum <paramref name="retries"/> attempts. It will retry while
    /// request is not OK/Accepted or maximum number of retries has been reached.
    /// </summary>
    /// <typeparam name="T">expected type of response object</typeparam>
    /// <param name="url">URL to send requests to</param>
    /// <param name="username">user name</param>
    /// <param name="password">user password</param>
    /// <param name="userAgent">'User-Agent' header value</param>
    /// <param name="retries">maximum number of attempts</param>
    /// <param name="delay">time to wait between attempts; usually in seconds</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>response of given type; default value if response status code is not of success</returns>
    public static async Task<T?> RetryGetRequestAsync<T>(
        this IHttpClient client,
        string? url,
        string? username,
        string? password,
        string userAgent,
        int retries,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        if (client is null || url is null)
        {
            return default;
        }

        // retry GET request
        IHttpResponse? response = null;
        await RetryTaskAsync(async (ct) =>
        {
            response = await client.GetRequestAsync(new Uri(url, UriKind.RelativeOrAbsolute), username, password, userAgent, ct);
        }, retries, delay, cancellationToken);

        // response is not valid; return default value
        if (!(response?.IsResponseSuccessful() ?? false))
        {
            return default;
        }

        return await response.GetJsonResponseAsync<T>(cancellationToken);
    }

    /// <summary>
    /// Whether given <see cref="IHttpResponse"/> has a status of <see cref="HttpStatusCode.OK"/>
    /// or <see cref="HttpStatusCode.Accepted"></see>
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    public static bool IsResponseSuccessful(this IHttpResponse response)
    {
        return response is not null
            && (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted);

    }

    /// <summary>
    /// Reads the <see cref="IHttpResponse"/> body as a string.
    /// </summary>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>the response body as text</returns>
    public static async Task<string> GetTextResponseAsync(this IHttpResponse response, CancellationToken cancellationToken)
    {
        var responseText = string.Empty;

        if (response is null || cancellationToken.IsCancellationRequested)
        {
            return responseText;
        }

        var responseBody = await response.GetResponseBodyAsync();
        if (responseBody is not null)
        {
            var streamReader = new StreamReader(responseBody);
            responseText = streamReader.ReadToEnd();
        }

        return responseText;
    }

    /// <summary>
    /// Attempts to serialize the <see cref="IHttpResponse"/> JSON content into an object of the given type.
    /// </summary>
    /// <typeparam name="T">type to serialize to</typeparam>
    /// <param name="cancellation">cancellation token</param>
    /// <returns><typeparamref name="T"/> object</returns>
    public static async Task<T?> GetJsonResponseAsync<T>(this IHttpResponse response, CancellationToken cancellation)
    {
        if (response is null || cancellation.IsCancellationRequested)
        {
            return default;
        }

        using var stream = await response.GetResponseBodyAsync();
        if (stream is null)
        {
            return default;
        }
        var reader = new StreamReader(stream, Encoding.UTF8);

        return JsonSerializer.Deserialize<T>(reader.ReadToEnd(), s_defaultSerializerOptions);
    }

    private static void AddAuthenticationHeader(string? username, string? password, IHttpClient client)
    {
        client.DefaultRequestHeaders.Remove("Connection");

        if (!string.Equals(username, s_azureADUserName, StringComparison.Ordinal))
        {
            string plainAuth = string.Format("{0}:{1}", username, password);
            byte[] plainAuthBytes = Encoding.ASCII.GetBytes(plainAuth);
            string base64 = Convert.ToBase64String(plainAuthBytes);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(BasicAuthenticationScheme, base64);
        }
        else
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(BearerAuthenticationScheme, password);
        }
    }

    /// <summary>
    /// Attempts to run the given function at least 1 time and at most <paramref name="retryCount"/> times.
    /// </summary>
    /// <param name="func">function to run</param>
    /// <param name="retryCount">maximum number of attempts</param>
    /// <param name="retryDelay">delay between each attempt</param>
    private static async System.Threading.Tasks.Task RetryTaskAsync(
        Func<CancellationToken, System.Threading.Tasks.Task> func,
        int retryCount,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await func(cancellationToken);
                }

                return;
            }
            catch (Exception)
            {
                if (retryCount <= 0)
                {
                    throw;
                }

                retryCount--;
            }

            await System.Threading.Tasks.Task.Delay(retryDelay, cancellationToken);
        }
    }
}
