// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using Moq;
using Microsoft.NET.TestFramework;

namespace Microsoft.NET.Sdk.Razor.Tool.Tests
{
    [TestClass]
    public class ServerLifecycleTest
    {
        public TestContext TestContext { get; set; }

        private static ServerRequest EmptyServerRequest => new(1, Array.Empty<RequestArgument>());

        private static ServerResponse EmptyServerResponse => new CompletedServerResponse(
            returnCode: 0,
            utf8output: false,
            output: string.Empty,
            error: string.Empty);

        [TestMethod]
        public void ServerStartup_MutexAlreadyAcquired_Fails()
        {
            // Arrange
            var pipeName = Guid.NewGuid().ToString("N");
            var mutexName = MutexName.GetServerMutexName(pipeName);
            var compilerHost = new Mock<CompilerHost>(MockBehavior.Strict);
            var host = new Mock<ConnectionHost>(MockBehavior.Strict);

            // Act & Assert
            using (var mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out var holdsMutex))
            {
                Assert.IsTrue(holdsMutex);
                try
                {
                    var result = ServerUtilities.RunServer(pipeName, host.Object, compilerHost.Object, cancellationToken: TestContext.CancellationToken);

                    // Assert failure
                    Assert.AreEqual(1, result);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        [TestMethod]
        public void ServerStartup_SuccessfullyAcquiredMutex()
        {
            // Arrange, Act & Assert
            var pipeName = Guid.NewGuid().ToString("N");
            var mutexName = MutexName.GetServerMutexName(pipeName);
            var compilerHost = new Mock<CompilerHost>(MockBehavior.Strict);
            var host = new Mock<ConnectionHost>(MockBehavior.Strict);
            host
                .Setup(x => x.WaitForConnectionAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    // Use a thread instead of Task to guarantee this code runs on a different
                    // thread and we can validate the mutex state. 
                    var source = new TaskCompletionSource<bool>();
                    var thread = new Thread(_ =>
                    {
                        Mutex mutex = null;
                        try
                        {
                            Assert.IsTrue(Mutex.TryOpenExisting(mutexName, out mutex));
                            Assert.IsFalse(mutex.WaitOne(millisecondsTimeout: 0));
                            source.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            source.SetException(ex);
                            throw;
                        }
                        finally
                        {
                            mutex?.Dispose();
                        }
                    });

                    // Synchronously wait here.  Don't returned a Task value because we need to 
                    // ensure the above check completes before the server hits a timeout and 
                    // releases the mutex. 
                    thread.Start();
                    source.Task.Wait(TestContext.CancellationToken);

                    return new TaskCompletionSource<Connection>().Task;
                });

            var result = ServerUtilities.RunServer(pipeName, host.Object, compilerHost.Object, cancellationToken: TestContext.CancellationToken, keepAlive: TimeSpan.FromSeconds(1));
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public async Task ServerRunning_ShutdownRequest_processesSuccessfully()
        {
            // Arrange
            using (var serverData = ServerUtilities.CreateServer())
            {
                // Act
                var serverProcessId = await ServerUtilities.SendShutdown(serverData.PipeName);

                // Assert
                Assert.AreEqual(Process.GetCurrentProcess().Id, serverProcessId);
                await serverData.Verify(connections: 1, completed: 1);
            }
        }

        /// <summary>
        /// A shutdown request should not abort an existing compilation.  It should be allowed to run to 
        /// completion.
        /// </summary>
        [TestMethod]
        public async Task ServerRunning_ShutdownRequest_DoesNotAbortCompilation()
        {
            // Arrange
            var startCompilationSource = new TaskCompletionSource<bool>();
            var finishCompilationSource = new TaskCompletionSource<bool>();
            var host = CreateCompilerHost(c => c.ExecuteFunc = (req, ct) =>
            {
                // At this point, the connection has been accepted and the compilation has started.
                startCompilationSource.SetResult(true);

                // We want this to keep running even after the shutdown is seen.
                finishCompilationSource.Task.Wait(TestContext.CancellationToken);
                return EmptyServerResponse;
            });

            using (var serverData = ServerUtilities.CreateServer(compilerHost: host))
            {
                var compileTask = ServerUtilities.Send(serverData.PipeName, EmptyServerRequest);

                // Wait for the request to go through and trigger compilation.
                await startCompilationSource.Task;

                // Act
                // The compilation is now in progress, send the shutdown.
                await ServerUtilities.SendShutdown(serverData.PipeName);
                Assert.IsFalse(compileTask.IsCompleted);

                // Now let the task complete.
                finishCompilationSource.SetResult(true);

                // Assert
                var response = await compileTask;
                Assert.AreEqual(ServerResponse.ResponseType.Completed, response.Type);
                Assert.AreEqual(0, ((CompletedServerResponse)response).ReturnCode);

                await serverData.Verify(connections: 2, completed: 2);
            }
        }

        /// <summary>
        /// Multiple clients should be able to send shutdown requests to the server.
        /// </summary>
        [TestMethod]
        public async Task ServerRunning_MultipleShutdownRequests_HandlesSuccessfully()
        {
            // Arrange
            var startCompilationSource = new TaskCompletionSource<bool>();
            var finishCompilationSource = new TaskCompletionSource<bool>();
            var host = CreateCompilerHost(c => c.ExecuteFunc = (req, ct) =>
            {
                // At this point, the connection has been accepted and the compilation has started.
                startCompilationSource.SetResult(true);

                // We want this to keep running even after the shutdown is seen.
                finishCompilationSource.Task.Wait(TestContext.CancellationToken);
                return EmptyServerResponse;
            });

            using (var serverData = ServerUtilities.CreateServer(compilerHost: host))
            {
                var compileTask = ServerUtilities.Send(serverData.PipeName, EmptyServerRequest);

                // Wait for the request to go through and trigger compilation.
                await startCompilationSource.Task;

                // Act
                for (var i = 0; i < 10; i++)
                {
                    // The compilation is now in progress, send the shutdown.
                    var processId = await ServerUtilities.SendShutdown(serverData.PipeName);
                    Assert.AreEqual(Process.GetCurrentProcess().Id, processId);
                    Assert.IsFalse(compileTask.IsCompleted);
                }

                // Now let the task complete.
                finishCompilationSource.SetResult(true);

                // Assert
                var response = await compileTask;
                Assert.AreEqual(ServerResponse.ResponseType.Completed, response.Type);
                Assert.AreEqual(0, ((CompletedServerResponse)response).ReturnCode);

                await serverData.Verify(connections: 11, completed: 11);
            }
        }

        // https://github.com/aspnet/Razor/issues/1991
        [TestMethod]
        [PlatformSpecific(TestPlatforms.Windows)]
        public async Task ServerRunning_CancelCompilation_CancelsSuccessfully()
        {
            // Arrange
            const int requestCount = 5;
            var count = 0;
            var completionSource = new TaskCompletionSource<bool>();
            var host = CreateCompilerHost(c => c.ExecuteFunc = (req, ct) =>
            {
                if (Interlocked.Increment(ref count) == requestCount)
                {
                    completionSource.SetResult(true);
                }

                ct.WaitHandle.WaitOne();
                return new RejectedServerResponse();
            });

            var semaphore = new SemaphoreSlim(1);
            Action<object, EventArgs> onListening = (s, e) =>
            {
                semaphore.Release();
            };
            using (var serverData = ServerUtilities.CreateServer(compilerHost: host, onListening: onListening))
            {
                // Send all the requests.
                var clients = new List<Client>();
                for (var i = 0; i < requestCount; i++)
                {
                    // Wait for the server to start listening.
                    await semaphore.WaitAsync(TimeSpan.FromMinutes(1), TestContext.CancellationToken);

                    var client = await Client.ConnectAsync(serverData.PipeName, timeout: null, cancellationToken: TestContext.CancellationToken);
                    await EmptyServerRequest.WriteAsync(client.Stream, TestContext.CancellationToken);
                    clients.Add(client);
                }

                // Act
                // Wait until all of the connections are being processed by the server. 
                await completionSource.Task;

                // Now cancel
                var stats = await serverData.CancelAndCompleteAsync();

                // Assert
                Assert.AreEqual(requestCount, stats.Connections);
                Assert.AreEqual(requestCount, count);

                // Read the server response to each client.
                foreach (var client in clients)
                {
                    var task = ServerResponse.ReadAsync(client.Stream, TestContext.CancellationToken);
                    // We expect this to throw because the stream is already closed.
                    await Assert.ThrowsAsync<IOException>(() => task);
                    client.Dispose();
                }
            }
        }

        private static TestableCompilerHost CreateCompilerHost(Action<TestableCompilerHost> configureCompilerHost = null)
        {
            var compilerHost = new TestableCompilerHost();
            configureCompilerHost?.Invoke(compilerHost);

            return compilerHost;
        }

        private class TestableCompilerHost : CompilerHost
        {
            internal Func<ServerRequest, CancellationToken, ServerResponse> ExecuteFunc;

            public override ServerResponse Execute(ServerRequest request, CancellationToken cancellationToken)
            {
                if (ExecuteFunc != null)
                {
                    return ExecuteFunc(request, cancellationToken);
                }

                return EmptyServerResponse;
            }
        }
    }
}
