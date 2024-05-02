// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Tests.Utilities
{
    //  This class is for frameworks that aren't yet in NuGet's FrameworkConstants.CommonFrameworks class
    public static class NuGetFrameworks
    {
        public static readonly NuGetFramework NetCoreApp21
                = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, new Version(2, 1, 0, 0));

        public static readonly NuGetFramework NetCoreApp22
                = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, new Version(2, 2, 0, 0));
    }
}
