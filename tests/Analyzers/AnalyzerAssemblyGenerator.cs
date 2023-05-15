// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Microsoft.CodeAnalysis.Tools.Tests.Analyzers
{
    public static class AnalyzerAssemblyGenerator
    {
        private static async Task<IEnumerable<MetadataReference>> GetReferencesAsync()
        {
            var references = new List<MetadataReference>()
            {
                MetadataReference.CreateFromFile(typeof(ImmutableArray).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(SharedAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DiagnosticAnalyzer).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CodeFixProvider).Assembly.Location),
            };

            var netcoreMetadataReferences = await ReferenceAssemblies.Net.Net70.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
            references.AddRange(netcoreMetadataReferences.Where(reference => Path.GetFileName(reference.Display) != "System.Collections.Immutable.dll"));

            return references;
        }

        public static SyntaxTree GenerateCodeFix(string typeName, string diagnosticId)
        {
            var codefix = $@"
using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof({typeName})), Shared]
public class {typeName} : CodeFixProvider
{{
    public const string DiagnosticId = ""{diagnosticId}"";

    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider()
    {{
        return WellKnownFixAllProviders.BatchFixer;
    }}

    public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
    {{
        throw new NotImplementedException();
    }}
}}";
            return CSharpSyntaxTree.ParseText(codefix);
        }

        public static SyntaxTree GenerateAnalyzerCode(string typeName, string diagnosticId)
        {
            var analyzer = $@"
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class {typeName} : DiagnosticAnalyzer
{{
    public const string DiagnosticId = ""{diagnosticId}"";
    internal static readonly LocalizableString Title = ""{typeName} Title"";
    internal static readonly LocalizableString MessageFormat = ""{typeName} '{{0}}'"";
    internal const string Category = ""{typeName} Category"";
    internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, true);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);
    public override void Initialize(AnalysisContext context)
    {{
    }}
}}";
            return CSharpSyntaxTree.ParseText(analyzer);
        }

        public static async Task<Assembly> GenerateAssemblyAsync(params SyntaxTree[] trees)
        {
            var assemblyName = Guid.NewGuid().ToString();
            var references = await GetReferencesAsync();
            var compilation = CSharpCompilation.Create(assemblyName, trees, references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}");

                throw new Exception(string.Join(Environment.NewLine, failures));
            }
            else
            {
                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());
                return assembly;
            }
        }
    }
}
