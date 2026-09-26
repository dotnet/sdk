// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Runs every MSBuild target a single CLI command needs to invoke on top of the build - for example
/// <c>Restore</c>, <c>ComputeAvailableDevices</c>, <c>DeployToDevice</c> and <c>ComputeRunArguments</c> -
/// inside one MSBuild build session, so the binary log behind <c>-bl</c> holds a single well formed build.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ProjectInstance.Build(string[], IEnumerable{ILogger})"/> runs a complete MSBuild build
/// (begin/request/end) on every call. Each of those builds creates its own logging service, so the
/// <see cref="BuildEventContext"/> ids it hands out - project context ids, target ids, task ids -
/// restart from zero, and every build emits its own build-started/build-finished pair.
/// Attaching the same binary logger to several of those builds, which is what <c>dotnet test -bl</c>
/// does when a run contains more than one test project or performs device selection, produces a binlog
/// holding several builds with colliding ids: readers attribute every project's targets to whichever
/// project claimed the id first, and the other projects appear to have executed no targets at all.
/// The underlying engine limitation is tracked by
/// <see href="https://github.com/dotnet/msbuild/issues/14609"/>.
/// </para>
/// <para>
/// Sending all the requests to a single <see cref="BuildManager"/> session instead yields one build
/// with unique ids, so the binlog is well formed and shows the targets executed for each project.
/// MSBuild requires every project instance built in one session to come from the same
/// <see cref="Evaluation.ProjectCollection"/>, so the session owns the collection every project of the
/// command has to be evaluated in - see <see cref="ProjectCollection"/>.
/// </para>
/// <para>
/// The session is started lazily and owns a dedicated <see cref="BuildManager"/> rather than using
/// <see cref="BuildManager.DefaultBuildManager"/>, so it can stay open across the in-process MSBuild
/// invocations the CLI makes to build or restore the projects it is working on (which go through the
/// default build manager and write their own binary log).
/// </para>
/// </remarks>
internal sealed class MSBuildSession : IDisposable
{
    private readonly MSBuildArgs _msbuildArgs;
    private readonly FacadeLogger? _logger;
    private readonly Lock _lock = new();

    private BuildManager? _buildManager;
    private bool _disposed;

    public MSBuildSession(MSBuildArgs msbuildArgs, FacadeLogger? logger)
    {
        _msbuildArgs = msbuildArgs;
        _logger = logger;
        using var _ = MSBuildForwardingAppWithoutLogging.SetMSBuildRequiredEnvironmentVariables();

        // Deliberately without global properties: MSBuild merges the global properties of a collection
        // into every project loaded from it, and the projects of a command do not all want the same ones
        // (restore, for instance, must run without the TargetFramework the rest of the command uses -
        // see https://github.com/dotnet/sdk/issues/53488). Every project passes its own instead.
        ProjectCollection = new ProjectCollection(
            globalProperties: null,
            loggers: logger is null ? null : [logger],
            toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);
    }

    /// <summary>
    /// The collection every project built in this session must be evaluated in. MSBuild rejects a build
    /// whose submissions mix project instances coming from different collections ("All build submissions
    /// in a build must use project instances originating from the same project collection.").
    /// </summary>
    /// <remarks>
    /// The collection carries no global properties of its own: each project passes the ones it needs -
    /// its target framework, device or runtime identifier - when it is evaluated.
    /// </remarks>
    public ProjectCollection ProjectCollection { get; }

    /// <summary>
    /// Builds the given targets of an already evaluated project in the shared build session.
    /// The results are applied to <paramref name="project"/> itself, so properties produced by the
    /// targets can be read from it afterwards.
    /// </summary>
    public bool Build(ProjectInstance project, string[] targets)
        => Build(project, targets, out _);

