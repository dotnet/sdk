// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework
{
    [Trait("AspNetCore", "Integration")]
    public abstract class AspNetSdkTest : SdkTest
    {
        public readonly string DefaultTfm;

#if !GENERATE_MSBUILD_LOGS
        public static bool GenerateMSbuildLogs = true;
#else
        public static bool GenerateMSbuildLogs = bool.TryParse(Environment.GetEnvironmentVariable("ASPNETCORE_GENERATE_BINLOG"), out var result) && result || Debugger.IsAttached;
#endif

        private bool _generateMSbuildLogs = GenerateMSbuildLogs;

        protected AspNetSdkTest(ITestOutputHelper log) : base(log)
        {
            var assembly = Assembly.GetCallingAssembly();
            var testAssemblyMetadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            DefaultTfm = testAssemblyMetadata.SingleOrDefault(a => a.Key == "AspNetTestTfm").Value;
        }

        public TestAsset CreateAspNetSdkTestAsset(
            string testAsset,
            [CallerMemberName] string callerName = "",
            string subdirectory = "",
            string overrideTfm = null,
            string identifier = null)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, callingMethod: callerName, testAssetSubdirectory: subdirectory, identifier: identifier)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var targetFramework = project.Descendants()
                       .SingleOrDefault(e => e.Name.LocalName == "TargetFramework");
                    if (targetFramework?.Value == "$(AspNetTestTfm)")
                    {
                        targetFramework.Value = overrideTfm ?? DefaultTfm;
                        targetFramework.AddAfterSelf(new XElement("StaticWebAssetsFingerprintContent", "false"));
                    }
                    var targetFrameworks = project.Descendants()
                        .SingleOrDefault(e => e.Name.LocalName == "TargetFrameworks");
                    if (targetFrameworks != null)
                    {
                        targetFrameworks.Value = targetFrameworks.Value.Replace("$(AspNetTestTfm)", overrideTfm ?? DefaultTfm);
                        targetFrameworks.AddAfterSelf(new XElement("StaticWebAssetsFingerprintContent", "false"));
                    }
                });

            foreach (string assetPath in Directory.EnumerateFiles(Path.Combine(_testAssetsManager.TestAssetsRoot, "WasmOverride")))
                File.Copy(assetPath, Path.Combine(projectDirectory.Path, Path.GetFileName(assetPath)));

            return projectDirectory;
        }

        public TestAsset CreateMultitargetAspNetSdkTestAsset(
            string testAsset,
            [CallerMemberName] string callerName = "",
            string subdirectory = "",
            string overrideTfm = null,
            string identifier = null)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, callingMethod: callerName, testAssetSubdirectory: subdirectory, identifier: identifier)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var targetFramework = project.Descendants()
                       .Single(e => e.Name.LocalName == "TargetFrameworks");
                    targetFramework.Value = targetFramework.Value.Replace("$(AspNetTestTfm)", overrideTfm ?? DefaultTfm);
                });
            return projectDirectory;
        }

        protected virtual RestoreCommand CreateRestoreCommand(TestAsset asset, string relativePathToProject = null)
        {
            var restore = new RestoreCommand(asset, relativePathToProject);
            restore.WithWorkingDirectory(asset.TestRoot);
            ApplyDefaults(restore);
            return restore;
        }

        protected virtual BuildCommand CreateBuildCommand(TestAsset asset, string relativePathToProject = null)
        {
            var build = new BuildCommand(asset, relativePathToProject);
            build.WithWorkingDirectory(asset.TestRoot);
            ApplyDefaults(build);

            return build;
        }

        protected virtual RebuildCommand CreateRebuildCommand(TestAsset asset, string relativePathToProject = null)
        {
            var rebuild = new RebuildCommand(Log, asset.Path, relativePathToProject);
            rebuild.WithWorkingDirectory(asset.TestRoot);
            ApplyDefaults(rebuild);

            return rebuild;
        }

        protected virtual PackCommand CreatePackCommand(TestAsset asset, string relativePathToProject = null)
        {
            var pack = new PackCommand(asset, relativePathToProject);
            pack.WithWorkingDirectory(asset.TestRoot);
            ApplyDefaults(pack);

            return pack;
        }

        protected virtual PublishCommand CreatePublishCommand(TestAsset asset, string relativePathToProject = null)
        {
            var publish = new PublishCommand(asset, relativePathToProject);
            publish.WithWorkingDirectory(asset.TestRoot);
            ApplyDefaults(publish);

            return publish;
        }

        protected virtual CommandResult ExecuteCommand(TestCommand command, params string[] arguments)
        {
            ValidateIndividualArgumentsContainNoSpaces(arguments);

            if (_generateMSbuildLogs)
            {
                var i = 0;
                for (i = 0; File.Exists(Path.Combine(command.WorkingDirectory, $"msbuild{i}.binlog")) && i < 20; i++) { }
                var log = $"msbuild{i}.binlog";

                return command.Execute([$"/bl:{log}", .. arguments]);
            }
            else
            {
                return command.Execute(arguments);
            }
        }

        protected virtual CommandResult ExecuteCommandWithoutRestore(MSBuildCommand command, params string[] arguments)
        {
            ValidateIndividualArgumentsContainNoSpaces(arguments);

            if (_generateMSbuildLogs)
            {
                var i = 0;
                for (i = 0; File.Exists(Path.Combine(command.WorkingDirectory, $"msbuild{i}.binlog")) && i < 20; i++) { }
                var log = $"msbuild{i}.binlog";
                return command.ExecuteWithoutRestore([$"/bl:{log}", .. arguments]);
            }
            else
            {
                return command.ExecuteWithoutRestore(arguments);
            }
        }

        private void ValidateIndividualArgumentsContainNoSpaces(string[] arguments)
        {
            foreach (var argument in arguments)
            {
                // assume our tests don't need to pass msbuild properties with spaces
                if (argument.Contains(' '))
                {
                    throw new ArgumentException($"Individual arguments should not contain spaces to avoid quoting issues when passing to msbuild, pass them as separate array elements instead. Argument: {argument}");
                }
            }
        }

        private void ApplyDefaults(MSBuildCommand command)
        {
            if (GetNuGetCachePath() is { } cache)
            {
                command.WithEnvironmentVariable("NUGET_PACKAGES", cache);
                command.WithEnvironmentVariable("AspNetNugetIsolationPath", cache);
                command.WithEnvironmentVariable("RestorePackagesPath", cache);
            }
        }

        protected virtual string GetNuGetCachePath() => null;
    }
}
