// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.LaunchSettings
{
    public class ProjectLaunchSettingsModel
    {
        /// <summary>
        /// The arguments to pass to the target being run.
        /// </summary>
        public string CommandLineArgs { get; set; }

        /// <summary>
        /// Whether or not to launched a browser.
        /// </summary>
        public bool LaunchBrowser { get; set; }

        /// <summary>
        /// The relative URL to launch in the browser.
        /// </summary>
        public string LaunchUrl { get; set; }

        /// <summary>
        /// A semi-colon delimited list of URL(s) to configure for the web server.
        /// </summary>
        public string ApplicationUrl { get; set; }

        public string DotNetRunMessages { get; set; }

        /// <summary>
        /// Environment variables to set.
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
