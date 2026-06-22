// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Regression tests for the default <see cref="TaskEnvironment"/> value on SDK tasks.
    ///
    /// Background: tasks implementing <see cref="IMultiThreadableTask"/> have their
    /// <see cref="IMultiThreadableTask.TaskEnvironment"/> set by the MSBuild engine before
    /// <c>Execute</c> is called when running on MSBuild that supports the interface.
    /// For all other callers (older MSBuild, direct unit tests, callers that forget to set
    /// the property), the default value of the property must be a usable, non-null
    /// <see cref="TaskEnvironment"/>. Tasks default to <see cref="TaskEnvironment.Fallback"/>
    /// which uses live process state, equivalent to the legacy single-threaded behavior.
    /// </summary>
    public class GivenATaskEnvironmentDefault
    {
        [Fact]
        public void NewTaskInstance_HasNonNullTaskEnvironment_EqualToFallback()
        {
            var task = new GenerateClsidMap();
            task.TaskEnvironment.Should().NotBeNull(
                "tasks must default to a usable TaskEnvironment so legacy callers and tests don't NRE");
            task.TaskEnvironment.Should().BeSameAs(TaskEnvironment.Fallback,
                "the default should be the shared Fallback singleton (live process state) for backwards compatibility");
        }

        [Fact]
        public void Fallback_GetAbsolutePath_ResolvesAgainstLiveCwd()
        {
            var task = new GenerateClsidMap();

            var savedCwd = Directory.GetCurrentDirectory();
            var probeDir = Path.Combine(Path.GetTempPath(), "tef-probe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(probeDir);
            try
            {
                Directory.SetCurrentDirectory(probeDir);
                // Re-read the CWD so we compare against the path the OS gave us back -- on macOS
                // /var/.../tef-probe-xxx is symlinked to /private/var/.../tef-probe-xxx and
                // Directory.SetCurrentDirectory resolves through the symlink.
                var liveCwd = Directory.GetCurrentDirectory();

                AbsolutePath resolved = task.TaskEnvironment.GetAbsolutePath("relative.txt");
                resolved.Value.Should().StartWith(liveCwd,
                    "TaskEnvironment.Fallback uses the live process CWD for relative path resolution");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                Directory.Delete(probeDir, recursive: true);
            }
        }

        [Fact]
        public void DifferentTaskInstances_ShareTheFallbackSingleton()
        {
            var a = new GenerateClsidMap();
            var b = new CreateAppHost();
            a.TaskEnvironment.Should().BeSameAs(b.TaskEnvironment,
                "Fallback is a shared singleton; this is fine because it carries no isolated state");
        }
    }
}
