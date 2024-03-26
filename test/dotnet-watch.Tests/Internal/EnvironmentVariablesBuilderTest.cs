// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Internal
{
    public class EnvironmentVariablesBuilderTest
    {
        [Fact]
        public void AddToEnvironment_Value()
        {
            var builder = new EnvironmentVariablesBuilder();
            builder.DotNetStartupHooks.Add("a");
            builder.AspNetCoreHostingStartupAssemblies.Add("b");

            var values = new Dictionary<string, string>();
            builder.AddToEnvironment(values);
            Assert.Equal("a", values["DOTNET_STARTUP_HOOKS"]);
            Assert.Equal("b", values["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"]);
        }

        [Fact]
        public void AddToEnvironment_MultipleValues()
        {
            var builder = new EnvironmentVariablesBuilder();
            builder.DotNetStartupHooks.Add("a1");
            builder.DotNetStartupHooks.Add("a2");
            builder.AspNetCoreHostingStartupAssemblies.Add("b1");
            builder.AspNetCoreHostingStartupAssemblies.Add("b2");

            var values = new Dictionary<string, string>();
            builder.AddToEnvironment(values);
            Assert.Equal("a1" + Path.PathSeparator + "a2", values["DOTNET_STARTUP_HOOKS"]);
            Assert.Equal("b1;b2", values["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"]);
        }

        [Fact]
        public void AddToEnvironment_ExistingHook()
        {
            var builder = new EnvironmentVariablesBuilder();
            builder.DotNetStartupHooks.Add("value1");

            var values = new Dictionary<string, string>()
            {
                ["DOTNET_STARTUP_HOOKS"] = "value3"
            };

            // variables shouild only be set via builder:
            Assert.Throws<ArgumentException>(() => builder.AddToEnvironment(values));
        }

        [Fact]
        public void AddToEnvironment_ExistingHostingStartupAssembly()
        {
            var builder = new EnvironmentVariablesBuilder();
            builder.DotNetStartupHooks.Add("value1");

            var values = new Dictionary<string, string>()
            {
                ["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] = "value3"
            };

            // variables shouild only be set via builder:
            Assert.Throws<ArgumentException>(() => builder.AddToEnvironment(values));
        }
    }
}
