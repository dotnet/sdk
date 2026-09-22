// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests
{
    [TestClass]
    public class GivenExponentialRetry : SdkTest
    {
        public GivenExponentialRetry()
        {
        }

        [TestMethod]
        public async Task ItReturnsOnSuccess()
        {
            var retryCount = 0;
            Func<Task<string>> action = () =>
            {
                retryCount++;
                return Task.FromResult("done");
            };
            var res = await ExponentialRetry.ExecuteWithRetryOnFailure<string>(
                action,
                TestContext.CancellationToken);

            retryCount.Should().Be(1);
        }

        [TestMethod]
        public async Task ItRetriesOnError()
        {
            var retryCount = 0;
            Func<Task<string?>> action = () => // Updated to use nullable reference type  
            {
                retryCount++;
                return Task.FromResult<string?>(null); // Updated to match nullable reference type  
            };
            var res = await ExponentialRetry.ExecuteWithRetryOnFailure<string?>(
                action,
                TestContext.CancellationToken,
                2,
                timer: () => ExponentialRetry.Timer(ExponentialRetry.TestingIntervals, TestContext.CancellationToken));

            res.Should().BeNull();
            retryCount.Should().Be(2);
        }

        [TestMethod]
        public async Task ItCancelsWhileWaitingToRetry()
        {
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            TaskCompletionSource firstAttemptCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var retryCount = 0;

            Task<string?> retryTask = ExponentialRetry.ExecuteWithRetryOnFailure<string?>(
                () =>
                {
                    retryCount++;
                    firstAttemptCompleted.TrySetResult();
                    return Task.FromResult<string?>(null);
                },
                cancellationSource.Token,
                timer: () => ExponentialRetry.Timer(
                    [TimeSpan.Zero, TimeSpan.FromMinutes(1)],
                    cancellationSource.Token));

            await firstAttemptCompleted.Task.WaitAsync(TestContext.CancellationToken);
            await cancellationSource.CancelAsync();

            await Assert.ThrowsExactlyAsync<TaskCanceledException>(
                () => retryTask.WaitAsync(TestContext.CancellationToken));
            retryCount.Should().Be(1);
        }
    }
}
