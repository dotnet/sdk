﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Comparing;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.ApiCompat
{
    internal sealed class ApiCompatServiceProvider
    {
        private readonly Lazy<ISuppressionEngine> _suppressionEngine;
        private readonly Lazy<ISuppressibleLog> _compatibilityLogger;
        private readonly Lazy<IApiCompatRunner> _apiCompatRunner;

        public ApiCompatServiceProvider(Func<ISuppressionEngine, ISuppressibleLog> logFactory,
            Func<ISuppressionEngine> suppressionEngineFactory,
            Func<ISuppressibleLog, IRuleFactory> ruleFactory,
            bool respectInternals,
            string[]? excludeAttributesFiles)
        {
            _suppressionEngine = new Lazy<ISuppressionEngine>(suppressionEngineFactory);
            _compatibilityLogger = new Lazy<ISuppressibleLog>(() => logFactory(SuppressionEngine));
            _apiCompatRunner = new Lazy<IApiCompatRunner>(() =>
            {
                AccessibilitySymbolFilter accessibilitySymbolFilter = new(respectInternals);
                SymbolEqualityComparer symbolEqualityComparer = new();

                ISymbolFilter attributeDataSymbolFilter = SymbolFilterFactory.GetFilterFromFiles(
                    apiExclusionFilePaths: excludeAttributesFiles,
                    accessibilitySymbolFilter: accessibilitySymbolFilter,
                    respectInternals: respectInternals);

                AttributeDataEqualityComparer attributeDataEqualityComparer = new(symbolEqualityComparer,
                        new TypedConstantEqualityComparer(symbolEqualityComparer));

                ApiComparerSettings apiComparerSettings = new(
                    symbolFilter: accessibilitySymbolFilter,
                    symbolEqualityComparer: symbolEqualityComparer,
                    attributeDataSymbolFilter: attributeDataSymbolFilter,
                    attributeDataEqualityComparer: attributeDataEqualityComparer,
                    includeInternalSymbols: respectInternals);

                return new ApiCompatRunner(SuppressibleLog,
                    SuppressionEngine,
                    new ApiComparerFactory(ruleFactory(SuppressibleLog), apiComparerSettings),
                    new AssemblySymbolLoaderFactory(SuppressibleLog, respectInternals));
            });
        }

        public ISuppressionEngine SuppressionEngine => _suppressionEngine.Value;
        public ISuppressibleLog SuppressibleLog => _compatibilityLogger.Value;
        public IApiCompatRunner ApiCompatRunner => _apiCompatRunner.Value;
    }
}
