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
            initContext.RegisterForPostInitialization((i) => 
                i.AddSource("UnifiedAssemblyInfo", GetProvideApplicationPartFactorySourceText()));

            initContext.RegisterExecutionPipeline(context =>
            {
                var rsgOptions = context.AnalyzerConfigOptionsProvider.Select(ComputeRazorCodeGenerationOptions);
                var sourceItems = context.AdditionalTextsProvider
                    .Combine(context.AnalyzerConfigOptionsProvider)
                    .Select(ComputeProjectItems);

                var references = context.CompilationProvider.Select((compilation, ct) => compilation.References);

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
                    .Select((pair, ct) => {
                        var (discoveryProjectEngine, compilation) = pair;
                        var tagHelperFeature = GetFeature<StaticCompilationTagHelperFeature>(discoveryProjectEngine);
                        return GetTagHelpers(
                            compilation.References,
                            tagHelperFeature ?? new StaticCompilationTagHelperFeature(),
                            compilation
                        );
                    });

                var tagHelpers = tagHelpersFromCompilation.Combine(tagHelpersFromReferences);

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

                context.RegisterSourceOutput(sourceItems.Combine(generationProjectEngine.Collect()), (context, pair) => {
                        var (projectItem, projectEngines) = pair;
                        var projectEngine = projectEngines.Last();

                        var codeDocument = projectEngine.Process(projectItem);
                        var csharpDocument = codeDocument.GetCSharpDocument();
                        context.AddSource(GetIdentifierFromPath(projectItem.RelativePhysicalPath), csharpDocument.GeneratedCode);
                    });
            });
        }
    }
}