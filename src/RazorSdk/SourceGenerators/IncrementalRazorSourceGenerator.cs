// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    [Generator]
    public partial class IncrementalRazorSourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext initContext)
        {
            initContext.RegisterExecutionPipeline(context =>
            {
                var rsgOptions = context.AnalyzerConfigOptionsProvider.Select(ComputeRazorCodeGenerationOptions);
                var sourceItemsWithDiagnostics = context.AdditionalTextsProvider
                    .Combine(context.AnalyzerConfigOptionsProvider)
                    .Select(ComputeProjectItems);

                var sourceItems = sourceItemsWithDiagnostics.ReportDiagnostics(context);

                context.RegisterSourceOutput(
                    sourceItems
                        .Where(item => item.FileKind == FileKinds.Legacy)
                        .Collect()
                        .WithLambdaComparer((c1, c2) => c1.Any() == c2.Any()),
                    (context, source) => context.AddSource("UnifiedAssemblyInfo", GetProvideApplicationPartFactorySourceText()));

                var references = context.CompilationProvider
                    .WithLambdaComparer(
                        (c1, c2) => c1 != null && c2 != null && c1.References == c2.References)
                    .Select((compilation, ct) => compilation.References);

                var discoveryProjectEngine = references
                    .Combine(rsgOptions)
                    .Combine(sourceItems.Collect())
                    .Select((pair, ct) => {
                        var ((references, rsgOptions), projectItems) = pair;
                        var tagHelperFeature = new StaticCompilationTagHelperFeature();
                        return GetDiscoveryProjectEngine(tagHelperFeature, references, projectItems, rsgOptions.RootNamespace);
                    });

                var syntaxTrees = sourceItems
                    .Combine(discoveryProjectEngine)
                    .Select((pair, ct) => {
                        var (item, discoveryProjectEngine) = pair;

                        var codeGen = discoveryProjectEngine.Process(item);
                        var generatedCode = codeGen.GetCSharpDocument().GeneratedCode;
                        return CSharpSyntaxTree.ParseText(generatedCode, new CSharpParseOptions(LanguageVersion.Preview));
                    });

                var tagHelpersFromCompilation = syntaxTrees
                    .Combine(context.CompilationProvider)
                    .Combine(discoveryProjectEngine)
                    .Select((pair, ct) => {
                        var ((syntaxTrees, compilation), discoveryProjectEngine) = pair;
                        var tagHelperFeature = GetFeature<StaticCompilationTagHelperFeature>(discoveryProjectEngine);
                        return GetTagHelpersFromCompilation(
                            compilation,
                            tagHelperFeature ?? new StaticCompilationTagHelperFeature(),
                            syntaxTrees
                        );
                    });

                var tagHelpersFromReferences = discoveryProjectEngine
                    .Combine(context.CompilationProvider)
                    .WithComparer(new LambdaComparer<(RazorProjectEngine, Compilation)>(
                        (p1, p2) => p1.Item2.References != p2.Item2.References
                    ))
                    .Select((pair, ct) => {
                        var (discoveryProjectEngine, compilation) = pair;
                        var tagHelperFeature = GetFeature<StaticCompilationTagHelperFeature>(discoveryProjectEngine);
                        return GetTagHelpers(
                            compilation.References,
                            tagHelperFeature ?? new StaticCompilationTagHelperFeature(),
                            compilation
                        );
                    });

                var tagHelpers = tagHelpersFromCompilation.Combine(tagHelpersFromReferences)
                    .WithLambdaComparer((tagHelpers, newTagHelpers) => {
                        var (tagHelpersFromCompilation, tagHelpersFromReferences) = tagHelpers;
                        var (newTagHelpersFromCompilation, newTagHelpersFromReferences) = newTagHelpers;
                        return tagHelpersFromCompilation != newTagHelpersFromCompilation &&
                            tagHelpersFromReferences != newTagHelpersFromReferences;
                    });

                var generationProjectEngine = tagHelpers.Combine(rsgOptions).Combine(sourceItems.Collect())
                    .Select((pair, ct) => {
                        var (tagHelpersAndOptions, items) = pair;
                        var (tagHelpers, rsgOptions) = tagHelpersAndOptions;
                        var (tagHelpersFromCompilation, tagHelpersFromReferences) = tagHelpers;
                        var tagHelpersCount = tagHelpersFromCompilation.Count + tagHelpersFromReferences.Count;
                        var allTagHelpers = new List<TagHelperDescriptor>(tagHelpersCount);
                        allTagHelpers.AddRange(tagHelpersFromCompilation);
                        allTagHelpers.AddRange(tagHelpersFromReferences);

                        return GetGenerationProjectEngine(allTagHelpers, items, rsgOptions);
                    });

                var generationInputs = sourceItems
                    .Combine(rsgOptions)
                    .Combine(generationProjectEngine.Collect());

                context.RegisterSourceOutput(generationInputs, (context, pair) => {
                        var (sourceItemsAndOptions, projectEngines) = pair;
                        var (projectItem, rsgOptions) = sourceItemsAndOptions;
                        var projectEngine = projectEngines.Last();

                        var codeDocument = projectEngine.Process(projectItem);
                        var csharpDocument = codeDocument.GetCSharpDocument();
                        if (!rsgOptions.SuppressRazorSourceGenerator)
                        {
                            context.AddSource(GetIdentifierFromPath(projectItem.RelativePhysicalPath), csharpDocument.GeneratedCode);
                        }
                    });
            });
        }
    }
}