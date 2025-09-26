// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    internal interface IInstallationValidator
    {
        bool Validate(DotnetInstall install);
    }
}
