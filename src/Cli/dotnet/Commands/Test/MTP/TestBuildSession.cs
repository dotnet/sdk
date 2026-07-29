// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Runs the MSBuild targets that <c>dotnet test</c> needs on top of the build (for example
/// <c>ComputeRunArguments</c>) for every test project of a run inside a single MSBuild build session.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ProjectInstance.Build(string[], IEnumerable{ILogger})"/> runs a complete MSBuild build
/// (begin/request/end) on every call. Each of those builds creates its own logging service, so the
/// <see cref="BuildEventContext"/> ids it hands out - project context ids, target ids, task ids -
/// restart from zero, and every build emits its own build-started/build-finished pair.
/// Attaching the same binary logger to several of those builds, which is what <c>dotnet test -bl</c>
/// does when a run contains more than one test project, produces a binlog holding several builds
/// with colliding ids: readers attribute every project's targets to whichever project claimed the id
/// first, and the other projects appear to have executed no targets at all.
/// </para>
/// <para>
/// Sending all the requests to a single <see cref="BuildManager"/> session instead yields one build
/// with unique ids, so the binlog produced by <c>dotnet test -bl</c> is well formed and shows the
/// targets executed for each test project.
/// </para>
/// <para>
/// The session is started lazily, so a caller can create it up front and still run MSBuild through
/// <see cref="BuildManager.DefaultBuildManager"/> (for example to build or restore the projects
/// under test) before the first target invocation.
/// </para>
/// </remarks>
[RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
internal sealed class TestBuildSession : IDisposable
{
    private readonly ProjectCollection _projectCollection;
    private readonly FacadeLogger? _logger;
    private readonly Lock _lock = new();

    private BuildManager? _buildManager;

    public TestBuildSession(ProjectCollection projectCollection, FacadeLogger? logger)
    {
        _projectCollection = projectCollection;
        _logger = logger;
    }

    /// <summary>
    /// Builds the given targets of an already evaluated project in the shared build session.
    /// The results are applied to <paramref name="project"/> itself, so properties produced by the
    /// targets can be read from it afterwards.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Temporary unblock for dotnet/msbuild#14064 (MSBuild build APIs are now [RequiresUnreferencedCode]). dotnet CLI runs MSBuild in-proc (not trimmed). Remove when dotnet/sdk#55225 is fixed.")]
    public bool Build(ProjectInstance project, string[] targets)
    {
        lock (_lock)
        {
            BuildResult result = GetOrStartBuildManager().BuildRequest(new BuildRequestData(project, targets));
            return result.OverallResult == BuildResultCode.Success;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_buildManager is not { } buildManager)
            {
                return;
            }

            _buildManager = null;
            try
            {
                buildManager.EndBuild();
            }
            finally
            {
                buildManager.Dispose();
            }
        }
    }

    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Temporary unblock for dotnet/msbuild#14064 (MSBuild build APIs are now [RequiresUnreferencedCode]). dotnet CLI runs MSBuild in-proc (not trimmed). Remove when dotnet/sdk#55225 is fixed.")]
    private BuildManager GetOrStartBuildManager()
    {
        if (_buildManager is { } existing)
        {
            return existing;
        }

        // A dedicated build manager is used instead of BuildManager.DefaultBuildManager so that keeping
        // this session open never conflicts with the in-process MSBuild invocations the CLI makes to
        // build or restore the projects under test.
        var buildManager = new BuildManager("dotnet-test");
        var parameters = new BuildParameters(_projectCollection)
        {
            Loggers = _logger is null ? null : [_logger],
            // ProjectInstance.Build defaults to a single in-process node, keep that behavior.
            MaxNodeCount = 1,
            EnableNodeReuse = false,
        };

        if (_logger is not null)
        {
            // The facade logger forwards to a binary logger, which wants the full event stream.
            parameters.LogTaskInputs = true;
            parameters.EnableTargetOutputLogging = true;
        }

        buildManager.BeginBuild(parameters);
        _buildManager = buildManager;
        return buildManager;
    }
}
