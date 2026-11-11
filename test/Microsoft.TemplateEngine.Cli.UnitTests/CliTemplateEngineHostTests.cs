// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    [TestClass]
    [ResourceLock(WellKnownResources.Console)]
    public class CliTemplateEngineHostTests
    {
        [TestMethod]
        [DataRow(LogLevel.Trace)]
        [DataRow(LogLevel.Debug)]
        [DataRow(LogLevel.Information)]
        [DataRow(LogLevel.Warning)]
        [DataRow(LogLevel.Error)]
        [DataRow(LogLevel.Critical)]
        [DataRow(LogLevel.None)]
        public void LoggerFactory_MinimumLevel_FiltersEverySeverity(LogLevel minimumLevel)
        {
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            using var outputWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            LogLevel[] messageLevels = [LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical];

            try
            {
                Console.SetOut(outputWriter);
                Console.SetError(errorWriter);
                using var host = CreateHost(minimumLevel);
                foreach (LogLevel messageLevel in messageLevels)
                {
                    host.Logger.Log(messageLevel, "matrix-{Level}", messageLevel);
                }
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }

            Assert.IsEmpty(errorWriter.ToString());
            string output = outputWriter.ToString();
            foreach (LogLevel messageLevel in messageLevels)
            {
                Assert.AreEqual(
                    minimumLevel != LogLevel.None && messageLevel >= minimumLevel,
                    output.Contains($"matrix-{messageLevel}", StringComparison.Ordinal));
            }
        }

        [TestMethod]
        public void LoggerFactory_PreservesFilteringFormatterAndDrainOnDispose()
        {
            const int hostCount = 5;
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            using var outputWriter = new StringWriter();
            using var errorWriter = new StringWriter();

            try
            {
                Console.SetOut(outputWriter);
                Console.SetError(errorWriter);

                using (var host = CreateHost(LogLevel.Warning))
                {
                    host.Logger.LogInformation("filtered information");
                    host.Logger.LogWarning("visible warning");
                }

                for (int index = 0; index < hostCount; index++)
                {
                    using var host = CreateHost(LogLevel.Debug);
                    using (host.Logger.BeginScope($"scope-{index}"))
                    {
                        host.Logger.LogDebug(new InvalidOperationException($"debug details {index}"), $"debug message {index}");
                    }
                    host.Logger.LogInformation($"information message {index}");
                    host.Logger.LogWarning(new InvalidOperationException($"warning details {index}"), $"warning message {index}");
                    host.Logger.LogError(new InvalidOperationException($"error details {index}"), $"error message {index}");
                    host.Logger.LogInformation($"final queued message {index}");
                }
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }

            string output = outputWriter.ToString();
            Assert.IsEmpty(errorWriter.ToString());
            output.Should().NotContain("filtered information");
            output.Should().Contain("Warning: visible warning");
            for (int index = 0; index < hostCount; index++)
            {
                output.Should().Contain($"[Debug] [Template Engine] => [scope-{index}]: debug message {index}");
                output.Should().Contain($"Details: System.InvalidOperationException: debug details {index}");
                output.Should().Contain($"information message {index}");
                output.Should().Contain($"Warning: warning message {index}");
                output.Should().Contain($"Details: warning details {index}");
                output.Should().Contain($"Error: error message {index}");
                output.Should().Contain($"Details: error details {index}");
                output.Should().Contain($"final queued message {index}");
            }
        }

        private static CliTemplateEngineHost CreateHost(LogLevel logLevel)
            => new(
                "test-host",
                "1.0.0",
                [],
                [],
                logLevel: logLevel);
    }
}