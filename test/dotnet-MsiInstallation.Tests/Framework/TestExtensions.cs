// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    public static class TestExtensions
    {
        public static AndConstraint<CommandResultAssertions> PassWithoutWarning(this CommandResultAssertions assertions)
        {
            return assertions.Pass()
                .And.NotHaveStdOutContaining("Warning: ")
                .And.NotHaveStdErrContaining("Warning: ");
        }
    }
}
