// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

/// <summary>
/// Web project create, build, run, publish scenario tests.
/// <see cref="BaseScenarioTests"/> for related basic scenarios.
/// They are encapsulated in a separate testclass so that they can be run in parallel.
/// </summary>
public class WebScenarioTests : SdkTests
{
    public WebScenarioTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    [Theory]
    [MemberData(nameof(GetScenarioObjects))]
    public void VerifyScenario(TestScenario scenario) => scenario.Execute(DotNetHelper);

    public static IEnumerable<object[]> GetScenarioObjects() => GetScenarios().Select(scenario => new object[] { scenario });

    private static IEnumerable<TestScenario> GetScenarios()
    {
        foreach (DotNetLanguage language in new[] { DotNetLanguage.CSharp, DotNetLanguage.FSharp })
        {
            yield return new(nameof(WebScenarioTests), language, DotNetTemplate.Web,    DotNetActions.Build | DotNetActions.Run | (DotNetHelper.ShouldPublishComplex() ? DotNetActions.PublishComplex : DotNetActions.None));
            yield return new(nameof(WebScenarioTests), language, DotNetTemplate.Mvc,    DotNetActions.Build | DotNetActions.Run | DotNetActions.Publish) { NoHttps = true };
            yield return new(nameof(WebScenarioTests), language, DotNetTemplate.WebApi, DotNetActions.Build | DotNetActions.Run | DotNetActions.Publish);
        }

        yield return new(nameof(WebScenarioTests), DotNetLanguage.CSharp, DotNetTemplate.Razor,         DotNetActions.Build | DotNetActions.Run | DotNetActions.Publish);
        yield return new(nameof(WebScenarioTests), DotNetLanguage.CSharp, DotNetTemplate.BlazorWasm,    DotNetActions.Build | DotNetActions.Run | DotNetActions.Publish);
        yield return new(nameof(WebScenarioTests), DotNetLanguage.CSharp, DotNetTemplate.WebApp,        DotNetActions.PublishSelfContained, VerifyRuntimePacksForSelfContained);
        yield return new(nameof(WebScenarioTests), DotNetLanguage.CSharp, DotNetTemplate.Worker);
    }

    private static void VerifyRuntimePacksForSelfContained(string projectPath)
    {
        // 'expectedPackageFiles' key in project.nuget.cache' will contain paths to restored packages
        // Since we are publishing an emtpy template, the only packages that could end up there are the ref packs we are after

        string projNugetCachePath = Path.Combine(projectPath, "obj", "project.nuget.cache");

        JsonNode? projNugetCache = JsonNode.Parse(File.ReadAllText(projNugetCachePath));
        JsonArray? restoredPackageFiles = (JsonArray?)projNugetCache?["expectedPackageFiles"];

        Assert.True(restoredPackageFiles is not null, "Failed to parse project.nuget.cache");

        string[] allowedPackages = [
            // Temporarily allowed due to https://github.com/dotnet/sdk/issues/46165
            // TODO: Remove this once the issue is resolved
            "Microsoft.AspNetCore.App.Internal.Assets"
        ];

        string packagesDirectory = Path.Combine(Environment.CurrentDirectory, "packages");

        IEnumerable<string> packages = restoredPackageFiles
            .Select(file =>
            {
                string path = file.ToString();
                path = path.Substring(packagesDirectory.Length + 1); // trim the leading path up to the package name directory
                return path.Substring(0, path.IndexOf('/')); // trim the rest of the path
            })
            .Except(allowedPackages, StringComparer.OrdinalIgnoreCase);

        if (packages.Any())
        {
            Assert.Fail($"The following runtime packs were retrieved from NuGet instead of the SDK: {string.Join(",", packages.ToArray())}");
        }
    }
}
