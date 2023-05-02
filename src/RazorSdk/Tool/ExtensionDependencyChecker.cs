﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal abstract class ExtensionDependencyChecker
    {
        public abstract bool Check(IEnumerable<string> extensionFilePaths);
    }
}
