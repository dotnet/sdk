// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.DotNet.TemplateLocator
{
    public sealed class TemplateLocator: ITemplateLocator
    {
        public IReadOnlyCollection<IOptionalSdkTemplatePackageInfo> GetDotnetSdkTemplatePackages(string sdKversion)
        {
            throw new NotImplementedException();
        }

        public string DotnetSdkVersionUsedInVs()
        {
            throw new NotImplementedException();
        }
    }
}
