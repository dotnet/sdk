// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Aspire.Tools.Service.UnitTests;

public class AspireServerServiceTests(ITestOutputHelper output)
{
    private const string Project1Path = @"c:\test\Projects\project1.csproj";
    private const int ProcessId = 34213;
    private const string DcpId = "myid";
    private const string VersionedSessionUrl = $"{RunSessionRequest.Url}?{RunSessionRequest.VersionQuery}={RunSessionRequest.OurProtocolVersion}";

    private static readonly TestRunSessionRequest Project1SessionRequest = new TestRunSessionRequest(Project1Path, debugging: false, launchProfile: null, disableLaunchProfile: false)
    {
        args = new List<string> { "--project1Arg" },
        env = new List<EnvVar> { new EnvVar { Name = "var1", Value = "value1" } }
    };

    [Fact]
    public async Task SessionStarted_Test()
    {
        var mocks = new Mocks();

        var server = await GetAspireServer(mocks);

        // Start listening
        TaskCompletionSource<bool> connected = new();

        TaskCompletionSource<ProcessRestartedNotification> notificationTask = new();
        _ = ListenForSessionUpdatesAsync(server, connected, (sn) =>
        {
            notificationTask.SetResult((ProcessRestartedNotification)sn);
        });

        await connected.Task;

        await server.NotifySessionStartedAsync(DcpId,"1", ProcessId, CancellationToken.None);

        var result = await notificationTask.Task;

        Assert.Equal(ProcessId, result.PID);
        Assert.Equal("1", result.SessionId);

        await server.DisposeAsync();

        mocks.Verify();
    }

    [Fact]
    public async Task SessionEndedAsync_Test()
    {
        var mocks = new Mocks();

        var server = await GetAspireServer(mocks);

        // Start listening
        TaskCompletionSource<bool> connected = new();
        TaskCompletionSource<SessionTerminatedNotification> sessionEndNotificationTask = new();
        _ = ListenForSessionUpdatesAsync(server, connected, (sn) =>
        {
            if (sn.NotificationType == NotificationType.SessionTerminated)
            {
                sessionEndNotificationTask.SetResult((SessionTerminatedNotification)sn);
            }
        });

        await connected.Task;

        await server.NotifySessionEndedAsync(DcpId, "1", ProcessId, 130, CancellationToken.None);

        var result = await sessionEndNotificationTask.Task;
        Assert.Equal(ProcessId, result.Pid);
        Assert.Equal("1", result.SessionId);
        Assert.Equal(130, result.ExitCode);

        await server.DisposeAsync();

        mocks.Verify();
    }

