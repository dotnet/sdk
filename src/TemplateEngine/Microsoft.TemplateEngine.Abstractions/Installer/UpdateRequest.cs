// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions.TemplatePackages;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    public class UpdateRequest
    {
        public IManagedTemplatePackage Source { get; set; }

        public string Version { get; set; }

        public static UpdateRequest FromCheckUpdateResult (CheckUpdateResult request)
        {
            _ = request ?? throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.LatestVersion))
            {
                throw new ArgumentException("Version cannot be null or empty", nameof(request));
            }

            return new UpdateRequest()
            {
                Source = request.Source,
                Version = request.LatestVersion
            };
        }
    }
}
