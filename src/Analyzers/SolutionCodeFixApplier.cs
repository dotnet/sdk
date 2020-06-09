// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class SolutionCodeFixApplier : ICodeFixApplier
    {
        public async Task<Solution> ApplyCodeFixesAsync(
            Solution solution,
            CodeAnalysisResult result,
            CodeFixProvider codeFix,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var diagnosticId = result.Diagnostics.FirstOrDefault().Value.FirstOrDefault()?.Id;

            var fixAllProvider = codeFix.GetFixAllProvider();
            if (fixAllProvider?.GetSupportedFixAllScopes()?.Contains(FixAllScope.Solution) != true)
            {
                logger.LogWarning($"Unable to fix {diagnosticId}. Code fix {codeFix.GetType().Name} doesn't support Fix All in Solution.");
                return solution;
            }

            var project = solution.Projects.FirstOrDefault();
            if (project == null)
            {
                throw new InvalidOperationException($"Solution {solution} has no projects");
            }

            var fixAllContext = new FixAllContext(
                project: project,
                codeFixProvider: codeFix,
                scope: FixAllScope.Solution,
                codeActionEquivalenceKey: null,
                diagnosticIds: codeFix.FixableDiagnosticIds,
                fixAllDiagnosticProvider: new DiagnosticProvider(result),
                cancellationToken: cancellationToken);

            try
            {
                var action = await fixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);
                var operations = action != null
                    ? await action.GetOperationsAsync(cancellationToken).ConfigureAwait(false)
                    : ImmutableArray<CodeActionOperation>.Empty;
                var applyChangesOperation = operations.OfType<ApplyChangesOperation>().SingleOrDefault();
                return applyChangesOperation?.ChangedSolution ?? solution;
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Failed to apply code fix {codeFix?.GetType().Name} for {diagnosticId}: {ex.Message}");
                return solution;
            }
        }

        private class DiagnosticProvider : FixAllContext.DiagnosticProvider
        {
            private static Task<IEnumerable<Diagnostic>> EmptyDignosticResult => Task.FromResult(Enumerable.Empty<Diagnostic>());
            private readonly IReadOnlyDictionary<Project, List<Diagnostic>> _diagnosticsByProject;

            internal DiagnosticProvider(CodeAnalysisResult analysisResult)
            {
                _diagnosticsByProject = analysisResult.Diagnostics;
            }

            public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                return GetProjectDiagnosticsAsync(project, cancellationToken);
            }

            public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                return _diagnosticsByProject.ContainsKey(project)
                    ? Task.FromResult<IEnumerable<Diagnostic>>(_diagnosticsByProject[project])
                    : EmptyDignosticResult;
            }
        }
    }
}