    [Fact]
    public async Task LaunchProject_Success()
    {
        var mocks = new Mocks();

        mocks.GetOrCreate<IAspireServerEventsMock>()
             .ImplementStartProjectAsync(DcpId, "2");

        var server = await GetAspireServer(mocks);
        var tokens = server.GetServerVariables();

        using HttpClient client = GetHttpClient(tokens);

        HttpResponseMessage response;
        response = await client.PutAsJsonAsync(VersionedSessionUrl, Project1SessionRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{client.BaseAddress}run_session/2", response.Headers.Location.AbsoluteUri);

        await server.DisposeAsync();

        mocks.Verify();
    }

    [Fact]
    public async Task LaunchProject_Success_ThenStopProcessRequest()
    {
        var mocks = new Mocks();

        mocks.GetOrCreate<IAspireServerEventsMock>()
             .ImplementStartProjectAsync(DcpId, "2")
             .ImplementStopSessionAsync(DcpId, "2", exists: true)
             .ImplementStopSessionAsync(DcpId, "3", exists: false);

        var server = await GetAspireServer(mocks);
        var tokens = server.GetServerVariables();

        using HttpClient client = GetHttpClient(tokens);

        var response = await client.PutAsJsonAsync(VersionedSessionUrl, Project1SessionRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Now send a stop session
        response = await client.DeleteAsync(RunSessionRequest.Url + "/2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Validate NoContent response if session not found
        response = await client.DeleteAsync(RunSessionRequest.Url + "/3");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await server.DisposeAsync();

        mocks.Verify();
    }

    [Fact]
    public async Task LaunchProject_FailedToLaunchProject()
    {
        var mocks = new Mocks();

        mocks.GetOrCreate<IAspireServerEventsMock>()
             .ImplementStartProjectAsync(DcpId, "2", new Exception("Launch project failed"));

        var server = await GetAspireServer(mocks);

        var tokens = server.GetServerVariables();
        using HttpClient client = GetHttpClient(tokens);

        var response = await client.PutAsJsonAsync(VersionedSessionUrl, Project1SessionRequest);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType.ToString());
        Assert.Equal("{\"error\":{\"message\":\"Launch project failed\"}}", await response.Content.ReadAsStringAsync());

        await server.DisposeAsync();
        mocks.Verify();
    }

    [Fact]
    public async Task LaunchProject_FailNoBearerToken()
    {
        var mocks = new Mocks();

        var server = await GetAspireServer(mocks);

        var tokens = server.GetServerVariables();
        using HttpClient client = GetHttpClient(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "badToken");

        var response = await client.PutAsJsonAsync(VersionedSessionUrl, Project1SessionRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await server.DisposeAsync();
        mocks.Verify();
    }

    [Fact]
    public async Task LaunchProject_FailWrongUrl()
    {
        var mocks = new Mocks();

        var server = await GetAspireServer(mocks);

        var tokens = server.GetServerVariables();
        using HttpClient client = GetHttpClient(tokens);

        var response = await client.PutAsJsonAsync("/run_badurl", Project1SessionRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await server.DisposeAsync();

        mocks.Verify();
    }

    [Fact]
    public async Task LaunchProject_NotAPUTRequest()
    {
        var mocks = new Mocks();

        var aspireServer = await GetAspireServer(mocks);

        var tokens = aspireServer.GetServerVariables();
        using HttpClient client = GetHttpClient(tokens);

        var response = await client.PostAsJsonAsync(VersionedSessionUrl, Project1SessionRequest);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);

        await aspireServer.DisposeAsync();

        mocks.Verify();
    }

    [Fact]
    public async Task StopSession_FailNoBearerToken()
    {
        var mocks = new Mocks();

        var server = await GetAspireServer(mocks);

        var tokens = server.GetServerVariables();
        using HttpClient client = GetHttpClient(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "badToken");

        var response = await client.DeleteAsync(RunSessionRequest.Url + "/2");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await server.DisposeAsync();
        mocks.Verify();
    }

    [Fact]
    public async Task Info_Success()
    {
        var mocks = new Mocks();

        var server = await GetAspireServer(mocks);

        var tokens = server.GetServerVariables();
        using HttpClient client = GetHttpClient(tokens);

        var response = await client.GetAsync(InfoResponse.Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await server.DisposeAsync();
        mocks.Verify();
    }

    [Fact]
    public async Task Info_FailNoBearerToken()
    {
        var mocks = new Mocks();

        var server = await GetAspireServer(mocks);

        var tokens = server.GetServerVariables();
        using HttpClient client = GetHttpClient(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "badToken");

        var response = await client.GetAsync(InfoResponse.Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await server.DisposeAsync();
        mocks.Verify();
    }

    [Fact]
    public async Task SendLogMessageAsync_Test()
    {
        var mocks = new Mocks();

        var aspireServer = await GetAspireServer(mocks);


        // Start listening
        TaskCompletionSource<bool> connected = new();
        TaskCompletionSource<ServiceLogsNotification> notificationTask = new();
        _ = ListenForSessionUpdatesAsync(aspireServer, connected, (sn) =>
        {
            notificationTask.SetResult((ServiceLogsNotification)sn);
        });

        await connected.Task;

        await aspireServer.NotifyLogMessageAsync(DcpId, "1", isStdErr: false, "My Message", CancellationToken.None);

        var result = await notificationTask.Task;

        Assert.Equal("My Message", result.LogMessage);
        Assert.False(result.IsStdErr);
        await aspireServer.DisposeAsync();

        mocks.Verify();
    }

    [Fact]
    public async Task GetEnvironmentForOrchestrator_Tests()
    {
        var mocks = new Mocks();

        var server = await GetAspireServer(mocks, waitForListening: false);

        // First time should create a key
        var envVars = server.GetServerConnectionEnvironment();

        Assert.Equal(3, envVars.Count);
        var token = envVars[1];
        Assert.NotNull(token.Value);

        // Should return the same
        envVars = server.GetServerConnectionEnvironment();
        Assert.Equal(token, envVars[1]);

        mocks.Verify();
    }

    private async Task ListenForSessionUpdatesAsync(AspireServerService aspireServer, TaskCompletionSource<bool> connected, Action<SessionNotification> callback)
    {
        var tokens = aspireServer.GetServerVariables();
        using var httpClient = GetHttpClient(tokens);

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Authorization", $"Bearer {tokens.bearerToken}");
        try
        {
            await ws.ConnectAsync(new Uri($"wss://{tokens.serverAddress}{RunSessionRequest.Url}{SessionNotification.Url}"), httpClient, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.Fail("Could not connect to session update endpoint: " + ex.ToString());
            connected.SetResult(false);
            return;
        }

        connected.SetResult(true);

        while (ws.State == WebSocketState.Open)
        {
            string message;
            try
            {
                (message, var messageType) = await GetSocketMsgAsync(ws);

                if (messageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    return;
                }
            }
            catch
            {
                // This is expected if the connection is closed
                Assert.Equal(WebSocketState.Closed, ws.State);
                return;
            }

            var notification = JsonSerializer.Deserialize<SessionNotification>(message, AspireServerService.JsonSerializerOptions);
            Assert.NotNull(notification);

            SessionNotification value = notification.NotificationType switch
            {
                NotificationType.ProcessRestarted => JsonSerializer.Deserialize<ProcessRestartedNotification>(message, AspireServerService.JsonSerializerOptions),
                NotificationType.SessionTerminated => JsonSerializer.Deserialize<SessionTerminatedNotification>(message, AspireServerService.JsonSerializerOptions),
                NotificationType.ServiceLogs => JsonSerializer.Deserialize<ServiceLogsNotification>(message, AspireServerService.JsonSerializerOptions),
                _ => throw new InvalidOperationException($"Unexpected {notification.NotificationType}")
            };

            Assert.NotNull(value);
            callback.Invoke(value);
        }
    }

    private static HttpClient GetHttpClient((string serverAddress, string bearerToken, string certToken) tokens)
    {
        HttpClient client;
        var serverCert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(tokens.certToken));
        var clientHandler = new HttpClientHandler()
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            SslProtocols = System.Security.Authentication.SslProtocols.None,
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                return cert?.Thumbprint == serverCert.Thumbprint;
            }
        };

        client = new HttpClient(clientHandler);
        client.BaseAddress = new Uri($"https://{tokens.serverAddress}");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.bearerToken);
        client.DefaultRequestHeaders.Add(HttpContextExtensions.DCPInstanceIDHeader, DcpId);

        return client;
    }

    private async Task<(string, WebSocketMessageType)> GetSocketMsgAsync(ClientWebSocket client)
    {
        var rcvBuffer = new ArraySegment<byte>(new byte[2048]);
        WebSocketReceiveResult rcvResult = await client.ReceiveAsync(rcvBuffer, CancellationToken.None);
        if (rcvResult.MessageType == WebSocketMessageType.Text)
        {
            byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
            return (Encoding.UTF8.GetString(msgBytes), rcvResult.MessageType);
        }

        return (null, rcvResult.MessageType);
    }

    private async Task<AspireServerService> GetAspireServer(Mocks mocks, bool waitForListening = true)
    {
        var serverEvents = mocks.GetOrCreate<IAspireServerEventsMock>();

        var aspireServer = new AspireServerService(serverEvents.Object, displayName: "Test server",
            line =>
            {
                output.WriteLine(line);
                Debug.WriteLine(line);
            });

        if (waitForListening)
        {
            await aspireServer.WaitForListeningAsync();
        }

        return aspireServer;
    }

#pragma warning disable IDE1006 // Naming Styles
    internal class TestRunSessionRequestP4
    {
        public string project_path { get; set; } = string.Empty;
        public bool debug { get; set; }
        public List<EnvVar> env { get; set; } = new List<EnvVar>();
        public List<string> args { get; set; } = new List<string>();
        public string launch_profile { get; set; }
        public bool disable_launch_profile { get; set; }
    }

    internal class TestRunSessionRequest
    {
        public TestRunSessionRequest(string projectPath, bool debugging, string launchProfile, bool disableLaunchProfile)
        {
            launch_configurations = new TestLaunchConfiguration[]
            {
                new() {
                    project_path = projectPath,
                    type = RunSessionRequest.ProjectLaunchConfigurationType,
                    mode= debugging? RunSessionRequest.DebugLaunchMode : RunSessionRequest.NoDebugLaunchMode,
                    launch_profile = launchProfile,
                    disable_launch_profile = disableLaunchProfile
                }
            };
        }
        public TestLaunchConfiguration[] launch_configurations { get; set; }
        public List<EnvVar> env { get; set; } = new List<EnvVar>();
        public List<string> args { get; set; } = new List<string>();

        public TestRunSessionRequestP4 ToTestRunSessionRequestP4()
        {
            var launchConfig = launch_configurations[0];
            return new TestRunSessionRequestP4()
            {
                project_path = launchConfig.project_path,
                debug = string.Equals(launchConfig.mode, RunSessionRequest.DebugLaunchMode, StringComparison.OrdinalIgnoreCase),
                args = args,
                env = env,
                launch_profile = launchConfig.launch_profile,
                disable_launch_profile = launchConfig.disable_launch_profile
            };
        }
    }

    internal class TestLaunchConfiguration
    {
        public string type { get; set; } = string.Empty;
        public string project_path { get; set; } = string.Empty;
        public string launch_profile { get; set; }
        public bool disable_launch_profile { get; set; }
        public string mode { get; set; } = string.Empty;
    }

    internal class TestStopSessionRequest
    {
        public string session_id { get; set; } = string.Empty;
    }
#pragma warning restore IDE1006 // Naming Styles
}

internal static class AspireServerServiceExtensions
{
    public static async Task WaitForListeningAsync(this AspireServerService aspireServer)
    {
        string serverAddress = aspireServer.GetServerVariables().serverAddress;

        // We need to wait on the port being available
        await Helpers.CanConnectToPortAsync(new Uri($"http://{serverAddress}"), 5000, CancellationToken.None);

    }

    public static (string serverAddress, string bearerToken, string certToken) GetServerVariables(this AspireServerService aspireServer)
    {
        var enVars = aspireServer.GetServerConnectionEnvironment();
        return (enVars[0].Value, enVars[1].Value, enVars[2].Value);
    }
}
