// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseAutoValidateAntiforgeryToken : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor UseAutoValidateAntiforgeryTokenRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5391",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.UseAutoValidateAntiforgeryToken),
            nameof(MicrosoftNetCoreAnalyzersResources.UseAutoValidateAntiforgeryTokenMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.UseAutoValidateAntiforgeryTokenDescription));
        internal static DiagnosticDescriptor MissHttpVerbAttributeRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5395",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.MissHttpVerbAttribute),
            nameof(MicrosoftNetCoreAnalyzersResources.MissHttpVerbAttributeMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.MissHttpVerbAttributeDescription));

        private static readonly Regex s_AntiForgeryAttributeRegex = new("^[a-zA-Z]*Validate[a-zA-Z]*Anti[Ff]orgery[a-zA-Z]*Attribute$", RegexOptions.Compiled);
        private static readonly Regex s_AntiForgeryRegex = new("^[a-zA-Z]*Validate[a-zA-Z]*Anti[Ff]orgery[a-zA-Z]*$", RegexOptions.Compiled);
        private static readonly ImmutableHashSet<string> HttpVerbAttributesMarkingOnActionModifyingMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpPostAttribute,
                WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpPutAttribute,
                WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpDeleteAttribute,
                WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpPatchAttribute);

        // It is used to translate ConcurrentDictionary into ConcurrentHashset, which is not provided.
        private const bool placeholder = true;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            UseAutoValidateAntiforgeryTokenRule,
            MissHttpVerbAttributeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var compilation = compilationStartAnalysisContext.Compilation;
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcFiltersFilterCollection, out var filterCollectionTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcController, out var controllerTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcControllerBase, out var controllerBaseTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcNonActionAttribute, out var nonActionAttributeTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcRoutingHttpMethodAttribute, out var httpMethodAttributeTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcFiltersIFilterMetadata, out var iFilterMetadataTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreAntiforgeryIAntiforgery, out var iAntiforgeryTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcFiltersIAsyncAuthorizationFilter, out var iAsyncAuthorizationFilterTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcFiltersIAuthorizationFilter, out var iAuthorizationFilterTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask, out var taskTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcFiltersAuthorizationFilterContext, out var authorizationFilterContextTypeSymbol))
                {
                    return;
                }

                var httpVerbAttributeTypeSymbolsAbleToModify = HttpVerbAttributesMarkingOnActionModifyingMethods.Select(
                    s => wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(s, out var attributeTypeSymbol) ? attributeTypeSymbol : null);

                if (httpVerbAttributeTypeSymbolsAbleToModify.Any(s => s == null))
                {
                    return;
                }

                var cancellationToken = compilationStartAnalysisContext.CancellationToken;

                // A dictionary from method symbol to set of methods calling it directly.
                var inverseGraph = new ConcurrentDictionary<ISymbol, ConcurrentDictionary<ISymbol, bool>>();

                // Ignore cases where a global anti forgery filter is in use.
                var hasGlobalAntiForgeryFilter = false;

                // Verify that validate anti forgery token attributes are used somewhere within this project,
                // to avoid reporting false positives on projects that use an alternative approach to mitigate CSRF issues.
                var usingValidateAntiForgeryAttribute = false;
                ConcurrentDictionary<IMethodSymbol, bool> onAuthorizationMethodSymbols = new ConcurrentDictionary<IMethodSymbol, bool>();
                var actionMethodSymbols = new ConcurrentDictionary<(IMethodSymbol, string), bool>();
                var actionMethodNeedAddingHttpVerbAttributeSymbols = new ConcurrentDictionary<IMethodSymbol, bool>();

                // Constructing inverse callGraph.
                // When it comes to delegate function assignment Del handler = DelegateMethod;, inverse call Graph will add:
                // (1) key: method gets called in DelegateMethod, value: handler.
                // When it comes to calling delegate function handler(), inverse callGraph will add:
                // (1) key: delegate function handler, value: callerMethod.
                // (2) key: Invoke(), value: callerMethod.
                compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                    (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                    {
                        if (hasGlobalAntiForgeryFilter)
                        {
                            return;
                        }

                        var owningSymbol = operationBlockStartAnalysisContext.OwningSymbol;
                        inverseGraph.GetOrAdd(owningSymbol, (_) => new ConcurrentDictionary<ISymbol, bool>());

                        operationBlockStartAnalysisContext.RegisterOperationAction(operationContext =>
                        {
                            ISymbol? calledSymbol = null;
                            ConcurrentDictionary<ISymbol, bool>? callers = null;

                            switch (operationContext.Operation)
                            {
                                case IInvocationOperation invocationOperation:
                                    calledSymbol = invocationOperation.TargetMethod.OriginalDefinition;

                                    break;

                                case IFieldReferenceOperation fieldReferenceOperation:
                                    var fieldSymbol = fieldReferenceOperation.Field;

                                    if (fieldSymbol.Type.TypeKind == TypeKind.Delegate)
                                    {
                                        calledSymbol = fieldSymbol;

                                        break;
                                    }

                                    return;
                            }

                            if (calledSymbol == null)
                            {
                                return;
                            }

                            callers = inverseGraph.GetOrAdd(calledSymbol, (_) => new ConcurrentDictionary<ISymbol, bool>());
                            callers.TryAdd(owningSymbol, placeholder);
                        }, OperationKind.Invocation, OperationKind.FieldReference);
                    });

                // Holds if the project has a global anti forgery filter.
                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    if (hasGlobalAntiForgeryFilter)
                    {
                        return;
                    }

                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                    var methodSymbol = invocationOperation.TargetMethod;

                    if (methodSymbol.Name == "Add" &&
                        methodSymbol.ContainingType.GetBaseTypesAndThis().Contains(filterCollectionTypeSymbol))
                    {
                        var potentialAntiForgeryFilters = invocationOperation
                            .Arguments
                            .Where(s => s.Parameter.Name == "filterType")
                            .Select(s => s.Value)
                            .OfType<ITypeOfOperation>()
                            .Select(s => s.TypeOperand)
                            .Union(methodSymbol.TypeArguments);

                        foreach (var potentialAntiForgeryFilter in potentialAntiForgeryFilters)
                        {
                            if (potentialAntiForgeryFilter.AllInterfaces.Contains(iFilterMetadataTypeSymbol) &&
                                s_AntiForgeryRegex.IsMatch(potentialAntiForgeryFilter.Name))
                            {
                                hasGlobalAntiForgeryFilter = true;

                                return;
                            }
                            else if (potentialAntiForgeryFilter.AllInterfaces.Contains(iAsyncAuthorizationFilterTypeSymbol))
                            {
                                // ASP.NET Core MVC seems to prioritize asynchronous over synchronous methods.
                                // https://github.com/dotnet/aspnetcore/blob/c925f99cddac0df90ed0bc4a07ecda6b054a0b02/src/Mvc/Mvc.Core/src/Infrastructure/ResourceInvoker.cs#L311
                                IMethodSymbol? onAuthorizationAsyncMethodSymbol =
                                    potentialAntiForgeryFilter
                                        .GetBaseTypesAndThis()
                                        .SelectMany(s => s.GetMembers())
                                        .OfType<IMethodSymbol>()
                                        .FirstOrDefault(
                                            s =>
                                                s.Name == "OnAuthorizationAsync" &&
                                                SymbolEqualityComparer.Default.Equals(s.ReturnType, taskTypeSymbol) &&
                                                s.Parameters.Length == 1 &&
                                                SymbolEqualityComparer.Default.Equals(
                                                    s.Parameters[0].Type,
                                                    authorizationFilterContextTypeSymbol));
                                if (onAuthorizationAsyncMethodSymbol != null)
                                {
                                    onAuthorizationMethodSymbols.TryAdd(
                                        onAuthorizationAsyncMethodSymbol,
                                        placeholder);
                                }
                            }
                            else if (potentialAntiForgeryFilter.AllInterfaces.Contains(iAuthorizationFilterTypeSymbol))
                            {
                                IMethodSymbol? onAuthorizationMethodSymbol =
                                    potentialAntiForgeryFilter
                                        .GetBaseTypesAndThis()
                                        .SelectMany(s => s.GetMembers())
                                        .OfType<IMethodSymbol>()
                                        .FirstOrDefault(
                                            s =>
                                                s.Name == "OnAuthorization" &&
                                                s.ReturnsVoid &&
                                                s.Parameters.Length == 1 &&
                                                SymbolEqualityComparer.Default.Equals(
                                                    s.Parameters[0].Type,
                                                    authorizationFilterContextTypeSymbol));
                                if (onAuthorizationMethodSymbol != null)
                                {
                                    onAuthorizationMethodSymbols.TryAdd(
                                        onAuthorizationMethodSymbol,
                                        placeholder);
                                }
                            }
                        }
                    }
                }, OperationKind.Invocation);

                compilationStartAnalysisContext.RegisterSymbolAction(symbolAnalysisContext =>
                {
                    if (hasGlobalAntiForgeryFilter)
                    {
                        return;
                    }

                    var onlyLookAtDerivedClassesOfController = compilationStartAnalysisContext.Options.GetBoolOptionValue(
                        optionName: EditorConfigOptionNames.ExcludeAspnetCoreMvcControllerBase,
                        rule: UseAutoValidateAntiforgeryTokenRule,
                        symbolAnalysisContext.Symbol,
                        compilation,
                        defaultValue: true);

                    var derivedControllerTypeSymbol = (INamedTypeSymbol)symbolAnalysisContext.Symbol;
                    var baseTypes = derivedControllerTypeSymbol.GetBaseTypes();

                    // An subtype of `Microsoft.AspNetCore.Mvc.Controller`, which probably indicates views are used and maybe cookie-based authentication is used and thus CSRF is a concern.
                    if (baseTypes.Contains(controllerTypeSymbol) ||
                            (!onlyLookAtDerivedClassesOfController &&
                            baseTypes.Contains(controllerBaseTypeSymbol)))
                    {
                        // The controller class is not protected by a validate anti forgery token attribute.
                        if (!IsUsingAntiFogeryAttribute(derivedControllerTypeSymbol))
                        {
                            foreach (var actionMethodSymbol in derivedControllerTypeSymbol.GetMembers().OfType<IMethodSymbol>())
                            {
                                if (actionMethodSymbol.MethodKind == MethodKind.Constructor)
                                {
                                    continue;
                                }

                                if (actionMethodSymbol.IsPublic() &&
                                    !actionMethodSymbol.IsStatic)
                                {
                                    var hasNonActionAttribute = actionMethodSymbol.HasAttribute(nonActionAttributeTypeSymbol);
                                    var overridenMethodSymbol = actionMethodSymbol as ISymbol;

                                    while (!hasNonActionAttribute && overridenMethodSymbol.IsOverride)
                                    {
                                        overridenMethodSymbol = overridenMethodSymbol.GetOverriddenMember();

                                        if (overridenMethodSymbol.HasAttribute(nonActionAttributeTypeSymbol))
                                        {
                                            hasNonActionAttribute = true;
                                        }
                                    }

                                    // The method has [NonAction].
                                    if (hasNonActionAttribute)
                                    {
                                        continue;
                                    }

                                    // The method is not protected by a validate anti forgery token attribute.
                                    if (!IsUsingAntiFogeryAttribute(actionMethodSymbol))
                                    {
                                        var httpVerbAttributeTypeSymbolAbleToModify = actionMethodSymbol.GetAttributes().FirstOrDefault(s => httpVerbAttributeTypeSymbolsAbleToModify.Contains(s.AttributeClass));

                                        if (httpVerbAttributeTypeSymbolAbleToModify != null)
                                        {
                                            var attributeName = httpVerbAttributeTypeSymbolAbleToModify.AttributeClass.Name;
                                            actionMethodSymbols.TryAdd(
                                                (actionMethodSymbol,
                                                    attributeName.EndsWith("Attribute", StringComparison.Ordinal) ? attributeName.Remove(attributeName.Length - "Attribute".Length) : attributeName),
                                                placeholder);
                                        }
                                        else if (!actionMethodSymbol.GetAttributes().Any(s => s.AttributeClass.GetBaseTypes().Contains(httpMethodAttributeTypeSymbol)))
                                        {
                                            actionMethodNeedAddingHttpVerbAttributeSymbols.TryAdd(actionMethodSymbol, placeholder);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }, SymbolKind.NamedType);

                compilationStartAnalysisContext.RegisterCompilationEndAction(
                (CompilationAnalysisContext compilationAnalysisContext) =>
                {
                    if (usingValidateAntiForgeryAttribute && !hasGlobalAntiForgeryFilter && (actionMethodSymbols.Any() || actionMethodNeedAddingHttpVerbAttributeSymbols.Any()))
                    {
                        var visited = new HashSet<ISymbol>();
                        var results = new Dictionary<ISymbol, HashSet<ISymbol>>();

                        if (onAuthorizationMethodSymbols.Any())
                        {
                            foreach (var calleeMethod in inverseGraph.Keys)
                            {
                                if (calleeMethod.Name == "ValidateRequestAsync" &&
                                    (calleeMethod.ContainingType.AllInterfaces.Contains(iAntiforgeryTypeSymbol) ||
                                     SymbolEqualityComparer.Default.Equals(calleeMethod.ContainingType, iAntiforgeryTypeSymbol)))
                                {
                                    FindAllTheSpecifiedCalleeMethods(calleeMethod, visited, results);

                                    if (results.Values.Any(s => s.Any()))
                                    {
                                        return;
                                    }
                                }
                            }
                        }

                        foreach (var (methodSymbol, attributeName) in actionMethodSymbols.Keys)
                        {
                            compilationAnalysisContext.ReportDiagnostic(
                                methodSymbol.CreateDiagnostic(
                                    UseAutoValidateAntiforgeryTokenRule,
                                    methodSymbol.Name,
                                    attributeName));
                        }

                        foreach (var methodSymbol in actionMethodNeedAddingHttpVerbAttributeSymbols.Keys)
                        {
                            compilationAnalysisContext.ReportDiagnostic(
                                methodSymbol.CreateDiagnostic(
                                    MissHttpVerbAttributeRule,
                                    methodSymbol.Name));
                        }
                    }
                });

                // <summary>
                // Analyze the method to find all the specified methods that call it, in this case, the specified method symbols are in onAuthorizationAsyncMethodSymbols.
                // </summary>
                // <param name="methodSymbol">The symbol of the method to be analyzed</param>
                // <param name="visited">All the method has been analyzed</param>
                // <param name="results">The result is organized by &lt;method to be analyzed, specified methods calling it&gt;</param>
                void FindAllTheSpecifiedCalleeMethods(ISymbol methodSymbol, HashSet<ISymbol> visited, Dictionary<ISymbol, HashSet<ISymbol>> results)
                {
                    if (visited.Add(methodSymbol))
                    {
                        results.Add(methodSymbol, new HashSet<ISymbol>());

                        if (!inverseGraph.TryGetValue(methodSymbol, out var callingMethods))
                        {
                            Debug.Fail(methodSymbol.Name + " was not found in inverseGraph.");

                            return;
                        }

                        foreach (var child in callingMethods.Keys)
                        {
                            if (child is IMethodSymbol childMethodSymbol &&
                                onAuthorizationMethodSymbols.ContainsKey(childMethodSymbol))
                            {
                                results[methodSymbol].Add(child);
                            }

                            FindAllTheSpecifiedCalleeMethods(child, visited, results);

                            if (results.TryGetValue(child, out var result))
                            {
                                results[methodSymbol].UnionWith(result);
                            }
                            else
                            {
                                Debug.Fail(child.Name + " was not found in results.");
                            }
                        }
                    }
                }

                bool IsUsingAntiFogeryAttribute(ISymbol symbol)
                {
                    if (symbol.GetAttributes().Any(s => s_AntiForgeryAttributeRegex.IsMatch(s.AttributeClass.Name)))
                    {
                        usingValidateAntiForgeryAttribute = true;

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            });
        }
    }
}
