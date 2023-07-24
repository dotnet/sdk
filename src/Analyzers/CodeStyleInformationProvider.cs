// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
            return solution.Projects
                .ToImmutableDictionary(
                    project => project.Id,
                    project =>
                    {
                        if (!analyzersByLanguage.TryGetValue(project.Language, out var analyzersAndFixers))
                        {
                            var analyzers = references.SelectMany(reference => reference.GetAnalyzers(project.Language)).ToImmutableArray();
                            var codeFixes = AnalyzerFinderHelpers.LoadFixers(references.Select(reference => reference.GetAssembly()), project.Language);
                            analyzersAndFixers = new AnalyzersAndFixers(analyzers, codeFixes);
                            analyzersByLanguage.Add(project.Language, analyzersAndFixers);
                        }

                        return analyzersAndFixers;
                    });
        }

        public DiagnosticSeverity GetSeverity(FormatOptions formatOptions) => formatOptions.CodeStyleSeverity;
    }
}
