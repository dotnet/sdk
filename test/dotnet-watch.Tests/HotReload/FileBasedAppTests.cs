// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.Watch.UnitTests;

public class FileBasedAppTests(ITestOutputHelper output) : DotNetWatchTestBase(output)
{
    [Fact]
    public async Task IncludeDirective()
    {
        var testAsset = TestAssets.CreateTestAsset("FBA");

        var entryPointFilePath = Path.Combine(testAsset.Path, "App.cs");
        File.WriteAllText(entryPointFilePath, """
            #:property ExperimentalFileBasedProgramEnableIncludeDirective=true
            #:property StartupHookSupport=true
            #:include Lib.cs

            using System.Reflection;
            using System.Runtime.Versioning;
            
            Console.WriteLine(typeof(Program).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);

            while (true)
            {
                Lib.F();
                Thread.Sleep(1000);
            }
            """);

        var referencedFilePath = Path.Combine(testAsset.Path, "Lib.cs");
        File.WriteAllText(referencedFilePath, """
            class Lib
            {
                public static void F()
                {
                    Console.WriteLine("Library");
                }
            }
            """);

        App.Start(testAsset, ["App.cs"]);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        await App.WaitUntilOutputContains(ToolsetInfo.CurrentTargetFrameworkMoniker);
        await App.WaitUntilOutputContains("Library");

        App.Process.ClearOutput();
        UpdateSourceFile(referencedFilePath, src => src.Replace("Library", "<Updated>"));

        await App.WaitUntilOutputContains("<Updated>");
    }

    [Fact]
    public async Task TargetFrameworks_Selection()
    {
        var testAsset = TestAssets.CreateTestAsset("FBA");

        var entryPointFilePath = Path.Combine(testAsset.Path, "App.cs");
        File.WriteAllText(entryPointFilePath, """
            #:property TargetFrameworks= net9.0; net10.0
            using System.Reflection;
            using System.Runtime.Versioning;

            Console.WriteLine(typeof(Program).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
            """);

        App.Start(testAsset, ["App.cs"], testFlags: TestFlags.ReadKeyFromStdin);

        await App.WaitUntilOutputContains(Resources.SelectTargetFrameworkPrompt);

        foreach (var c in "net9.0")
        {
            App.SendKey(c);
        }

        App.SendKey('\r');

        await App.WaitUntilOutputContains(".NETCoreApp,Version=v9.0");
        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
    }

    [Fact]
    public async Task TargetFramework()
    {
        var testAsset = TestAssets.CreateTestAsset("FBA");

        var entryPointFilePath = Path.Combine(testAsset.Path, "App.cs");
        File.WriteAllText(entryPointFilePath, """
            #:property TargetFramework=net9.0
            using System.Reflection;
            using System.Runtime.Versioning;

            Console.WriteLine(typeof(Program).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
            """);

        App.Start(testAsset, ["App.cs"]);

        await App.WaitUntilOutputContains(".NETCoreApp,Version=v9.0");
        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
    }
}
