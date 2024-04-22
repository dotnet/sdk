﻿using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;

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
