// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public abstract class EndToEndTestBase
    {
        public void Run(string args, params string[] scripts)
        {
            string codebase = typeof(EndToEndTestBase).GetTypeInfo().Assembly.Location;
            Uri cb = new Uri(codebase);
            string asmPath = cb.LocalPath;
            DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(asmPath));

#if DEBUG
            string configuration = "Debug";
#else
            string configuration = "Release";
#endif
            string harnessPath = Path.Combine(dir.FullName, "..", "..", "Microsoft.TemplateEngine.EndToEndTestHarness", configuration);
            int scriptCount = scripts.Length;
            StringBuilder builder = new StringBuilder();
            builder.Append(scriptCount);
            builder.Append(' ');

            foreach (string script in scripts)
            {
                string testScript = Path.Combine(dir.FullName, "Resources", script);
                builder.Append($"\"{testScript}\" ");
            }

            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "temp");

            string testAssetsRoot = Path.Combine(TestContext.Current.TestAssetsDirectory, "TestPackages", "dotnet-new");

            var startInfo = new ProcessStartInfo
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = harnessPath,
                FileName = "dotnet",
                Arguments = $"Microsoft.TemplateEngine.EndToEndTestHarness.dll {builder} \"{outputPath}\" \"{testAssetsRoot}\" {args} -o \"{outputPath}\""
            };

            Process p = Process.Start(startInfo);

            StringBuilder errorData = new StringBuilder();
            StringBuilder outputData = new StringBuilder();

            errorData.AppendLine($"TestAssetsRoot: {testAssetsRoot}");
            errorData.AppendLine($"WorkingDirectory: {harnessPath}");
            errorData.AppendLine($"OutputPath: {outputPath}");

            var home = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME";
            errorData.AppendLine($"CLI_HOME: {Environment.GetEnvironmentVariable("DOTNET_CLI_HOME")}");
            errorData.AppendLine($"HOME: {Environment.GetEnvironmentVariable(home)}");
            errorData.AppendLine($"UserProfile: {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}");
            errorData.AppendLine($"Result of CliFolderPathCalculator: {CliFolderPathCalculator.DotnetHomePath}");

            p.ErrorDataReceived += (sender, e) =>
            {
                errorData.AppendLine(e.Data);
            };

            p.OutputDataReceived += (sender, e) =>
            {
                outputData.AppendLine(e.Data);
            };

            p.BeginErrorReadLine();
            p.BeginOutputReadLine();
            p.WaitForExit();

            string output = outputData.ToString();
            string error = errorData.ToString();
            Assert.True(0 == p.ExitCode, $@"stdout:
{output}

stderr:
{error}");
        }
    }
}
