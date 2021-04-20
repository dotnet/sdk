// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Runs ApiCompat over different assembly tuples.
    /// </summary>
    public class ApiCompatRunner
    {
        private List<(Stream leftAssemblyStream, Stream rightAssemblyStream, string assemblyName, string compatibilityReason, string header)> queue = new();
        private string _noWarn;
        private (string, string)[] _ignoredDifferences;

        public ApiCompatRunner(string noWarn, (string, string)[] ignoredDifferences)
        {
            _noWarn = noWarn;
            _ignoredDifferences = ignoredDifferences;
        }

        /// <summary>
        /// Runs the api compat for the tuples in the queue.
        /// </summary>
        /// <returns>The list api compat diagnostics.</returns>
        public IEnumerable<ApiCompatDiagnostics> RunApiCompat()
        {
            List<ApiCompatDiagnostics> apiDifferences = new();
            foreach (var apicompatTuples in queue)
            {
                // TODO: Add version check and proper way to display api compat error messages for each tuple.
                // TODO: Add optimisations tuples.
                // TODO: Run it Asynchronously.
                IAssemblySymbol leftSymbols =  new AssemblySymbolLoader().LoadAssembly(apicompatTuples.assemblyName, apicompatTuples.leftAssemblyStream);
                IAssemblySymbol rightSymbols = new AssemblySymbolLoader().LoadAssembly(apicompatTuples.assemblyName,  apicompatTuples.rightAssemblyStream);
                ApiComparer differ = new();
                apiDifferences.Add(new ApiCompatDiagnostics(apicompatTuples.compatibilityReason, apicompatTuples.header, _noWarn, _ignoredDifferences, differ.GetDifferences(leftSymbols, rightSymbols)));
                apicompatTuples.leftAssemblyStream.Dispose();
                apicompatTuples.rightAssemblyStream.Dispose();
            }
            queue.Clear();
            return apiDifferences;
        }

        /// <summary>
        /// Queues the api compat for 2 assemblies.
        /// </summary>
        /// <param name="leftAssemblyStream">The left assembly stream.</param>
        /// <param name="rightAssemblyStream">The right assembly stream.</param>
        /// <param name="assemblyName">The name of the assembly.</param>
        /// <param name="compatibiltyReason">The reason for assembly compatibilty.</param>
        /// <param name="header">The header for the api compat diagnostics.</param>
        public void QueueApiCompat(Stream leftAssemblyStream, Stream rightAssemblyStream, string assemblyName, string compatibiltyReason, string header)
        {
            queue.Add((leftAssemblyStream, rightAssemblyStream, assemblyName, compatibiltyReason, header));
        }
    }   
}
