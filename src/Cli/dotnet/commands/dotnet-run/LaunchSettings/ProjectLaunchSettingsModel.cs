// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    public class ProjectLaunchSettingsModel
    {
        public string CommandLineArgs { get; set; }

        public bool LaunchBrowser { get; set; }

        public string LaunchUrl { get; set; }

        public string ApplicationUrl { get; set; }

        public string DotNetRunMessages { get; set; }

        public Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        ///<summary>
        /// If set, the project will be executed using the generated AppHost if one is available.
        /// This means the project will be run like `./myapp.exe &lt;args&gt;` instead of like `dotnet myapp.dll &lt;args&gt;`.
        ///</summary>
        public bool UseAppHostIfAvailable {get; set;} = true;
    }
}
