// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Razor.Tests;

[TestClass]
public class SdkRazorTagHelperTest
{
    [TestMethod]
    public void GenerateResponseFileCommands_IncludesCSharpLanguageVersion()
    {
        var configuration = new TaskItem("MVC-3.0");
        var task = new TestableSdkRazorTagHelper
        {
            Assemblies = [],
            Configuration = [configuration],
            CSharpLanguageVersion = "preview",
            Extensions = [],
            ProjectRoot = Directory.GetCurrentDirectory(),
            TagHelperManifest = "taghelpers.json",
            Version = "Latest",
        };

        Assert.Contains(
            $"--csharp-language-version{Environment.NewLine}preview{Environment.NewLine}",
            task.GetResponseFileCommands());
    }

    private sealed class TestableSdkRazorTagHelper : SdkRazorTagHelper
    {
        public string GetResponseFileCommands() => GenerateResponseFileCommands();
    }
}
