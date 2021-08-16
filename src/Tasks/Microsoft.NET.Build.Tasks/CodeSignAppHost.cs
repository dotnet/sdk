// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Sign the app binary using codesign with an anonymous certificate.
    /// </summary>
    public class CodeSignAppHost : TaskBase
    {
        private const string CodeSignPath = @"/usr/bin/codesign";

        [Required]
        public string AppHostPath { get; set; }

        protected override void ExecuteCore()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return;
            }

            if (!File.Exists(AppHostPath))
            {
                return;
            }

            var psi = new ProcessStartInfo()
            {
                Arguments = $"-s - \"{AppHostPath}\"",
                FileName = CodeSignPath,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    throw new BuildErrorException(Strings.AppHostSigningFailed, p.StandardError.ReadToEnd(), p.ExitCode.ToString());
                }
            }
        }
    }
}
