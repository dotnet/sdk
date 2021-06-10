// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Mocks;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class TemplateConfigTestHelpers
    {
        public static readonly Guid FileSystemMountPointFactoryId = new Guid("8C19221B-DEA3-4250-86FE-2D4E189A11D2");
        public static readonly string DefaultConfigRelativePath = ".template.config/template.json";

        public static IFileSystemInfo ConfigFileSystemInfo(IMountPoint mountPoint, string configFile = null)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                configFile = DefaultConfigRelativePath;
            }

            return mountPoint.FileInfo(configFile);
        }

        // Note: this does not deal with configs split into multiple files.
        internal static IRunnableProjectConfig ConfigFromSource(IEngineEnvironmentSettings environment, IMountPoint mountPoint, string configFile = null)
        {
            return new SimpleConfigModel((IFile)ConfigFileSystemInfo(mountPoint, configFile));
        }

        internal static void SetupFileSourceMatchersOnGlobalRunSpec(MockGlobalRunSpec runSpec, FileSourceMatchInfo source)
        {
            FileSourceHierarchicalPathMatcher matcher = new FileSourceHierarchicalPathMatcher(source);
            runSpec.Include = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.Include, matcher) };
            runSpec.Exclude = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.Exclude, matcher) };
            runSpec.CopyOnly = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.CopyOnly, matcher) };
            runSpec.Rename = source.Renames ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
