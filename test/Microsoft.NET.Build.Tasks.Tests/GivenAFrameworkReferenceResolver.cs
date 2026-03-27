// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAFrameworkReferenceResolver
    {
        [Fact]
        public void ItReadsReferenceAssembliesPathThroughInjectedDelegate()
        {
            var envVarsRead = new List<string>();
            var resolver = new FrameworkReferenceResolver(name =>
            {
                envVarsRead.Add(name);
                return name == "DOTNET_REFERENCE_ASSEMBLIES_PATH" ? "/custom/ref/path" : null;
            });

            var result = resolver.GetDefaultReferenceAssembliesPath();

            result.Should().Be("/custom/ref/path");
            envVarsRead.Should().Contain("DOTNET_REFERENCE_ASSEMBLIES_PATH",
                "env var should be read through the injected delegate, not process-global Environment");
        }

        [Fact]
        public void ItFallsToProgramFilesWhenReferenceAssembliesPathNotSet()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On non-Windows, it returns null when env var is not set
                var resolver = new FrameworkReferenceResolver(_ => null);
                resolver.GetDefaultReferenceAssembliesPath().Should().BeNull();
                return;
            }

            var resolver2 = new FrameworkReferenceResolver(name =>
                name == "ProgramFiles(x86)" ? @"C:\Program Files (x86)" : null);

            var result = resolver2.GetDefaultReferenceAssembliesPath();

            result.Should().Be(@"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework");
        }

        [Fact]
        public void ItDoesNotCallProcessGlobalEnvironment()
        {
            // Set a process-level env var that should NOT be seen by the resolver
            // if it correctly uses the injected delegate
            var originalValue = Environment.GetEnvironmentVariable("DOTNET_REFERENCE_ASSEMBLIES_PATH");
            try
            {
                Environment.SetEnvironmentVariable("DOTNET_REFERENCE_ASSEMBLIES_PATH", "/process-global-path");

                // Inject a delegate that returns a DIFFERENT value
                var resolver = new FrameworkReferenceResolver(name =>
                    name == "DOTNET_REFERENCE_ASSEMBLIES_PATH" ? "/injected-path" : null);

                var result = resolver.GetDefaultReferenceAssembliesPath();

                result.Should().Be("/injected-path",
                    "resolver should use injected delegate, not process-global Environment.GetEnvironmentVariable");
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_REFERENCE_ASSEMBLIES_PATH", originalValue);
            }
        }
    }
}
