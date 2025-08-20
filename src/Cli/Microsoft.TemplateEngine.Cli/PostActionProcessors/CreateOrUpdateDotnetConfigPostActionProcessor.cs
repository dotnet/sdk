// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal sealed class CreateOrUpdateDotnetConfigPostActionProcessor : PostActionProcessorBase
    {
        private const string SectionArgument = "section";
        private const string KeyArgument = "key";
        private const string ValueArgument = "value";

        public override Guid Id => ActionProcessorId;

        internal static Guid ActionProcessorId { get; } = new Guid("597E7933-0D87-452C-B094-8FA0EEF7FD97");

        protected override bool ProcessInternal(
            IEngineEnvironmentSettings environment,
            IPostAction action,
            ICreationEffects creationEffects,
            ICreationResult templateCreationResult,
            string outputBasePath)
        {
            if (!action.Args.TryGetValue(SectionArgument, out string? sectionName))
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_DotnetConfig_Error_ArgumentNotConfigured, SectionArgument));
                return false;
            }

            if (!action.Args.TryGetValue(KeyArgument, out string? key))
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_DotnetConfig_Error_ArgumentNotConfigured, KeyArgument));
                return false;
            }

            if (!action.Args.TryGetValue(ValueArgument, out string? value))
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_DotnetConfig_Error_ArgumentNotConfigured, ValueArgument));
                return false;
            }

            var fileSystem = environment.Host.FileSystem;
            var repoRoot = GetRootDirectory(fileSystem, outputBasePath);
            var dotnetConfigFilePath = Path.Combine(repoRoot, "dotnet.config");
            if (!fileSystem.FileExists(dotnetConfigFilePath))
            {
                fileSystem.WriteAllText(dotnetConfigFilePath, $"""
                    [{sectionName}]
                    {key} = "{value}"

                    """);

                Reporter.Output.WriteLine(LocalizableStrings.PostAction_CreateDotnetConfig_Succeeded);
                return true;
            }

            var builder = new ConfigurationBuilder();
            using var stream = fileSystem.OpenRead(dotnetConfigFilePath);
            builder.AddIniStream(stream);
            IConfigurationRoot config = builder.Build();
            var section = config.GetSection(sectionName);

            if (!section.Exists())
            {
                var existingContent = fileSystem.ReadAllText(dotnetConfigFilePath);
                fileSystem.WriteAllText(dotnetConfigFilePath, $"""
                    {existingContent}

                    [{sectionName}]
                    {key} = "{value}"

                    """);

                Reporter.Output.WriteLine(LocalizableStrings.PostAction_CreateDotnetConfig_CreatedNewSection);
                return true;
            }

            string? existingValue = section[key];
            if (string.IsNullOrEmpty(existingValue))
            {
                // The section exists, but the key/value pair does not.
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_CreateDotnetConfig_ManuallyUpdate, $"{key} = \"{value}\"", $"[{sectionName}]"));
                return false;
            }

            if (existingValue.Equals(value, StringComparison.Ordinal))
            {
                // The key already exists with the same value, nothing to do.
                Reporter.Output.WriteLine(LocalizableStrings.PostAction_CreateDotnetConfig_ValueAlreadyExist);
                return true;
            }

            Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_CreateDotnetConfig_ManuallyUpdate, $"{key} = \"{value}\"", $"[{sectionName}]"));
            return false;
        }

        private static string GetRootDirectory(IPhysicalFileSystem fileSystem, string outputBasePath)
        {
            string? currentDirectory = outputBasePath;
            string? directoryWithSln = null;
            while (currentDirectory is not null)
            {
                if (fileSystem.FileExists(Path.Combine(currentDirectory, "dotnet.config")) ||
                    fileSystem.DirectoryExists(Path.Combine(currentDirectory, ".git")))
                {
                    return currentDirectory;
                }

                // DirectoryExists here should always be true in practice, but for the way tests are mocking the file system, it's not.
                // The check was added to prevent test failures similar to:
                // System.IO.DirectoryNotFoundException : Could not find a part of the path '/Users/runner/work/1/s/artifacts/bin/Microsoft.TemplateEngine.Cli.UnitTests/Release/sandbox'.
                // at System.IO.Enumeration.FileSystemEnumerator`1.CreateDirectoryHandle(String path, Boolean ignoreNotFound)
                // at System.IO.Enumeration.FileSystemEnumerator`1.Init()
                // at System.IO.Enumeration.FileSystemEnumerable`1..ctor(String directory, FindTransform transform, EnumerationOptions options, Boolean isNormalized)
                // at System.IO.Enumeration.FileSystemEnumerableFactory.UserFiles(String directory, String expression, EnumerationOptions options)
                // at System.IO.Directory.InternalEnumeratePaths(String path, String searchPattern, SearchTarget searchTarget, EnumerationOptions enumerationOptions)
                // at Microsoft.TemplateEngine.Utils.PhysicalFileSystem.EnumerateFiles(String path, String pattern, SearchOption searchOption)
                // at Microsoft.TemplateEngine.TestHelper.MonitoredFileSystem.EnumerateFiles(String path, String pattern, SearchOption searchOption)
                // at Microsoft.TemplateEngine.Utils.InMemoryFileSystem.EnumerateFiles(String path, String pattern, SearchOption searchOption)+MoveNext()
                // at Microsoft.TemplateEngine.Utils.InMemoryFileSystem.EnumerateFiles(String path, String pattern, SearchOption searchOption)+MoveNext()
                // at System.Linq.Enumerable.Any[TSource](IEnumerable`1 source)
                // at Microsoft.TemplateEngine.Cli.PostActionProcessors.CreateOrUpdateDotnetConfigPostActionProcessor.GetRootDirectory(IPhysicalFileSystem fileSystem, String outputBasePath) in /_/src/Cli/Microsoft.TemplateEngine.Cli/PostActionProcessors/CreateOrUpdateDotnetConfigPostActionProcessor.cs:line 113
                // at Microsoft.TemplateEngine.Cli.PostActionProcessors.CreateOrUpdateDotnetConfigPostActionProcessor.ProcessInternal(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects creationEffects, ICreationResult templateCreationResult, String outputBasePath) in /_/src/Cli/Microsoft.TemplateEngine.Cli/PostActionProcessors/CreateOrUpdateDotnetConfigPostActionProcessor.cs:line 47
                // at Microsoft.TemplateEngine.Cli.PostActionProcessors.PostActionProcessorBase.Process(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects creationEffects, ICreationResult templateCreationResult, String outputBasePath) in /_/src/Cli/Microsoft.TemplateEngine.Cli/PostActionProcessors/PostActionProcessorBase.cs:line 26
                // at Microsoft.TemplateEngine.Cli.UnitTests.PostActionTests.CreateOrUpdateDotnetConfigPostActionTests.CreatesDotnetConfigWhenDoesNotExist() in /Users/runner/work/1/s/test/Microsoft.TemplateEngine.Cli.UnitTests/PostActionTests/CreateOrUpdateDotnetConfigPostActionTests.cs:line 84
                // at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)
                // at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
                if (fileSystem.DirectoryExists(currentDirectory) &&
                    (fileSystem.EnumerateFiles(currentDirectory, "*.sln", SearchOption.TopDirectoryOnly).Any() ||
                    fileSystem.EnumerateFiles(currentDirectory, "*.slnx", SearchOption.TopDirectoryOnly).Any()))
                {
                    directoryWithSln = currentDirectory;
                }

                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }

            return directoryWithSln ?? outputBasePath;
        }
    }
}
