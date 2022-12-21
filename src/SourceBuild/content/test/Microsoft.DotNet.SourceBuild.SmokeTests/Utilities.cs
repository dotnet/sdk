// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public static class Utilities
{
    public static async Task RetryAsync(Func<Task> executor, ITestOutputHelper outputHelper)
    {
        await Utilities.RetryAsync(
            async () =>
            {
                try
                {
                    await executor();
                    return null;
                }
                catch (Exception e)
                {
                    return e;
                }
            },
            outputHelper);
    }

    private static async Task RetryAsync(Func<Task<Exception?>> executor, ITestOutputHelper outputHelper)
    {
        const int maxRetries = 5;
        const int waitFactor = 5;

        int retryCount = 0;

        Exception? exception = await executor();
        while (exception != null)
        {
            retryCount++;
            if (retryCount >= maxRetries)
            {
                throw new InvalidOperationException($"Failed after {retryCount} retries.", exception);
            }

            int waitTime = Convert.ToInt32(Math.Pow(waitFactor, retryCount - 1));
            if (outputHelper != null)
            {
                outputHelper.WriteLine($"Retry {retryCount}/{maxRetries}, retrying in {waitTime} seconds...");
            }

            Thread.Sleep(TimeSpan.FromSeconds(waitTime));
            exception = await executor();
        }
    }
}
