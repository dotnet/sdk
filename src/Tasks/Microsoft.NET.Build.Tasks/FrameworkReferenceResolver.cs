// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.DependencyModel.Resolution;

namespace Microsoft.NET.Build.Tasks
{
    internal class FrameworkReferenceResolver
    {
        private readonly Func<string, string> _getEnvironmentVariable;

        /// <summary>
        /// Creates an instance that reads environment variables from the process environment.
        /// </summary>
        public FrameworkReferenceResolver()
            : this(Environment.GetEnvironmentVariable)
        {
        }

        /// <summary>
        /// Creates an instance that reads environment variables via the supplied delegate.
        /// Use this from MSBuild tasks to route reads through TaskEnvironment.
        /// </summary>
        public FrameworkReferenceResolver(Func<string, string> getEnvironmentVariable)
        {
            _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        }

        public string GetDefaultReferenceAssembliesPath()
        {
            // Allow setting the reference assemblies path via an environment variable.
            // We read this directly instead of calling DotNetReferenceAssembliesPathResolver.Resolve()
            // because that runtime method uses process-global Environment.GetEnvironmentVariable.
            var referenceAssembliesPath = _getEnvironmentVariable(DotNetReferenceAssembliesPathResolver.DotNetReferenceAssembliesPathEnv);

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
