// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using DotnetCommand = Microsoft.DotNet.Tools.Test.Utilities.DotnetCommand;
using TestBase = Microsoft.DotNet.Tools.Test.Utilities.TestBase;
using RequiresAspNetCore = Microsoft.DotNet.Tools.Test.Utilities.RequiresAspNetCore;

namespace EndToEnd
{
    public class GivenDotnetUsesDotnetTools : TestBase
    {
        [RequiresAspNetCore]
        public void ThenOneDotnetToolsCanBeCalled() => new DotnetCommand()
                .ExecuteWithCapturedOutput("dev-certs --help")
                .Should().Pass();
    }
}
