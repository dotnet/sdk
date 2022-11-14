// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.VirtualMonoRepo.Tasks;

internal class RemoteFactory : IRemoteFactory
{
    private readonly IProcessManager _processManager;
    private readonly IVersionDetailsParser _versionDetailsParser;
    private readonly string _tmpPath;

    public RemoteFactory(IProcessManager processManager, IVersionDetailsParser versionDetailsParser, string tmpPath)
    {
        _processManager = processManager;
        _versionDetailsParser = versionDetailsParser;
        _tmpPath = tmpPath;
    }

    public Task<IRemote> GetBarOnlyRemoteAsync(ILogger logger)
    {
        throw new NotImplementedException();
    }

    public Task<IRemote> GetRemoteAsync(string repoUrl, ILogger logger)
    {
        var githubClient = new DarcLib.GitHubClient(
            _processManager.GitExecutable,
            accessToken: null,
            logger,
            _tmpPath,
            cache: null);

        IRemote remote = new Remote(githubClient, barClient: null, _versionDetailsParser, logger);
        return Task.FromResult(remote);
    }
}
