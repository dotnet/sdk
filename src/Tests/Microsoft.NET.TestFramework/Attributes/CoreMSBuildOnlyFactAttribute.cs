// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.PlatformAbstractions;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class CoreMSBuildOnlyFactAttribute : FactAttribute
    {
        public CoreMSBuildOnlyFactAttribute()
        {
            this.Skip = "To debug";
        }
    }
}
