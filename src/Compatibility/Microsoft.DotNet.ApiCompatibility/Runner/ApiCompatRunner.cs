// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.ApiCompatibility.Runner
{
    /// <summary>
    /// Enqueues work items and performs api compatibility checks on them.
    /// </summary>
    public class ApiCompatRunner : IApiCompatRunner
    {
        private readonly HashSet<ApiCompatRunnerWorkItem> _workItems = new();
        private readonly ICompatibilityLogger _log;
        private readonly ISuppressionEngine _suppressionEngine;
        private readonly IApiComparerFactory _apiComparerFactory;
        private readonly IAssemblySymbolLoaderFactory _assemblySymbolLoaderFactory;
        private readonly IMetadataStreamProvider _metadataStreamProvider;

        /// <inheritdoc />
        public IReadOnlyCollection<ApiCompatRunnerWorkItem> WorkItems => _workItems;

        public ApiCompatRunner(ICompatibilityLogger log,
            ISuppressionEngine suppressionEngine,
            IApiComparerFactory apiComparerFactory,
            IAssemblySymbolLoaderFactory assemblySymbolLoaderFactory,
            IMetadataStreamProvider metadataStreamProvider)
        {
            _suppressionEngine = suppressionEngine;
            _log = log;
            _apiComparerFactory = apiComparerFactory;
            _assemblySymbolLoaderFactory = assemblySymbolLoaderFactory;
            _metadataStreamProvider = metadataStreamProvider;
        }

        /// <inheritdoc />
        public void ExecuteWorkItems()
        {
            _log.LogMessage(MessageImportance.Low, Resources.ApiCompatRunnerExecutingWorkItems, _workItems.Count.ToString());

            foreach (ApiCompatRunnerWorkItem workItem in _workItems)
            {
                bool runWithReferences = true;

                List<ElementContainer<IAssemblySymbol>> leftContainerList = new();
                foreach (MetadataInformation left in workItem.Lefts)
                {
                    IAssemblySymbol leftSymbols;
                    using (Stream leftAssemblyStream = _metadataStreamProvider.GetStream(left))
                    {
                        leftSymbols = GetAssemblySymbolFromStream(leftAssemblyStream, left, workItem.Options, out bool resolvedReferences);
                        runWithReferences &= resolvedReferences;
                    }

                    leftContainerList.Add(new ElementContainer<IAssemblySymbol>(leftSymbols, left));
                }

                List<ElementContainer<IAssemblySymbol>> rightContainerList = new();
                foreach (MetadataInformation right in workItem.Rights)
                {
                    IAssemblySymbol rightSymbols;
                    using (Stream rightAssemblyStream = _metadataStreamProvider.GetStream(right))
                    {
                        rightSymbols = GetAssemblySymbolFromStream(rightAssemblyStream, right, workItem.Options, out bool resolvedReferences);
                        runWithReferences &= resolvedReferences;
                    }

                    rightContainerList.Add(new ElementContainer<IAssemblySymbol>(rightSymbols, right));
                }

                // Create and configure the work item specific api comparer
                IApiComparer apiComparer = _apiComparerFactory.Create();
                apiComparer.StrictMode = workItem.Options.EnableStrictMode;
                apiComparer.WarnOnMissingReferences = runWithReferences;

                // TODO: Support passing in multiple lefts in ApiComparer: https://github.com/dotnet/sdk/issues/17364.

                // Invoke the api comparer for the work item and operate on the difference result
                IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                    apiComparer.GetDifferences(leftContainerList[0], rightContainerList);

                foreach ((MetadataInformation Left, MetadataInformation Right, IEnumerable<CompatDifference> differences) diff in differences)
                {
                    // Log the difference header only if there are differences and errors aren't baselined.
                    bool logHeader = !_suppressionEngine.BaselineAllErrors;

                    foreach (CompatDifference difference in diff.differences)
                    {
                        Suppression suppression = new(difference.DiagnosticId)
                        {
                            Target = difference.ReferenceId,
                            Left = diff.Left.AssemblyId,
                            Right = diff.Right.AssemblyId,
                            IsBaselineSuppression = workItem.Options.IsBaselineComparison
                        };

                        // If the error is suppressed, don't log anything.
                        if (_suppressionEngine.IsErrorSuppressed(suppression))
                            continue;

                        if (logHeader)
                        {
                            logHeader = false;
                            _log.LogMessage(MessageImportance.Normal,
                                Resources.ApiCompatibilityHeader,
                                diff.Left.AssemblyId,
                                diff.Right.AssemblyId,
                                workItem.Options.IsBaselineComparison ? diff.Left.FullPath! : "left",
                                workItem.Options.IsBaselineComparison ? diff.Right.FullPath! : "right");
                        }

                        _log.LogError(suppression,
                            difference.DiagnosticId,
                            difference.Message);
                    }

                    _log.LogMessage(MessageImportance.Low,
                        Resources.ApiCompatibilityFooter,
                        diff.Left.AssemblyId,
                        diff.Right.AssemblyId,
                        workItem.Options.IsBaselineComparison ? diff.Left.FullPath! : "left",
                        workItem.Options.IsBaselineComparison ? diff.Right.FullPath! : "right");
                }
            }

            _workItems.Clear();
        }

        private IAssemblySymbol GetAssemblySymbolFromStream(Stream assemblyStream, MetadataInformation assemblyInformation, ApiCompatRunnerOptions options, out bool resolvedReferences)
        {
            resolvedReferences = false;

            // In order to enable reference support for baseline suppression we need a better way
            // to resolve references for the baseline package. Let's not enable it for now.
            bool shouldResolveReferences = !options.IsBaselineComparison &&
                assemblyInformation.References != null;

            // Create the work item specific assembly symbol loader and configure if references should be resolved
            IAssemblySymbolLoader loader = _assemblySymbolLoaderFactory.Create(shouldResolveReferences);
            if (shouldResolveReferences)
            {
                resolvedReferences = true;
                loader.AddReferenceSearchDirectories(assemblyInformation.References!);
            }

            return loader.LoadAssembly(assemblyInformation.AssemblyName, assemblyStream);
        }

        /// <inheritdoc />
        public void EnqueueWorkItem(ApiCompatRunnerWorkItem workItem)
        {
            if (_workItems.TryGetValue(workItem, out ApiCompatRunnerWorkItem actualWorkItem))
            {
                foreach (MetadataInformation right in workItem.Rights)
                {
                    actualWorkItem.Rights.Add(right);
                }
            }
            else
            {
                _workItems.Add(workItem);
            }
        }
    }
}
