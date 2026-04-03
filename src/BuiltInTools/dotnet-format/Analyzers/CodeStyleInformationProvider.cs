// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class CodeStyleInformationProvider : IAnalyzerInformationProvider
    {
        private static readonly string s_executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;

        private readonly string _featuresPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.Features.dll");
        private readonly string _featuresCSharpPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.CSharp.Features.dll");
        private readonly string _featuresVisualBasicPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.VisualBasic.Features.dll");

        public ImmutableDictionary<ProjectId, AnalyzersAndFixers> GetAnalyzersAndFixers(
            Workspace workspace,
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger)
        {
            var analyzerService = workspace.Services.GetService<IAnalyzerService>() ?? throw new NotSupportedException();
            var analyzerAssemblyLoader = analyzerService.GetLoader();
            var references = new[]
                {
                    _featuresPath,
                    _featuresCSharpPath,
                    _featuresVisualBasicPath,
                }
                .Select(path => new AnalyzerFileReference(path, analyzerAssemblyLoader));

            var analyzersByLanguage = new Dictionary<string, AnalyzersAndFixers>();

            // We need AnalyzerReferenceInformationProvider to get all project suppressors
            var referenceProvider = new AnalyzerReferenceInformationProvider();
            var perProjectAnalyzersAndFixers = referenceProvider.GetAnalyzersAndFixers(workspace, solution, formatOptions, logger);

            return solution.Projects
                .ToImmutableDictionary(
                    project => project.Id,
                    project =>
                    {
                        if (!analyzersByLanguage.TryGetValue(project.Language, out var analyzersAndFixers))
                        {
                            var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
                            analyzers.AddRange(references.SelectMany(reference => reference.GetAnalyzers(project.Language)));
                            var codeFixes = AnalyzerFinderHelpers.LoadFixers(references.Select(reference => reference.GetAssembly()), project.Language);

                            // Add project suppressors to featured analyzers
                            if (perProjectAnalyzersAndFixers.TryGetValue(project.Id, out var thisProjectAnalyzersAndFixers))
                            {
                                analyzers.AddRange(thisProjectAnalyzersAndFixers.Analyzers.OfType<DiagnosticSuppressor>());
                            }

                            analyzersAndFixers = new AnalyzersAndFixers(analyzers.ToImmutableArray(), codeFixes);
                            analyzersByLanguage.Add(project.Language, analyzersAndFixers);
                        }

                        return analyzersAndFixers;
                    });
        }

        public DiagnosticSeverity GetSeverity(FormatOptions formatOptions) => formatOptions.CodeStyleSeverity;
    }
}
