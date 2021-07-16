// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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

            var syntaxTreesAndItems = sourceItems
                .Combine(discoveryProjectEngine)
                .Combine(context.ParseOptionsProvider)
                .Select((pair, _) =>
                {
                    var (itemAndDiscoveryEngine, parseOptions) = pair;
                    var (item, discoveryProjectEngine) = itemAndDiscoveryEngine;

                    var codeGen = discoveryProjectEngine.Process(item);
                    var generatedCode = codeGen.GetCSharpDocument().GeneratedCode;
                    return (CSharpSyntaxTree.ParseText(generatedCode, (CSharpParseOptions)parseOptions), item);
                }).RecordCalls("syntaxTrees", s_calls);

            var tagHelpersFromCompilation = syntaxTreesAndItems
                .Combine(context.CompilationProvider)
                .Combine(discoveryProjectEngine)
                .SelectMany((pair, _) =>
                {
                    var (((syntaxTree, item), compilation), discoveryProjectEngine) = pair;
                    var tagHelperFeature = GetFeature<StaticCompilationTagHelperFeature>(discoveryProjectEngine);
                    var helpers = GetTagHelpersFromCompilation(
                        compilation,
                        tagHelperFeature!,
                        syntaxTree
                    );
                    return helpers.Select(h => new SourceTagHelperInfo(h, item));
                }).RecordCalls("tagHelpersFromCompilation", s_calls);

            var tagHelpersFromReferences = discoveryProjectEngine
                .Combine(context.CompilationProvider)
                .Combine(references)
                .Select((pair, _) =>
                {
                    var (engineAndCompilation, references) = pair;
                    var (discoveryProjectEngine, compilation) = engineAndCompilation;
                    var tagHelperFeature = GetFeature<StaticCompilationTagHelperFeature>(discoveryProjectEngine);
                    var helpers = GetTagHelpers(
                            references,
                            tagHelperFeature!,
                            compilation
                        );
                    return helpers.Select(h => new TagHelperInfo(h));
                })
                .RecordCalls("tagHelpersFromReferences", s_calls);

            var tagHelpersWithInfo = tagHelpersFromCompilation.Collect().Combine(tagHelpersFromReferences)
                .Select((pair, _) => pair.Left.Union(pair.Right).ToList())
                .WithLambdaComparer((t1, t2) => t1.SequenceEqual(t2));

            var tagHelpers = tagHelpersWithInfo
                .Select((th, _) => th.Select(t => t.TagHelper).ToList())
                .WithLambdaComparer((t1, t2) => t1.SequenceEqual(t2));

            var generationProjectEngine = tagHelpers.Combine(razorSourceGeneratorOptions).Combine(sourceItems.Collect())
                .Select((pair, _) =>
                {
                    var (tagHelpersAndOptions, items) = pair;
                    var (tagHelpers, razorSourceGeneratorOptions) = tagHelpersAndOptions;
                    return GetGenerationProjectEngine(tagHelpers, items, razorSourceGeneratorOptions);
                }).RecordCalls("generationProjectEngine", s_calls);


            // figure out what tag helpers each source file actually uses
            // we ignore the engine status, but re-run if the tags have changed.
            var sourceWithTagHelpers = sourceItems
                .Combine(generationProjectEngine)
                .WithLHSComparer()
                .Combine(tagHelpers)
                .Select((pair, _) =>
                {
                    var ((projectItem, projectEngine), _) = pair;

                    projectEngine.Process(projectItem);
                    var decls = projectEngine.Process(projectItem);
                    var itermediateNode = decls.GetDocumentIntermediateNode();

                    Visitor v = new Visitor();
                    v.Visit(itermediateNode);
                    var tags = v.usedTagHelpers;

                    return (projectItem, tags);
                })
                .WithLambdaComparer((p1, p2) => p1.projectItem.Equals(p2.projectItem) && p1.tags.SequenceEqual(p2.tags))
                .RecordCalls("sourceWithTagHelpers", s_calls);

            // now we work out if any of the sourceWithTagHelpers can have changed based on the tags
            var changedSources = sourceWithTagHelpers
                .Combine(tagHelpersWithInfo)
                .Select((pair, _) =>
                {
                    var (source, helpers) = pair;
                    var matchingTagInfos = helpers.Where(t => source.tags.Contains(t.TagHelper)).ToList();
                    return (source.projectItem, matchingTagInfos);
                });

            var generationInputs = changedSources
                .Combine(razorSourceGeneratorOptions)
                .Combine(generationProjectEngine)
                .WithLHSComparer((@new, old) => @new.Right.Equals(old.Right) && @new.Left.projectItem.Equals(old.Left.projectItem) && @new.Left.matchingTagInfos.SequenceEqual(old.Left.matchingTagInfos))
                .RecordCalls("generationInputs", s_calls);

            context.RegisterSourceOutput(generationInputs, (context, pair) =>
            {
                var (sourceItemsAndOptions, projectEngine) = pair;
                var ((projectItem, _), razorSourceGeneratorOptions) = sourceItemsAndOptions;

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
                Console.WriteLine("CallBreakDown:\r\n");
                foreach (var kvp in calls)
                {
                    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
                calls.Clear();
            });
        }
    }

    internal record TagHelperInfo(TagHelperDescriptor TagHelper);

    internal record SourceTagHelperInfo(TagHelperDescriptor TagHelper, SourceGeneratorProjectItem Source) : TagHelperInfo(TagHelper);

    internal record SourceWithTagHelpers(SourceGeneratorProjectItem Source, IReadOnlyList<TagHelperDescriptor> TagHelpers);

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

namespace System.Runtime.CompilerServices
{
    using global::System.Diagnostics;
    using global::System.Diagnostics.CodeAnalysis;

    internal static class IsExternalInit
    {
    }
}
