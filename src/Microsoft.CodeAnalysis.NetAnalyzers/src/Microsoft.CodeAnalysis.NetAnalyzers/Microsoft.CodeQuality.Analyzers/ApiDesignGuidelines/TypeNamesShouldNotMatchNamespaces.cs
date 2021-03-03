// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1724: Type names should not match namespaces
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class TypeNamesShouldNotMatchNamespacesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1724";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.TypeNamesShouldNotMatchNamespacesTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.TypeNamesShouldNotMatchNamespacesMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSystem = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.TypeNamesShouldNotMatchNamespacesMessageSystem), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.TypeNamesShouldNotMatchNamespacesDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDefault,
                                                                             DiagnosticCategory.Naming,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isReportedAtCompilationEnd: true);
        internal static DiagnosticDescriptor SystemRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSystem,
                                                                             DiagnosticCategory.Naming,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule, SystemRule);

        private static readonly object s_lock = new();
        private static ImmutableDictionary<string, string>? s_wellKnownSystemNamespaceTable;

        private static ImmutableDictionary<string, string> WellKnownSystemNamespaceTable
        {
            get
            {
                InitializeWellKnownSystemNamespaceTable();
                RoslynDebug.Assert(s_wellKnownSystemNamespaceTable != null);
                return s_wellKnownSystemNamespaceTable;
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

            context.RegisterCompilationStartAction(
                compilationStartAnalysisContext =>
                {
                    var externallyVisibleNamedTypes = new ConcurrentBag<INamedTypeSymbol>();

                    compilationStartAnalysisContext.RegisterSymbolAction(
                        symbolAnalysisContext =>
                        {
                            var namedType = (INamedTypeSymbol)symbolAnalysisContext.Symbol;
                            if (namedType.IsExternallyVisible())
                            {
                                externallyVisibleNamedTypes.Add(namedType);
                            }
                        }, SymbolKind.NamedType);

                    compilationStartAnalysisContext.RegisterCompilationEndAction(
                        compilationAnalysisContext =>
                        {
                            var namespaceNamesInCompilation = new ConcurrentBag<string>();
                            Compilation compilation = compilationAnalysisContext.Compilation;
                            AddNamespacesFromCompilation(namespaceNamesInCompilation, compilation.GlobalNamespace);

                            /* We construct a dictionary whose keys are all the components of all the namespace names in the compilation,
                             * and whose values are the namespace names of which the components are a part. For example, if the compilation
                             * includes namespaces A.B and C.D, the dictionary will map "A" to "A", "B" to "A.B", "C" to "C", and "D" to "C.D".
                             * When the analyzer encounters a type name that appears in a dictionary, it will emit a diagnostic, for instance,
                             * "Type name "D" conflicts with namespace name "C.D"".

                             * A component can occur in more than one namespace (for example, you might have namespaces "A" and "A.B".).
                             * In that case, we have to choose one namespace to report the diagnostic on. We want to make sure that this is
                             * deterministic (we don't want to complain about "A" in one compilation, and about "A.B" in the next).
                             * By calling ToImmutableSortedSet on the list of namespace names in the compilation, we ensure that
                             * we'll always construct the dictionary with the same set of keys.
                             */
                            var namespaceComponentToNamespaceNameDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            UpdateNamespaceTable(namespaceComponentToNamespaceNameDictionary, namespaceNamesInCompilation.ToImmutableSortedSet());

                            foreach (INamedTypeSymbol symbol in externallyVisibleNamedTypes)
                            {
                                string symbolName = symbol.Name;
                                if (WellKnownSystemNamespaceTable.ContainsKey(symbolName))
                                {
                                    compilationAnalysisContext.ReportDiagnostic(symbol.CreateDiagnostic(SystemRule, symbolName, WellKnownSystemNamespaceTable[symbolName]));
                                }
                                else if (namespaceComponentToNamespaceNameDictionary.ContainsKey(symbolName))
                                {
                                    compilationAnalysisContext.ReportDiagnostic(symbol.CreateDiagnostic(DefaultRule, symbolName, namespaceComponentToNamespaceNameDictionary[symbolName]));
                                }
                            }
                        });
                });
        }

        private static bool AddNamespacesFromCompilation(ConcurrentBag<string> namespaceNamesInCompilation, INamespaceSymbol @namespace)
        {
            bool hasExternallyVisibleType = false;

            foreach (INamespaceSymbol namespaceMember in @namespace.GetNamespaceMembers())
            {
                if (AddNamespacesFromCompilation(namespaceNamesInCompilation, namespaceMember))
                {
                    hasExternallyVisibleType = true;
                }
            }

            if (!hasExternallyVisibleType)
            {
                foreach (var type in @namespace.GetTypeMembers())
                {
                    if (type.IsExternallyVisible())
                    {
                        hasExternallyVisibleType = true;
                        break;
                    }
                }
            }

            if (hasExternallyVisibleType)
            {
                namespaceNamesInCompilation.Add(@namespace.ToDisplayString());
                return true;
            }

            return false;
        }

        private static void InitializeWellKnownSystemNamespaceTable()
        {
            if (s_wellKnownSystemNamespaceTable == null)
            {
                lock (s_lock)
                {
#pragma warning disable CA1508 // Avoid dead conditional code - https://github.com/dotnet/roslyn-analyzers/issues/3861
                    if (s_wellKnownSystemNamespaceTable == null)
#pragma warning restore CA1508 // Avoid dead conditional code
                    {
                        #region List of Well known System Namespaces
                        var wellKnownSystemNamespaces = new List<string>
                                {
                                    "Microsoft.CSharp",
                                    "Microsoft.SqlServer.Server",
                                    "Microsoft.VisualBasic",
                                    "Microsoft.Win32",
                                    "Microsoft.Win32.SafeHandles",
                                    "System",
                                    "System.CodeDom",
                                    "System.CodeDom.Compiler",
                                    "System.Collections",
                                    "System.Collections.Generic",
                                    "System.Collections.ObjectModel",
                                    "System.Collections.Specialized",
                                    "System.ComponentModel",
                                    "System.ComponentModel.Design",
                                    "System.ComponentModel.Design.Serialization",
                                    "System.Configuration",
                                    "System.Configuration.Assemblies",
                                    "System.Data",
                                    "System.Data.Common",
                                    "System.Data.Odbc",
                                    "System.Data.OleDb",
                                    "System.Data.Sql",
                                    "System.Data.SqlClient",
                                    "System.Data.SqlTypes",
                                    "System.Deployment.Internal",
                                    "System.Diagnostics",
                                    "System.Diagnostics.CodeAnalysis",
                                    "System.Diagnostics.SymbolStore",
                                    "System.Drawing",
                                    "System.Drawing.Design",
                                    "System.Drawing.Drawing2D",
                                    "System.Drawing.Imaging",
                                    "System.Drawing.Printing",
                                    "System.Drawing.Text",
                                    "System.Globalization",
                                    "System.IO",
                                    "System.IO.Compression",
                                    "System.IO.IsolatedStorage",
                                    "System.IO.Ports",
                                    "System.Media",
                                    "System.Net",
                                    "System.Net.Cache",
                                    "System.Net.Configuration",
                                    "System.Net.Mail",
                                    "System.Net.Mime",
                                    "System.Net.NetworkInformation",
                                    "System.Net.Security",
                                    "System.Net.Sockets",
                                    "System.Reflection",
                                    "System.Reflection.Emit",
                                    "System.Resources",
                                    "System.Runtime",
                                    "System.Runtime.CompilerServices",
                                    "System.Runtime.ConstrainedExecution",
                                    "System.Runtime.Hosting",
                                    "System.Runtime.InteropServices",
                                    "System.Runtime.InteropServices.ComTypes",
                                    "System.Runtime.InteropServices.Expando",
                                    "System.Runtime.Remoting",
                                    "System.Runtime.Remoting.Activation",
                                    "System.Runtime.Remoting.Channels",
                                    "System.Runtime.Remoting.Contexts",
                                    "System.Runtime.Remoting.Lifetime",
                                    "System.Runtime.Remoting.Messaging",
                                    "System.Runtime.Remoting.Metadata",
                                    "System.Runtime.Remoting.Metadata.W3cXsd2001",
                                    "System.Runtime.Remoting.Proxies",
                                    "System.Runtime.Remoting.Services",
                                    "System.Runtime.Serialization",
                                    "System.Runtime.Serialization.Formatters",
                                    "System.Runtime.Serialization.Formatters.Binary",
                                    "System.Runtime.Versioning",
                                    "System.Security",
                                    "System.Security.AccessControl",
                                    "System.Security.Authentication",
                                    "System.Security.Cryptography",
                                    "System.Security.Cryptography.X509Certificates",
                                    "System.Security.Permissions",
                                    "System.Security.Policy",
                                    "System.Security.Principal",
                                    "System.Text",
                                    "System.Text.RegularExpressions",
                                    "System.Threading",
                                    "System.Timers",
                                    "System.Web",
                                    "System.Web.Caching",
                                    "System.Web.Compilation",
                                    "System.Web.Configuration",
                                    "System.Web.Configuration.Internal",
                                    "System.Web.Handlers",
                                    "System.Web.Hosting",
                                    "System.Web.Mail",
                                    "System.Web.Management",
                                    "System.Web.Profile",
                                    "System.Web.Security",
                                    "System.Web.SessionState",
                                    "System.Web.UI",
                                    "System.Web.UI.Adapters",
                                    "System.Web.UI.HtmlControls",
                                    "System.Web.UI.WebControls",
                                    "System.Web.UI.WebControls.Adapters",
                                    "System.Web.UI.WebControls.WebParts",
                                    "System.Web.Util",
                                    "System.Windows.Forms",
                                    "System.Windows.Forms.ComponentModel.Com2Interop",
                                    "System.Windows.Forms.Design",
                                    "System.Windows.Forms.Layout",
                                    "System.Windows.Forms.PropertyGridInternal",
                                    "System.Windows.Forms.VisualStyles",
                                    "System.Xml",
                                    "System.Xml.Schema",
                                    "System.Xml.Serialization",
                                    "System.Xml.Serialization.Advanced",
                                    "System.Xml.Serialization.Configuration",
                                    "System.Xml.XPath",
                                    "System.Xml.Xsl"
                                };
                        #endregion

                        var wellKnownSystemNamespaceTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        UpdateNamespaceTable(wellKnownSystemNamespaceTable, wellKnownSystemNamespaces);
                        s_wellKnownSystemNamespaceTable = wellKnownSystemNamespaceTable.ToImmutableDictionary();
                    }
                }
            }
        }

        private static void UpdateNamespaceTable(Dictionary<string, string> namespaceTable, IList<string> namespaces)
        {
            if (namespaces == null)
            {
                return;
            }

            foreach (string namespaceName in namespaces)
            {
                UpdateNamespaceTable(namespaceTable, namespaceName);
            }
        }

        private static void UpdateNamespaceTable(Dictionary<string, string> namespaceTable, string namespaceName)
        {
            foreach (string word in namespaceName.Split('.'))
            {
                if (!namespaceTable.ContainsKey(word))
                    namespaceTable.Add(word, namespaceName);
            }
        }
    }
}
