// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class EnvironmentVariablesBuilderTest
    {
        [Fact]
        public void Value()
        {
            var builder = new EnvironmentVariablesBuilder();
            builder.DotNetStartupHookDirective.Add("a");
            builder.AspNetCoreHostingStartupAssembliesVariable.Add("b");

            var values = new Dictionary<string, string>();
            builder.AddToEnvironment(values);
            AssertEx.SequenceEqual(["[env:DOTNET_STARTUP_HOOKS=a]"], builder.GetCommandLineDirectives());
            AssertEx.SequenceEqual([("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "b")], values.Select(e => (e.Key, e.Value)));
        }

        [Fact]
        public void MultipleValues()
        {
            var builder = new EnvironmentVariablesBuilder();
            builder.DotNetStartupHookDirective.Add("a1");
            builder.DotNetStartupHookDirective.Add("a2");
            builder.AspNetCoreHostingStartupAssembliesVariable.Add("b1");
            builder.AspNetCoreHostingStartupAssembliesVariable.Add("b2");

            var values = new Dictionary<string, string>();
            builder.AddToEnvironment(values);
            AssertEx.SequenceEqual([$"[env:DOTNET_STARTUP_HOOKS=a1{Path.PathSeparator}a2]"], builder.GetCommandLineDirectives());
            AssertEx.SequenceEqual([("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "b1;b2")], values.Select(e => (e.Key, e.Value)));
        }
    }
}