    /// <inheritdoc cref="Build(ProjectInstance, string[])"/>
    /// <param name="targetOutputs">The outputs of the requested targets.</param>
    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Temporary unblock for dotnet/msbuild#14064 (MSBuild build APIs are now [RequiresUnreferencedCode]). dotnet CLI runs MSBuild in-proc (not trimmed). Remove when dotnet/sdk#55225 is fixed.")]
    public bool Build(ProjectInstance project, string[] targets, out IDictionary<string, TargetResult> targetOutputs)
    {
        // The MSBuild build APIs cannot be called in parallel, even for different projects:
        // BuildManager throws "The operation cannot be completed because a build is already in progress."
        // Every project of the command shares this session, so the requests are serialized here.
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var _ = MSBuildForwardingAppWithoutLogging.SetMSBuildRequiredEnvironmentVariables();

            // Every ProjectInstance.Build call used to be its own build, which meant a fresh results
            // cache each time. Within a single session MSBuild would instead serve a second request
            // for the same project from the results cached by the first one, so targets shared by
            // DeployToDevice and ComputeRunArguments would silently stop re-running.
            // ReplaceExistingProjectInstance re-points the configuration at this instance and clears
            // its cached results, which keeps the previous per-request behavior.
            var requestData = new BuildRequestData(
                project,
                targets,
                hostServices: null,
                BuildRequestDataFlags.ReplaceExistingProjectInstance);

            BuildResult result = GetOrStartBuildManager().BuildRequest(requestData);
            targetOutputs = result.ResultsByTarget;
            return result.OverallResult == BuildResultCode.Success;
        }
    }

    /// <summary>
    /// Ends the session, surfacing any failure MSBuild captured during it.
    /// </summary>
    /// <remarks>
    /// Call this on the success path. Unlike <see cref="ProjectInstance.Build(string[], IEnumerable{ILogger})"/>,
    /// which drains the engine's captured thread exception into the <see cref="BuildResult"/> it
    /// returns, the public <see cref="BuildManager.BuildRequest"/> leaves it in place and only
    /// <see cref="BuildManager.EndBuild"/> rethrows it. A failure that lands between requests or
    /// while the logging service is flushing - a binary logger failing to write, for example - is
    /// therefore reported here and nowhere else, so it must not be swallowed when the run otherwise
    /// succeeded.
    /// </remarks>
    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Temporary unblock for dotnet/msbuild#14064 (MSBuild build APIs are now [RequiresUnreferencedCode]). dotnet CLI runs MSBuild in-proc (not trimmed). Remove when dotnet/sdk#55225 is fixed.")]
    public void Complete()
    {
        lock (_lock)
        {
            if (TakeBuildManager() is not { } buildManager)
            {
                return;
            }

            try
            {
                buildManager.EndBuild();
            }
            finally
            {
                buildManager.Dispose();
                ProjectCollection.Dispose();
            }
        }
    }

    /// <summary>
    /// Ends the session without reporting failures, for the case where the run is already failing.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (TakeBuildManager() is not { } buildManager)
            {
                return;
            }

            try
            {
                // Reaching Dispose without Complete means an exception is unwinding through the
                // enclosing 'using'. EndBuild rethrows whatever the engine captured, which would
                // replace that exception - typically the GracefulException naming the project that
                // failed - with a raw MSBuild stack trace, so it is only logged here.
                buildManager.EndBuild();
            }
            catch (Exception ex)
            {
                Reporter.Verbose.WriteLine($"Failed to end the MSBuild session: {ex}");
            }
            finally
            {
                buildManager.Dispose();
                ProjectCollection.Dispose();
            }
        }
    }

    /// <summary>
    /// Marks the session finished and hands back the build manager to close, or <see langword="null"/>
    /// if it has already been closed. The collection is released even when the session was never
    /// started, because the projects of the command are evaluated in it whether or not a target ran.
    /// </summary>
    private BuildManager? TakeBuildManager()
    {
        if (_disposed)
        {
            return null;
        }

        _disposed = true;

        BuildManager? buildManager = _buildManager;
        _buildManager = null;

        if (buildManager is null)
        {
            ProjectCollection.Dispose();
        }

        return buildManager;
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
        // build or restore the projects it is working on.
        var buildManager = new BuildManager("dotnet-cli-session");
        try
        {
            var parameters = new BuildParameters(ProjectCollection)
            {
                Loggers = CreateLoggers(),
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
        }
        catch
        {
            buildManager.Dispose();
            throw;
        }

        _buildManager = buildManager;
        return buildManager;
    }

    /// <summary>
    /// Gets the loggers to attach to the session.
    /// </summary>
    /// <remarks>
    /// A console logger is attached (unless <c>-noConsoleLogger</c> was passed) so that MSBuild errors are
    /// actually reported to the user: the binary logger only forwards events to binlogs, and it is only
    /// created when <c>-bl</c> was passed, so without a console logger these builds fail silently and the user
    /// is only told to "fix the errors and warnings" without any error being printed anywhere.
    /// This mirrors what <c>dotnet run</c> does in <c>RunCommand.InvokeRunArgumentsTarget</c>.
    /// The loggers are created once per session rather than once per build, because a session
    /// initializes and shuts down its loggers exactly once.
    /// </remarks>
    private List<ILogger> CreateLoggers()
    {
        var loggers = new List<ILogger>(capacity: 2);

        if (_logger is not null)
        {
            loggers.Add(_logger);
        }

        if (!LoggerUtility.HasNoConsoleLoggerArgument(_msbuildArgs.OtherMSBuildArgs))
        {
            // These builds only select devices, deploy and compute run arguments, so keep them quiet -
            // at this verbosity MSBuild still reports errors and warnings.
            loggers.Add(CommonRunHelpers.GetConsoleLogger(
                _msbuildArgs.CloneWithExplicitArgs([$"--verbosity:{LoggerVerbosity.Quiet.ToString().ToLowerInvariant()}", .. _msbuildArgs.OtherMSBuildArgs])));
        }

        return loggers;
    }
}
