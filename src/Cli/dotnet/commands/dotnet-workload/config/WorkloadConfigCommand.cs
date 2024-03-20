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
        bool _hasUpdateMode;
        string? _updateMode;
        readonly IWorkloadResolverFactory _workloadResolverFactory;

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
            //  TODO: Is it possible to check the order of the options?  This would allow us to print the values out in the same order they are specified on the command line
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
            if (_hasUpdateMode)
            {
                if (_updateMode == WorkloadConfigCommandParser.UpdateMode_WorkloadSet)
                {
                    _workloadInstaller.UpdateInstallMode(_sdkFeatureBand, true);
                }
                else if (_updateMode == WorkloadConfigCommandParser.UpdateMode_Manifests)
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
