// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
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
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger)
        {
            var assemblies = new[]
            {
                _featuresPath,
                _featuresCSharpPath,
                _featuresVisualBasicPath
            }.Select(path => Assembly.LoadFrom(path));

            var analyzersAndFixers = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            return solution.Projects
                .ToImmutableDictionary(project => project.Id, project => analyzersAndFixers);
        }

        public DiagnosticSeverity GetSeverity(FormatOptions formatOptions) => formatOptions.CodeStyleSeverity;
    }
}
