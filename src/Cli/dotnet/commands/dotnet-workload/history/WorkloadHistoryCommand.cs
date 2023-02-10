// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.CommandLine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Workloads.Workload.History
{
    internal class WorkloadHistoryCommand : WorkloadCommandBase
    {
        public WorkloadHistoryCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            string tempDirPath = null,
            INuGetPackageDownloader nugetPackageDownloader = null
        ) : base(parseResult, CommonOptions.HiddenVerbosityOption, reporter, tempDirPath, nugetPackageDownloader)
        {
        }

        public override int Execute()
        {
            Reporter.WriteLine("Workload history not yet implemented.");
            return 0;
        }
       
    }
}
