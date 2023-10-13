// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.AccessControl;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

/// <summary>
/// OmniSharp tests to ensure it works with a source-built sdk.
/// </summary>
public class OmniSharpTests : SdkTests
{
    // Update version as new releases become available: https://github.com/OmniSharp/omnisharp-roslyn/releases
    private const string OmniSharpReleaseVersion = "1.39.8";

    private string OmniSharpDirectory { get; } = Path.Combine(Directory.GetCurrentDirectory(), "omnisharp");

    public OmniSharpTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    [SkippableTheory(Config.ExcludeOmniSharpEnv, skipOnTrueEnv: true, skipArchitectures: new[] { "ppc64le", "s390x" })]
    [InlineData(DotNetTemplate.BlazorWasm)]
    [InlineData(DotNetTemplate.ClassLib)]
    [InlineData(DotNetTemplate.Console)]
    [InlineData(DotNetTemplate.MSTest)]
    [InlineData(DotNetTemplate.Mvc)]
    [InlineData(DotNetTemplate.NUnit)]
    [InlineData(DotNetTemplate.Web)]
    [InlineData(DotNetTemplate.WebApp)]
    [InlineData(DotNetTemplate.WebApi)]
    [InlineData(DotNetTemplate.Worker)]
    [InlineData(DotNetTemplate.XUnit)]
    public async void VerifyScenario(DotNetTemplate template)
    {
        await InitializeOmniSharp();

        string templateName = template.GetName();
        string projectName = $"{nameof(OmniSharpTests)}_{templateName}";
        string projectDirectory = DotNetHelper.ExecuteNew(templateName, projectName);

        (Process Process, string StdOut, string StdErr) executeResult = ExecuteHelper.ExecuteProcess(
            Path.Combine(OmniSharpDirectory, "run"),
            $"-s {projectDirectory}",
            OutputHelper,
            logOutput: true,
            millisecondTimeout: 5000,
            configureCallback: (process) => DotNetHelper.ConfigureProcess(process, projectDirectory, setPath: true));

        Assert.NotEqual(0, executeResult.Process.ExitCode);
        Assert.DoesNotContain("ERROR", executeResult.StdOut);
        Assert.DoesNotContain("ERROR", executeResult.StdErr);
    }

    private async Task InitializeOmniSharp()
    {
        if (!Directory.Exists(OmniSharpDirectory))
        {
            using HttpClient client = new();
            string omniSharpTarballFile = $"omnisharp-linux-{Config.TargetArchitecture}.tar.gz";
            Uri omniSharpTarballUrl = new($"https://github.com/OmniSharp/omnisharp-roslyn/releases/download/v{OmniSharpReleaseVersion}/{omniSharpTarballFile}");
            await client.DownloadFileAsync(omniSharpTarballUrl, omniSharpTarballFile, OutputHelper);

            Directory.CreateDirectory(OmniSharpDirectory);
            Utilities.ExtractTarball(omniSharpTarballFile, OmniSharpDirectory, OutputHelper);
            
            // Ensure the run script is executable (see https://github.com/OmniSharp/omnisharp-roslyn/issues/2547)
            File.SetUnixFileMode($"{OmniSharpDirectory}/run", UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
    }
}
