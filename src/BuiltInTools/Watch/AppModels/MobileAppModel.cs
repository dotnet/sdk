// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class MobileAppModel(DotNetWatchContext context, ProjectGraphNode project) : HotReloadAppModel
{
    public override ValueTask<HotReloadClients?> TryCreateClientsAsync(ILogger clientLogger, ILogger agentLogger, CancellationToken cancellationToken)
        // Use HTTP transport for mobile platforms (Android, iOS, MacCatalyst)
        // Named pipes don't work over the network for remote device scenarios
        => new(new HotReloadClients(new MobileHotReloadClient(clientLogger, agentLogger, context.EnvironmentOptions.HotReloadHttpPort), browserRefreshServer: null));

    public override async ValueTask<bool> DeployAgent(ILogger clientLogger, IReadOnlyDictionary<string, string> environment)
    {
        clientLogger.LogDebug("Deploying Hot Reload agent to the device.");

        // Deep copy so that we don't pollute the project graph:
        var instance = project.ProjectInstance.DeepCopy();

        instance.SetProperty(PropertyNames.DotNetHotReloadAgentStartupHook, GetStartupHookPath(project));

        foreach (var (name, value) in environment)
        {
            instance.AddItem(PropertyNames.DotNetHotReloadAgentEnvironment, name, [new(MetadataNames.Value, value)]);
        }

        var buildReporter = new BuildReporter(context.BuildLogger, context.Options, context.EnvironmentOptions);
        using var loggers = buildReporter.GetLoggers(project.ProjectInstance.FullPath, operationName: "DeployAgent");

        if (!instance.Build(TargetNames.DeployHotReloadAgentConfiguration, loggers))
        {
            loggers.ReportOutput();
            return false;
        }

        return true;
    }
}
