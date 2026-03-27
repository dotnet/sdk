// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Build.Tasks
{
    internal class FrameworkReferenceResolver
    {
        private readonly Func<string, string> _getEnvironmentVariable;

        public FrameworkReferenceResolver(Func<string, string> getEnvironmentVariable)
        {
            _getEnvironmentVariable = getEnvironmentVariable;
        }

        public string GetDefaultReferenceAssembliesPath()
        {
            // Read DOTNET_REFERENCE_ASSEMBLIES_PATH through the injected delegate
            // instead of DotNetReferenceAssembliesPathResolver.Resolve(), which uses
            // process-global Environment.GetEnvironmentVariable and bypasses TaskEnvironment.
            var referenceAssembliesPath = _getEnvironmentVariable("DOTNET_REFERENCE_ASSEMBLIES_PATH");

            if (!string.IsNullOrEmpty(referenceAssembliesPath))
            {
                return referenceAssembliesPath;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // There is no reference assemblies path outside of windows
                // The environment variable can be used to specify one
                return null;
            }

            // References assemblies are in %ProgramFiles(x86)% on
            // 64 bit machines
            var programFiles = _getEnvironmentVariable("ProgramFiles(x86)");

            if (string.IsNullOrEmpty(programFiles))
            {
                // On 32 bit machines they are in %ProgramFiles%
                programFiles = _getEnvironmentVariable("ProgramFiles");
            }

            if (string.IsNullOrEmpty(programFiles))
            {
                // Reference assemblies aren't installed
                return null;
            }

            return Path.Combine(
                    programFiles,
                    "Reference Assemblies", "Microsoft", "Framework");
        }
    }
}
