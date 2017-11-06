// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.IO;
using System.Security;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Determines if any Reference depends on netstandard.dll.
    /// </summary>
    public partial class GetDependsOnNETStandard : TaskBase
    {
        private const string NetStandardAssemblyName = "netstandard";

        // System.Runtime from netstandard1.5
        // We also treat this as depending on netstandard so that we can provide netstandard1.5 and netstandard1.6 compatible 
        // facades since net461 was previously only compatible with netstandard1.4 and thus packages only provided netstandard1.4
        // compatible facades.
        private const string SystemRuntimeAssemblyName = "System.Runtime";
        private static readonly Version SystemRuntimeMinVersion = new Version(4, 1, 0, 0);

        //   Encountered conflict between 'Platform:System.IO.Compression.dll' and 'CopyLocal:C:\git\dotnet-sdk\packages\system.io.compression\4.3.0\runtimes\win\lib\net46\System.IO.Compression.dll'.  Choosing 'CopyLocal:C:\git\dotnet-sdk\packages\system.io.compression\4.3.0\runtimes\win\lib\net46\System.IO.Compression.dll' because AssemblyVersion '4.1.2.0' is greater than '4.0.0.0'.
        //  .NET Standard facade version: 4.2.0.0
        //   Encountered conflict between 'Platform:System.Net.Http.dll' and 'CopyLocal:C:\git\dotnet-sdk\packages\system.net.http\4.3.0\runtimes\win\lib\net46\System.Net.Http.dll'.  Choosing 'CopyLocal:C:\git\dotnet-sdk\packages\system.net.http\4.3.0\runtimes\win\lib\net46\System.Net.Http.dll' because AssemblyVersion '4.1.1.0' is greater than '4.0.0.0'.
        //  .NET Standard facade version: 4.2.0.0

        private const string SystemIOCompressionAssemblyName = "System.IO.Compression";
        private static readonly Version SystemIOCompressionMinVersion = new Version(4, 1, 0, 0);

        private const string SystemNetHttpAssemblyName = "System.Net.Http";
        private static readonly Version SystemNetHttpMinVersion = new Version(4, 1, 0, 0);

        /// <summary>
        /// Set of reference items to analyze.
        /// </summary>
        [Required]
        public ITaskItem[] References { get; set; }

        /// <summary>
        /// True if any of the references depend on netstandard.dll
        /// </summary>
        [Output]
        public bool DependsOnNETStandard { get; set; }

        /// <summary>
        /// True if any of the references depend on a version of System.IO.Compression at least 4.0.1.0 (ie from a NuGet package)
        /// </summary>
        [Output]
        public bool DependsOnNuGetCompression { get; set; }

        /// <summary>
        /// True if any of the references depend on a version of System.Net.Http at least 4.0.1.0 (ie from a NuGet package)
        /// </summary>
        [Output]
        public bool DependsOnNuGetHttp { get; set; }

        protected override void ExecuteCore()
        {
            ProcessReferences();
        }

        private void ProcessReferences()
        {
            DependsOnNETStandard = false;
            DependsOnNuGetCompression = false;
            DependsOnNuGetHttp = false;

            foreach (var reference in References)
            {
                var referenceSourcePath = ItemUtilities.GetSourcePath(reference);

                if (referenceSourcePath != null && File.Exists(referenceSourcePath))
                {
                    try
                    {
                        bool dependsOnNETStandard;
                        bool dependsOnNuGetCompression;
                        bool dependsOnNuGetHttp;

                        GetFileDependsOn(referenceSourcePath, out dependsOnNETStandard, out dependsOnNuGetCompression, out dependsOnNuGetHttp);

                        DependsOnNETStandard |= dependsOnNETStandard;
                        DependsOnNuGetCompression |= dependsOnNuGetCompression;
                        DependsOnNuGetHttp |= dependsOnNuGetHttp;
                    }
                    catch (Exception e) when (IsReferenceException(e))
                    {
                        // ResolveAssemblyReference treats all of these exceptions as warnings so we'll do the same
                        Log.LogWarning(Strings.GetDependsOnNETStandardFailedWithException, e.Message);
                    }
                }
            }


        }

        // ported from MSBuild's ReferenceTable.SetPrimaryAssemblyReferenceItem
        private static bool IsReferenceException(Exception e)
        {
            // These all derive from IOException
            //     DirectoryNotFoundException
            //     DriveNotFoundException
            //     EndOfStreamException
            //     FileLoadException
            //     FileNotFoundException
            //     PathTooLongException
            //     PipeException
            return e is BadImageFormatException
                   || e is UnauthorizedAccessException
                   || e is NotSupportedException
                   || (e is ArgumentException && !(e is ArgumentNullException))
                   || e is SecurityException
                   || e is IOException;
        }

    }
}
