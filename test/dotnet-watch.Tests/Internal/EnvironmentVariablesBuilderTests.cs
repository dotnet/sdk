// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class EnvironmentVariablesBuilderTests
    {
        [Fact]
        public void Value()
        {
            var builder = new EnvironmentVariablesBuilder();
            builder.DotNetStartupHookDirective.Add("a");
            builder.AspNetCoreHostingStartupAssembliesVariable.Add("b");

            var env = builder.GetEnvironment();
            AssertEx.SequenceEqual(
            [
                ("DOTNET_STARTUP_HOOKS", "a"),
                ("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "b")
            ], env);
        }

        [Fact]
        public void MultipleValues()
        {
            var builder = new EnvironmentVariablesBuilder();
            builder.DotNetStartupHookDirective.Add("a1");
            builder.DotNetStartupHookDirective.Add("a2");
            builder.AspNetCoreHostingStartupAssembliesVariable.Add("b1");
            builder.AspNetCoreHostingStartupAssembliesVariable.Add("b2");

            var env = builder.GetEnvironment();
            AssertEx.SequenceEqual(
            [
                ("DOTNET_STARTUP_HOOKS", $"a1{Path.PathSeparator}a2"),
                ("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "b1;b2")
            ], env);
        }
    }
}
