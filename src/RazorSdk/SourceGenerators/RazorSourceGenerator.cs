// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    [Generator]
    public partial class RazorSourceGenerator : IIncrementalGenerator
    {
        static Dictionary<string, int> s_calls = new();

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {

            var razorSourceGeneratorOptionsWithDiagnostics = context.AnalyzerConfigOptionsProvider
                .Combine(context.ParseOptionsProvider)
                .Select(ComputeRazorSourceGeneratorOptions).RecordCalls("razorSourceGeneratorOptionsWithDiagnostics", s_calls);

            var razorSourceGeneratorOptions = razorSourceGeneratorOptionsWithDiagnostics.ReportDiagnostics(context);

            var sourceItemsWithDiagnostics = context.AdditionalTextsProvider
                .Combine(context.AnalyzerConfigOptionsProvider)
                .Where((pair) => pair.Item1.Path.EndsWith(".razor") || pair.Item1.Path.EndsWith(".cshtml"))
                .Select(ComputeProjectItems).RecordCalls("sourceItemsWithDiagnostics", s_calls);

            var sourceItems = sourceItemsWithDiagnostics.ReportDiagnostics(context);

            var references = context.CompilationProvider
                .WithLambdaComparer(
                    (c1, c2) => c1 != null && c2 != null && c1.References != c2.References)
                .Select((compilation, _) => compilation.References).RecordCalls("references", s_calls);

            var sourceItemsByName = sourceItems.Collect().WithLambdaComparer((@new, old) => @new.SequenceEqual(old, new LambdaComparer<SourceGeneratorProjectItem>((l, r) => string.Equals(l?.FilePath, r?.FilePath, System.StringComparison.OrdinalIgnoreCase))));

            var discoveryProjectEngine = references
                .Combine(razorSourceGeneratorOptions)
                .Combine(sourceItemsByName)
                .Select((pair, _) =>
                {
                    var ((references, razorSourceGeneratorOptions), projectItems) = pair;
                    var tagHelperFeature = new StaticCompilationTagHelperFeature();
                    return GetDiscoveryProjectEngine(tagHelperFeature, references, projectItems, razorSourceGeneratorOptions);
                }).RecordCalls("discoveryProjectEngine", s_calls);

            var syntaxTrees = sourceItems
                .Combine(discoveryProjectEngine)
                .Combine(context.ParseOptionsProvider)
                .Select((pair, _) =>
                {
                    var (itemAndDiscoveryEngine, parseOptions) = pair;
                    var (item, discoveryProjectEngine) = itemAndDiscoveryEngine;

                    var codeGen = discoveryProjectEngine.Process(item);
                    var generatedCode = codeGen.GetCSharpDocument().GeneratedCode;
                    return CSharpSyntaxTree.ParseText(generatedCode, (CSharpParseOptions)parseOptions);
                }).RecordCalls("syntaxTrees", s_calls);

            var tagHelpersFromCompilation = syntaxTrees
                .Combine(context.CompilationProvider)
                .Combine(discoveryProjectEngine)
                .SelectMany((pair, _) =>
                {
                    var ((syntaxTrees, compilation), discoveryProjectEngine) = pair;
                    var tagHelperFeature = GetFeature<StaticCompilationTagHelperFeature>(discoveryProjectEngine);
                    return GetTagHelpersFromCompilation(
                        compilation,
                        tagHelperFeature!,
                        syntaxTrees
                    );
                }).RecordCalls("tagHelpersFromCompilation", s_calls);

            var tagHelpersFromReferences = discoveryProjectEngine
                .Combine(context.CompilationProvider)
                .Combine(references)
                .Select((pair, _) =>
                {
                    var (engineAndCompilation, references) = pair;
                    var (discoveryProjectEngine, compilation) = engineAndCompilation;
                    var tagHelperFeature = GetFeature<StaticCompilationTagHelperFeature>(discoveryProjectEngine);
                    return GetTagHelpers(
                        references,
                        tagHelperFeature!,
                        compilation
                    );
                }).RecordCalls("tagHelpersFromReferences", s_calls);

            var tagHelpers = tagHelpersFromCompilation.Collect().Combine(tagHelpersFromReferences);
                //.Select((pair, _) => pair.Left.Union(pair.Right).ToList())
                //.WithLambdaComparer((t1, t2) => t1.SequenceEqual(t2));

            var generationProjectEngine = tagHelpers.Combine(razorSourceGeneratorOptions).Combine(sourceItems.Collect())
                .Select((pair, _) =>
                {
                    var (tagHelpersAndOptions, items) = pair;
                    var (tagHelpers, razorSourceGeneratorOptions) = tagHelpersAndOptions;
                    var (tagHelpersFromCompilation, tagHelpersFromReferences) = tagHelpers;
                    var tagHelpersCount = tagHelpersFromCompilation.Count() + tagHelpersFromReferences.Count;
                    var allTagHelpers = new List<TagHelperDescriptor>(tagHelpersCount);
                    allTagHelpers.AddRange(tagHelpersFromCompilation);
                    allTagHelpers.AddRange(tagHelpersFromReferences);

                    return GetGenerationProjectEngine(allTagHelpers, items, razorSourceGeneratorOptions);
                }).RecordCalls("generationProjectEngine", s_calls);

            var generationInputs = sourceItems
                .Combine(razorSourceGeneratorOptions)
                .Combine(generationProjectEngine).RecordCalls("generationInputs", s_calls);

            context.RegisterSourceOutput(generationInputs, (context, pair) =>
            {
                var (sourceItemsAndOptions, projectEngine) = pair;
                var (projectItem, razorSourceGeneratorOptions) = sourceItemsAndOptions;

                var decls = projectEngine.ProcessDesignTime(projectItem);
                var itermediateNode = decls.GetDocumentIntermediateNode();
                Visitor v = new Visitor();
                v.Visit(itermediateNode);
                var tags = v.usedTagHelpers;

                var codeDocument = projectEngine.Process(projectItem);
                var helpers = codeDocument.GetTagHelpers();
                var csharpDocument = codeDocument.GetCSharpDocument();



                for (var j = 0; j < csharpDocument.Diagnostics.Count; j++)
                {
                    var razorDiagnostic = csharpDocument.Diagnostics[j];
                    var csharpDiagnostic = razorDiagnostic.AsDiagnostic();
                    context.ReportDiagnostic(csharpDiagnostic);
                }

                if (!razorSourceGeneratorOptions.SuppressRazorSourceGenerator)
                {
                    context.AddSource(GetIdentifierFromPath(projectItem.RelativePhysicalPath), csharpDocument.GeneratedCode);
                }
            });

            context.RegisterSourceOutput(generationInputs.Collect(), (spc, t) =>
            {
                var calls = s_calls;

                calls.Clear();
            });
        }
    }

    internal abstract class DirectiveVisitor : SyntaxWalker
    {
        public abstract HashSet<TagHelperDescriptor> Matches { get; }

        public abstract string TagHelperPrefix { get; }

        public abstract void Visit(RazorSyntaxTree tree);
    }

    internal class Visitor : IntermediateNodeWalker
    {
        internal List<TagHelperDescriptor> usedTagHelpers = new();

        public override void VisitComponent(ComponentIntermediateNode node)
        {
            usedTagHelpers.Add(node.Component);
            base.VisitComponent(node);
        }
    }

}
