// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    public class UpdateChannel
    {
        public string Name { get; set; }

        public UpdateChannel(string name)
        {
            Name = name;
        }

        public bool IsFullySpecifiedVersion()
        {
            return ReleaseVersion.TryParse(Name, out _);
        }

    }
}
