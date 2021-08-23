// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class IdentifiersShouldDifferByMoreThanCaseAnalyzer : DiagnosticAnalyzer
    {
        public const string RuleId = "CA1708";
        public const string Namespace = "Namespaces";
        public const string Type = "Types";
        public const string Member = "Members";
        public const string Parameter = "Parameters of";

        private static readonly LocalizableResourceString s_localizableTitle = new(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldDifferByMoreThanCaseTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableResourceString s_localizableMessage = new(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldDifferByMoreThanCaseMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableResourceString s_localizableDescription = new(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldDifferByMoreThanCaseDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMessage,
                                                                                      DiagnosticCategory.Naming,
                                                                                      RuleLevel.IdeHidden_BulkConfigurable,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: true,
                                                                                      isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationAction(AnalyzeCompilation);
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            IEnumerable<INamespaceSymbol> globalNamespaces = context.Compilation.GlobalNamespace.GetNamespaceMembers()
                .Where(item => Equals(item.ContainingAssembly, context.Compilation.Assembly));

            IEnumerable<INamedTypeSymbol> globalTypes = context.Compilation.GlobalNamespace.GetTypeMembers().Where(item =>
                    Equals(item.ContainingAssembly, context.Compilation.Assembly) &&
                    MatchesConfiguredVisibility(item, context.Options, context.Compilation));

            CheckTypeNames(globalTypes, context);
            CheckNamespaceMembers(globalNamespaces, context);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Do not descent into non-publicly visible types by default
            // Note: This is the behavior of FxCop, it might be more correct to descend into internal but not private
            // types because "InternalsVisibleTo" could be set. But it might be bad for users to start seeing warnings
            // where they previously did not from FxCop.
            // Note that end user can now override this default behavior via options.
            if (!context.Options.MatchesConfiguredVisibility(Rule, namedTypeSymbol, context.Compilation))
            {
                return;
            }

            // Get externally visible members in the given type
            IEnumerable<ISymbol> members = namedTypeSymbol.GetMembers()
                                                          .Where(item => !item.IsAccessorMethod() &&
                                                                         MatchesConfiguredVisibility(item, context.Options, context.Compilation));

            if (members.Any())
            {
                // Check parameters names of externally visible members with parameters
                CheckParameterMembers(members, context.ReportDiagnostic);

                // Check names of externally visible type members and their members
                CheckTypeMembers(members, context.ReportDiagnostic);
            }
        }

        private static void CheckNamespaceMembers(IEnumerable<INamespaceSymbol> namespaces, CompilationAnalysisContext context)
        {
            HashSet<INamespaceSymbol> excludedNamespaces = new HashSet<INamespaceSymbol>();
            foreach (INamespaceSymbol @namespace in namespaces)
            {
                // Get all the potentially externally visible types in the namespace
                IEnumerable<INamedTypeSymbol> typeMembers = @namespace.GetTypeMembers().Where(item =>
                    Equals(item.ContainingAssembly, context.Compilation.Assembly) &&
                    MatchesConfiguredVisibility(item, context.Options, context.Compilation));

                if (typeMembers.Any())
                {
                    CheckTypeNames(typeMembers, context);
                }
                else
                {
                    // If the namespace does not contain any externally visible types then exclude it from name check
                    excludedNamespaces.Add(@namespace);
                }

                IEnumerable<INamespaceSymbol> namespaceMembers = @namespace.GetNamespaceMembers();
                if (namespaceMembers.Any())
                {
                    CheckNamespaceMembers(namespaceMembers, context);

                    // If there is a child namespace that has externally visible types, then remove the parent namespace from exclusion list
                    if (namespaceMembers.Any(item => !excludedNamespaces.Contains(item)))
                    {
                        excludedNamespaces.Remove(@namespace);
                    }
                }
            }

            // Before name check, remove all namespaces that don't contain externally visible types in current scope
            namespaces = namespaces.Where(item => !excludedNamespaces.Contains(item));

            CheckNamespaceNames(namespaces, context);
        }

        private static void CheckTypeMembers(IEnumerable<ISymbol> members, Action<Diagnostic> addDiagnostic)
        {
            // If there is only one member, then return
            if (!members.Skip(1).Any())
            {
                return;
            }

            using var overloadsToSkip = PooledHashSet<ISymbol>.GetInstance();
            using var membersByName = PooledDictionary<string, PooledHashSet<ISymbol>>.GetInstance(StringComparer.OrdinalIgnoreCase);
            foreach (var member in members)
            {
                // Ignore constructors, indexers, operators and destructors for name check
                if (member.IsConstructor() ||
                    member.IsDestructor() ||
                    member.IsIndexer() ||
                    member.IsUserDefinedOperator() ||
                    overloadsToSkip.Contains(member))
                {
                    continue;
                }

                var name = DiagnosticHelpers.GetMemberName(member);
                if (!membersByName.TryGetValue(name, out var membersWithName))
                {
                    membersWithName = PooledHashSet<ISymbol>.GetInstance();
                    membersByName[name] = membersWithName;
                }

                membersWithName.Add(member);

                if (member is IMethodSymbol method)
                {
                    foreach (var overload in method.GetOverloads())
                    {
                        overloadsToSkip.Add(overload);
                    }
                }
            }

            foreach (var (name, membersWithName) in membersByName)
            {
                if (membersWithName.Count > 1 &&
                    !membersWithName.All(m => m.IsOverride))
                {
                    ISymbol symbol = membersWithName.First().ContainingSymbol;
                    addDiagnostic(symbol.CreateDiagnostic(Rule, Member, GetSymbolDisplayString(membersWithName)));
                }

                membersWithName.Dispose();
            }
        }

        private static void CheckParameterMembers(IEnumerable<ISymbol> members, Action<Diagnostic> addDiagnostic)
        {
            foreach (var member in members)
            {
                if (IsViolatingMember(member) || IsViolatingDelegate(member))
                {
                    addDiagnostic(member.CreateDiagnostic(Rule, Parameter, member.ToDisplayString()));
                }
            }

            return;

            // Local functions
            static bool IsViolatingMember(ISymbol member)
                => member.ContainingType.DelegateInvokeMethod == null &&
                   HasViolatingParameters(member);

            static bool IsViolatingDelegate(ISymbol member)
                => member is INamedTypeSymbol typeSymbol &&
                   typeSymbol.DelegateInvokeMethod != null &&
                   HasViolatingParameters(typeSymbol.DelegateInvokeMethod);
        }

        #region NameCheck Methods

        private static bool HasViolatingParameters(ISymbol symbol)
        {
            var parameters = symbol.GetParameters();

            // We only analyze symbols with more then one parameter.
            if (parameters.Length <= 1)
            {
                return false;
            }

            using var uniqueNames = PooledHashSet<string>.GetInstance(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in parameters)
            {
                if (!uniqueNames.Add(parameter.Name))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CheckTypeNames(IEnumerable<INamedTypeSymbol> types, CompilationAnalysisContext context)
        {
            // If there is only one type, then return
            if (!types.Skip(1).Any())
            {
                return;
            }

            using var typesByName = PooledDictionary<string, PooledHashSet<ISymbol>>.GetInstance(StringComparer.OrdinalIgnoreCase);
            foreach (var type in types)
            {
                var name = DiagnosticHelpers.GetMemberName(type);
                if (!typesByName.TryGetValue(name, out var typesWithName))
                {
                    typesWithName = PooledHashSet<ISymbol>.GetInstance();
                    typesByName[name] = typesWithName;
                }

                typesWithName.Add(type);
            }

            foreach (var (_, typesWithName) in typesByName)
            {
                if (typesWithName.Count > 1)
                {
                    context.ReportNoLocationDiagnostic(Rule, Type, GetSymbolDisplayString(typesWithName));
                }

                typesWithName.Dispose();
            }
        }

        private static void CheckNamespaceNames(IEnumerable<INamespaceSymbol> namespaces, CompilationAnalysisContext context)
        {
            // If there is only one namespace, then return
            if (!namespaces.Skip(1).Any())
            {
                return;
            }

            using var namespacesByName = PooledDictionary<string, PooledHashSet<ISymbol>>.GetInstance(StringComparer.OrdinalIgnoreCase);
            foreach (var namespaceSym in namespaces)
            {
                var name = namespaceSym.ToDisplayString();
                if (!namespacesByName.TryGetValue(name, out var namespacesWithName))
                {
                    namespacesWithName = PooledHashSet<ISymbol>.GetInstance();
                    namespacesByName[name] = namespacesWithName;
                }

                namespacesWithName.Add(namespaceSym);
            }

            foreach (var (_, namespacesWithName) in namespacesByName)
            {
                if (namespacesWithName.Count > 1)
                {
                    context.ReportNoLocationDiagnostic(Rule, Namespace, GetSymbolDisplayString(namespacesWithName));
                }

                namespacesWithName.Dispose();
            }
        }

        #endregion

        #region Helper Methods

        private static string GetSymbolDisplayString(PooledHashSet<ISymbol> group)
        {
            return string.Join(", ", group.Select(s => s.ToDisplayString()).OrderBy(k => k, StringComparer.Ordinal));
        }

        public static bool MatchesConfiguredVisibility(ISymbol symbol, AnalyzerOptions options, Compilation compilation)
        {
            var defaultAllowedVisibilties = SymbolVisibilityGroup.Public | SymbolVisibilityGroup.Internal;
            var allowedVisibilities = options.GetSymbolVisibilityGroupOption(Rule, symbol, compilation, defaultAllowedVisibilties);
            return allowedVisibilities.Contains(symbol.GetResultantVisibility());
        }

        #endregion
    }
}
