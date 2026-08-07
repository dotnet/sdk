// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.DotNet.HotReload;
using Moq;

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class HotReloadAgentTest
    {
        [Fact]
        public void ClearHotReloadEnvironmentVariables_ClearsStartupHook()
        {
            Assert.Equal("",
                HotReloadAgent.RemoveCurrentAssembly(typeof(StartupHook), typeof(StartupHook).Assembly.Location));
        }

        [Fact]
        public void ClearHotReloadEnvironmentVariables_PreservedOtherStartupHooks()
        {
            var customStartupHook = "/path/mycoolstartup.dll";
            Assert.Equal(customStartupHook,
                HotReloadAgent.RemoveCurrentAssembly(typeof(StartupHook), typeof(StartupHook).Assembly.Location + Path.PathSeparator + customStartupHook));
        }

        [PlatformSpecificFact(TestPlatforms.Windows)]
        public void ClearHotReloadEnvironmentVariables_RemovesHotReloadStartup_InCaseInvariantManner()
        {
            var customStartupHook = "/path/mycoolstartup.dll";
            Assert.Equal(customStartupHook,
                HotReloadAgent.RemoveCurrentAssembly(typeof(StartupHook), customStartupHook + Path.PathSeparator + typeof(StartupHook).Assembly.Location.ToUpperInvariant()));
        }

        [Fact]
        public void TopologicalSort_Works()
        {
            // Arrange
            var assembly1 = GetAssembly("System.Private.CoreLib", Array.Empty<AssemblyName>());
            var assembly2 = GetAssembly("System.Text.Json", new[] { new AssemblyName("System.Private.CoreLib"), });
            var assembly3 = GetAssembly("Microsoft.AspNetCore.Components", new[] { new AssemblyName("System.Private.CoreLib"), });
            var assembly4 = GetAssembly("Microsoft.AspNetCore.Components.Web", new[] { new AssemblyName("Microsoft.AspNetCore.Components"), new AssemblyName("System.Text.Json"), });

            var sortedList = MetadataUpdateHandlerInvoker.TopologicalSort(new[] { assembly2, assembly4, assembly1, assembly3 });

            // Assert
            Assert.Equal(new[] { assembly1, assembly2, assembly3, assembly4 }, sortedList);
        }

        [Fact]
        public void TopologicalSort_IgnoresUnknownReferencedAssemblies()
        {
            // Arrange
            var assembly1 = GetAssembly("System.Private.CoreLib", Array.Empty<AssemblyName>());
            var assembly2 = GetAssembly("System.Text.Json", new[] { new AssemblyName("netstandard"), new AssemblyName("System.Private.CoreLib"), });
            var assembly3 = GetAssembly("Microsoft.AspNetCore.Components", new[] { new AssemblyName("System.Private.CoreLib"), new AssemblyName("Microsoft.Extensions.DependencyInjection"), });
            var assembly4 = GetAssembly("Microsoft.AspNetCore.Components.Web", new[] { new AssemblyName("Microsoft.AspNetCore.Components"), new AssemblyName("System.Text.Json"), });

            var sortedList = MetadataUpdateHandlerInvoker.TopologicalSort(new[] { assembly2, assembly4, assembly1, assembly3 });

            // Assert
            Assert.Equal(new[] { assembly1, assembly2, assembly3, assembly4 }, sortedList);
        }

        [Fact]
        public void TopologicalSort_WithCycles()
        {
            // Arrange
            var assembly1 = GetAssembly("System.Private.CoreLib", Array.Empty<AssemblyName>());
            var assembly2 = GetAssembly("System.Text.Json", new[] { new AssemblyName("System.Collections.Immutable"), new AssemblyName("System.Private.CoreLib"), });
            var assembly3 = GetAssembly("System.Collections.Immutable", new[] { new AssemblyName("System.Text.Json"), new AssemblyName("System.Private.CoreLib"), });
            var assembly4 = GetAssembly("Microsoft.AspNetCore.Components", new[] { new AssemblyName("System.Private.CoreLib"), new AssemblyName("Microsoft.Extensions.DependencyInjection"), });
            var assembly5 = GetAssembly("Microsoft.AspNetCore.Components.Web", new[] { new AssemblyName("Microsoft.AspNetCore.Components"), new AssemblyName("System.Text.Json"), });

            var sortedList = MetadataUpdateHandlerInvoker.TopologicalSort(new[] { assembly2, assembly4, assembly1, assembly3, assembly5 });

            // Assert
            Assert.Equal(new[] { assembly1, assembly3, assembly2, assembly4, assembly5 }, sortedList);
        }

        [Theory]
        [InlineData(typeof(HandlerWithClearCache))]
        [InlineData(typeof(HandlerWithUpdateApplication))]
        [InlineData(typeof(HandlerWithUpdateContent))]
        public void GetHandlerActions_SingleAction(Type handlerType)
        {
            var reporter = new AgentReporter();
            var invoker = new MetadataUpdateHandlerInvoker(reporter);
            var actions = invoker.GetUpdateHandlerActions([handlerType]);

            Assert.Empty(reporter.GetAndClearLogEntries(ResponseLoggingLevel.Verbose));

            if (handlerType == typeof(HandlerWithUpdateContent))
            {
                Assert.Single(actions.UpdateContentHandlers);
                Assert.Empty(actions.ClearCacheHandlers);
                Assert.Empty(actions.UpdateApplicationHandlers);
            }
            else if (handlerType == typeof(HandlerWithUpdateApplication))
            {
                Assert.Single(actions.UpdateApplicationHandlers);
                Assert.Empty(actions.ClearCacheHandlers);
                Assert.Empty(actions.UpdateContentHandlers);
            }
            else if (handlerType == typeof(HandlerWithClearCache))
            {
                Assert.Single(actions.ClearCacheHandlers);
                Assert.Empty(actions.UpdateContentHandlers);
                Assert.Empty(actions.UpdateApplicationHandlers);
            }
        }

        [Fact]
        public void GetHandlerActions_DiscoversActionsOnTypeWithAllActions()
        {
            var reporter = new AgentReporter();
            var invoker = new MetadataUpdateHandlerInvoker(reporter);
            var actions = invoker.GetUpdateHandlerActions([typeof(HandlerWithAllActions)]);

            AssertEx.Empty(reporter.GetAndClearLogEntries(ResponseLoggingLevel.Verbose));
            Assert.Equal(typeof(HandlerWithAllActions).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic), actions.ClearCacheHandlers.Single().Method);
            Assert.Equal(typeof(HandlerWithAllActions).GetMethod("UpdateApplication", BindingFlags.Static | BindingFlags.NonPublic), actions.UpdateApplicationHandlers.Single().Method);
            Assert.Equal(typeof(HandlerWithAllActions).GetMethod("UpdateContent", BindingFlags.Static | BindingFlags.NonPublic), actions.UpdateContentHandlers.Single().Method);
        }

        [Fact]
        public void GetHandlerActions_LogsMessageIfMethodHasIncorrectSignature()
        {
            var reporter = new AgentReporter();
            var invoker = new MetadataUpdateHandlerInvoker(reporter);

            var handlerType = typeof(HandlerWithIncorrectSignature);
            var actions = invoker.GetUpdateHandlerActions([handlerType]);

            var log = reporter.GetAndClearLogEntries(ResponseLoggingLevel.WarningsAndErrors);
            AssertEx.SequenceEqual(
            [
                $"Warning: Type '{handlerType}' has method 'Void ClearCache()' that does not match the required signature.",
                $"Warning: Type '{handlerType}' has method 'Void UpdateContent()' that does not match the required signature."
            ],
            log.Select(e => $"{e.severity}: {e.message}"));

            Assert.Empty(actions.ClearCacheHandlers);
            Assert.Empty(actions.UpdateContentHandlers);
            Assert.Single(actions.UpdateApplicationHandlers);
        }

        [Fact]
        public void GetHandlerActions_LogsMessageIfNoActionsAreDiscovered()
        {
            var reporter = new AgentReporter();
            var invoker = new MetadataUpdateHandlerInvoker(reporter);

            var handlerType = typeof(HandlerWithNoActions);
            var actions = invoker.GetUpdateHandlerActions([handlerType]);

            var log = reporter.GetAndClearLogEntries(ResponseLoggingLevel.WarningsAndErrors);
            var logEntry = Assert.Single(log);
            Assert.Equal(
                $"Expected to find a static method 'ClearCache', 'UpdateApplication' or 'UpdateContent' on type '{handlerType.AssemblyQualifiedName}' but neither exists.", logEntry.message);

            Assert.Equal(AgentMessageSeverity.Warning, logEntry.severity);
            Assert.Empty(actions.ClearCacheHandlers);
            Assert.Empty(actions.UpdateApplicationHandlers);
        }

        private static Assembly GetAssembly(string fullName, AssemblyName[] dependencies)
        {
            var assembly = new Mock<Assembly>();
            assembly.Setup(a => a.GetName()).Returns(new AssemblyName(fullName));
            assembly.SetupGet(a => a.FullName).Returns(fullName);
            assembly.Setup(a => a.GetReferencedAssemblies()).Returns(dependencies);
            assembly.Setup(a => a.ToString()).Returns(fullName);
            return assembly.Object;
        }

        private class HandlerWithClearCache
        {
            internal static void ClearCache(Type[]? _) { }
        }

        private class HandlerWithUpdateApplication
        {
            internal static void UpdateApplication(Type[]? _) { }
        }

        private class HandlerWithUpdateContent
        {
            public static void UpdateContent(string assemblyName, bool isApplicationProject, string relativePath, byte[] contents) { }
        }

        private class HandlerWithAllActions
        {
            internal static void ClearCache(Type[]? _) { }
            internal static void UpdateApplication(Type[]? _) { }
            internal static void UpdateContent(string assemblyName, bool isApplicationProject, string relativePath, byte[] contents) { }
        }

        private class HandlerWithIncorrectSignature
        {
            internal static void ClearCache() { }   
            internal static void UpdateContent() { }
            internal static void UpdateApplication(Type[]? _) { }
        }

        private class HandlerWithNoActions
        {
            internal static void SomeMethod() { }
        }
    }
}
