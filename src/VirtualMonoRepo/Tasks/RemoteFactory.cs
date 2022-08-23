// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.DarcLib.Helpers;

namespace Microsoft.DotNet.VirtualMonoRepo.Tasks;

internal class RemoteFactory : IRemoteFactory
{
    private readonly IProcessManager _processManager;
    private readonly string _tmpPath;

    public RemoteFactory(IProcessManager processManager, string tmpPath)
    {
        _processManager = processManager;
        _tmpPath = tmpPath;
    }

    public Task<IRemote> GetBarOnlyRemoteAsync(Extensions.Logging.ILogger logger)
    {
        throw new NotImplementedException();
    }

    public Task<IRemote> GetRemoteAsync(string repoUrl, Extensions.Logging.ILogger logger)
    {
        var githubClient = new DarcLib.GitHubClient(
            _processManager.GitExecutable,
            accessToken: null,
            logger,
            _tmpPath,
            cache: null);

        return System.Threading.Tasks.Task.FromResult<IRemote>(new Remote(githubClient, barClient: null, logger));
    }
}
