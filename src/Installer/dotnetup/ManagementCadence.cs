// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    public enum ManagementCadenceType
    {
        Dotnetup,
        GlobalJson,
        Standalone
    }

    public struct ManagementCadence
    {
        public ManagementCadence()
        {
            Type = ManagementCadenceType.Dotnetup;
            Metadata = new Dictionary<string, string>();
        }
        public ManagementCadence(ManagementCadenceType managementStyle) : this()
        {
            Type = managementStyle;
            Metadata = [];
        }

        public ManagementCadenceType Type { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }
}
