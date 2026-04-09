// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;

#nullable enable

namespace Microsoft.DotNet.Workloads.Workload.Config
{
    internal class WorkloadConfigCommand : WorkloadCommandBase
    {
        private bool _hasUpdateMode;
        private string? _updateMode;
        private readonly IWorkloadResolverFactory _workloadResolverFactory;

        private string? _dotnetPath;
        private string _userProfileDir;
        private readonly IWorkloadResolver _workloadResolver;
        private readonly ReleaseVersion _sdkVersion;
        private readonly SdkFeatureBand _sdkFeatureBand;

        readonly IInstaller _workloadInstaller;

        public WorkloadConfigCommand(
            ParseResult parseResult,
            IReporter? reporter = null,
            IWorkloadResolverFactory? workloadResolverFactory = null
        ) : base(parseResult, CommonOptions.HiddenVerbosityOption, reporter)
        {
            _hasUpdateMode = parseResult.HasOption(WorkloadConfigCommandParser.UpdateMode);
            _updateMode = parseResult.GetValue(WorkloadConfigCommandParser.UpdateMode);

            _workloadResolverFactory = workloadResolverFactory ?? new WorkloadResolverFactory();

            var creationResult = _workloadResolverFactory.Create();

            _dotnetPath = creationResult.DotnetPath;
            _userProfileDir = creationResult.UserProfileDir;
            _workloadResolver = creationResult.WorkloadResolver;
            _sdkVersion = creationResult.SdkVersion;

            _sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _workloadInstaller = WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, _sdkFeatureBand, creationResult.WorkloadResolver, Verbosity, creationResult.UserProfileDir, VerifySignatures, PackageDownloader, creationResult.DotnetPath);
        }

        public override int Execute()
        {
            //  When we support multiple configuration values, it would be nice if we could process and display them in the order they are passed.
            //  It seems that the parser doesn't give us a good way to do that, however
            if (_hasUpdateMode)
            {
                if (WorkloadConfigCommandParser.UpdateMode_WorkloadSet.Equals(_updateMode, StringComparison.InvariantCultureIgnoreCase))
                {
                    _workloadInstaller.UpdateInstallMode(_sdkFeatureBand, true);
                }
                else if (WorkloadConfigCommandParser.UpdateMode_Manifests.Equals(_updateMode, StringComparison.InvariantCultureIgnoreCase))
                {
                    _workloadInstaller.UpdateInstallMode(_sdkFeatureBand, false);
                }
                else if (string.IsNullOrEmpty(_updateMode))
                {
                    if (InstallingWorkloadCommand.ShouldUseWorkloadSetMode(_sdkFeatureBand, _dotnetPath))
                    {
                        Reporter.WriteLine(WorkloadConfigCommandParser.UpdateMode_WorkloadSet);
                    }
                    else
                    {
                        Reporter.WriteLine(WorkloadConfigCommandParser.UpdateMode_Manifests);
                    }
                }
                else
                {
                    //  This should not be hit, as parser sets the accepted values and should error before getting here if the value is not valid
                    throw new InvalidOperationException($"Invalid update mode: {_updateMode}");
                }
            }
            else
            {
                _parseResult.ShowHelp();
            }

            return 0;
        }
    }
    
}
