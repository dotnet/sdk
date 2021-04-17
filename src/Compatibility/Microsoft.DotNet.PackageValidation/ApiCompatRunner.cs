// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Runs ApiCompat over different assembly tuples.
    /// </summary>
    public class ApiCompatRunner
    {
        private static List<(Stream leftAssemblyStream, Stream rightAssemblyStream, string assemblyName, string source, string errorMessage)> queue = new();

        public static IEnumerable<CompatDifference> RunApiCompat()
        {
            List<CompatDifference> apiDifferences = new();
            foreach (var apicompatTuples in queue)
            {
                // TODO: Add version check and proper way to display api compat error messages for each tuple.
                // TODO: Add optimisations tuples.
                IAssemblySymbol leftSymbols =  new AssemblySymbolLoader().LoadAssembly(apicompatTuples.assemblyName, apicompatTuples.leftAssemblyStream);
                IAssemblySymbol rightSymbols = new AssemblySymbolLoader().LoadAssembly(apicompatTuples.assemblyName,  apicompatTuples.rightAssemblyStream);
                ApiComparer differ = new();
                apiDifferences.AddRange(differ.GetDifferences(leftSymbols, rightSymbols));
            }
            return apiDifferences;
        }

        public static void QueueApiCompat(Stream leftAssemblyStream, Stream rightAssemblyStream, string assemblyName, string source, string errorMessage)
        {
            queue.Add((leftAssemblyStream, rightAssemblyStream, assemblyName, source, errorMessage));
        }
    }   
}
