// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace Microsoft.NET.Build.Tasks
{
    internal static class FrameworkReferenceResolver
    {
        public static string GetDefaultReferenceAssembliesPath(TaskEnvironment taskEnvironment)
        {
            // Allow setting the reference assemblies path via an environment variable
            var referenceAssembliesPath = DotNetReferenceAssembliesPathResolver.Resolve();

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
            var programFiles = taskEnvironment.GetEnvironmentVariable("ProgramFiles(x86)");

            if (string.IsNullOrEmpty(programFiles))
            {
                // On 32 bit machines they are in %ProgramFiles%
                programFiles = taskEnvironment.GetEnvironmentVariable("ProgramFiles");
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
