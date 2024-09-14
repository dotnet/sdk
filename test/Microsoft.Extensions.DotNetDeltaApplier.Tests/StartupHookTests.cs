// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.DotNetDeltaApplier
{
    public class StartupHookTests
    {
        [Fact]
        public void ClearHotReloadEnvironmentVariables_ClearsStartupHook()
        {
            Assert.Equal("", StartupHook.RemoveCurrentAssembly(typeof(StartupHook).Assembly.Location));
        }

        [Fact]
        public void ClearHotReloadEnvironmentVariables_PreservedOtherStartupHooks()
        {
            var customStartupHook = "/path/mycoolstartup.dll";
            Assert.Equal(customStartupHook, StartupHook.RemoveCurrentAssembly(typeof(StartupHook).Assembly.Location + Path.PathSeparator + customStartupHook));
        }

        [Fact]
        public void ClearHotReloadEnvironmentVariables_RemovesHotReloadStartup_InCaseInvariantManner()
        {
            var customStartupHook = "/path/mycoolstartup.dll";
            Assert.Equal(customStartupHook, StartupHook.RemoveCurrentAssembly(customStartupHook + Path.PathSeparator + typeof(StartupHook).Assembly.Location.ToUpperInvariant()));
        }
    }
}
