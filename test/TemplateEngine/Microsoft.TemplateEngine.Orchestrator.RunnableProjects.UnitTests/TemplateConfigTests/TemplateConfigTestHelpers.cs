// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class TemplateConfigTestHelpers
    {
        public static readonly Guid FileSystemMountPointFactoryId = new Guid("8C19221B-DEA3-4250-86FE-2D4E189A11D2");
        public static readonly string DefaultConfigRelativePath = ".template.config/template.json";

        internal static void SetupFileSourceMatchersOnGlobalRunSpec(MockGlobalRunSpec runSpec, FileSourceMatchInfo source)
        {
            FileSourceHierarchicalPathMatcher matcher = new FileSourceHierarchicalPathMatcher(source);
            runSpec.Include = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.Include, matcher) };
            runSpec.Exclude = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.Exclude, matcher) };
            runSpec.CopyOnly = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.CopyOnly, matcher) };
            runSpec.Rename = source.Renames ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        internal static void WriteTemplateSource(
            IEngineEnvironmentSettings environment,
            string sourceBasePath,
            IDictionary<string, string?> templateSourceFileNamesWithContent)
        {
            foreach (KeyValuePair<string, string?> fileInfo in templateSourceFileNamesWithContent)
            {
                string filePath = Path.Combine(sourceBasePath, fileInfo.Key);
                string fullPathDir = Path.GetDirectoryName(filePath)!;
                environment.Host.FileSystem.CreateDirectory(fullPathDir);
                environment.Host.FileSystem.WriteAllText(filePath, fileInfo.Value ?? string.Empty);
            }
        }

        internal static IMountPoint CreateMountPoint(IEngineEnvironmentSettings environment, string sourceBasePath)
        {
            foreach (var factory in environment.Components.OfType<IMountPointFactory>())
            {
                if (factory.TryMount(environment, null, sourceBasePath, out IMountPoint sourceMountPoint))
                {
                    return sourceMountPoint;
                }
            }
            Assert.True(false, "couldn't create source mount point");
            throw new Exception("couldn't create source mount point");
        }
    }
}
