// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class DotNetWatchTests : SdkTests
{
    public DotNetWatchTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    [Fact]
    public void WatchTests()
    {
        if (DotNetHelper.IsMonoRuntime)
        {
            // TODO: Temporarily disabled due to https://github.com/dotnet/sdk/issues/37774
            return;
        }
        
        string projectDirectory = DotNetHelper.ExecuteNew(DotNetTemplate.Console.GetName(), nameof(DotNetWatchTests));
        bool outputChanged = false;

        DotNetHelper.ExecuteCmd(
            "watch run --non-interactive --verbose",
            workingDirectory: projectDirectory,
            processConfigCallback: processConfigCallback,
            expectedExitCode: null, // The exit code does not reflect whether or not dotnet watch is working properly
            millisecondTimeout: 60000);

        Assert.True(outputChanged);

        void processConfigCallback(Process process)
        {
            const string waitingString = "Waiting for changes";
            const string expectedString = "Hello from dotnet watch!";

            bool fileChanged = false;

            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                try
                {
                    OutputHelper.WriteLine(e.Data);
                }
                catch 
                {
                    // avoid System.InvalidOperationException: There is no currently active test.
                }

                if (e.Data.Contains(waitingString))
                {
                    if (!fileChanged) {
                        OutputHelper.WriteLine("Program started, changing file on disk to trigger restart...");
                        File.WriteAllText(
                            Path.Combine(projectDirectory, "Program.cs"),
                            File.ReadAllText(Path.Combine(projectDirectory, "Program.cs")).Replace("Hello, World!", expectedString));
                        fileChanged = true;
                    }
                }
                else if (e.Data.Contains(expectedString))
                {
                    outputChanged = true;
                    OutputHelper.WriteLine("Successfully re-ran program after code change.");
                    process.Kill(true);
                }
            });
        }
    }
}
