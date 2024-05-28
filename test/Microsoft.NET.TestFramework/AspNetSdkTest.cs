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
                    }
                    var targetFrameworks = project.Descendants()
                        .SingleOrDefault(e => e.Name.LocalName == "TargetFrameworks");
                    if (targetFrameworks != null)
                    {
                        targetFrameworks.Value = targetFrameworks.Value.Replace("$(AspNetTestTfm)", overrideTfm ?? DefaultTfm);
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
            if (Debugger.IsAttached)
            {
                return command.Execute(["/bl", .. arguments]);
            }
            else
            {
                return command.Execute(arguments);
            }
        }

        protected virtual CommandResult ExecuteCommandWithoutRestore(MSBuildCommand command, params string[] arguments)
        {
            if (Debugger.IsAttached)
            {
                return command.ExecuteWithoutRestore(["/bl", .. arguments]);
            }
            else
            {
                return command.ExecuteWithoutRestore(arguments);
            }
        }

        private void ApplyDefaults(MSBuildCommand command)
        {
            if (GetNuGetCachePath() is { } cache)
            {
                command.WithEnvironmentVariable("NUGET_PACKAGES", cache);
                command.WithEnvironmentVariable("AspNetNugetIsolationPath", cache);
            }
        }

        protected virtual string GetNuGetCachePath() => null;
    }
}
