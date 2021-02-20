// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetFramework.Analyzers.Helpers
{
    public static class SecurityDiagnosticHelpers
    {
        public static bool IsXslCompiledTransformLoad([NotNullWhen(returnValue: true)] this IMethodSymbol? method, CompilationSecurityTypes xmlTypes)
            => method != null
                && xmlTypes.XslCompiledTransform != null
                && method.MatchMethodByName(xmlTypes.XslCompiledTransform, SecurityMemberNames.Load);

        public static bool IsXmlDocumentCtorDerived([NotNullWhen(returnValue: true)] this IMethodSymbol? method, CompilationSecurityTypes xmlTypes)
        {
            return method != null
                && xmlTypes.XmlDocument != null
                && method.MatchMethodDerivedByName(xmlTypes.XmlDocument, WellKnownMemberNames.InstanceConstructorName);
        }

        public static bool IsXmlDocumentXmlResolverProperty([NotNullWhen(returnValue: true)] IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return IsSpecifiedProperty(symbol, xmlTypes.XmlDocument, SecurityMemberNames.XmlResolver);
        }

        public static bool IsXmlTextReaderCtorDerived([NotNullWhen(returnValue: true)] this IMethodSymbol? method, CompilationSecurityTypes xmlTypes)
        {
            return method != null
                && xmlTypes.XmlTextReader != null
                && method.MatchMethodDerivedByName(xmlTypes.XmlTextReader, WellKnownMemberNames.InstanceConstructorName);
        }

        public static bool IsXmlTextReaderXmlResolverPropertyDerived([NotNullWhen(returnValue: true)] IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return IsSpecifiedPropertyDerived(symbol, xmlTypes.XmlTextReader, SecurityMemberNames.XmlResolver);
        }

        public static bool IsXmlTextReaderDtdProcessingPropertyDerived([NotNullWhen(returnValue: true)] IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return IsSpecifiedPropertyDerived(symbol, xmlTypes.XmlTextReader, SecurityMemberNames.DtdProcessing);
        }

        public static bool IsXmlTextReaderXmlResolverProperty([NotNullWhen(returnValue: true)] IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return IsSpecifiedProperty(symbol, xmlTypes.XmlTextReader, SecurityMemberNames.XmlResolver);
        }

        public static bool IsXmlTextReaderDtdProcessingProperty([NotNullWhen(returnValue: true)] IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return IsSpecifiedProperty(symbol, xmlTypes.XmlTextReader, SecurityMemberNames.DtdProcessing);
        }

        public static bool IsXmlReaderSettingsCtor([NotNullWhen(returnValue: true)] this IMethodSymbol? method, CompilationSecurityTypes xmlTypes)
        {
            return method != null
                && xmlTypes.XmlReaderSettings != null
                && method.MatchMethodByName(xmlTypes.XmlReaderSettings, WellKnownMemberNames.InstanceConstructorName);
        }

        public static bool IsXmlReaderSettingsXmlResolverProperty([NotNullWhen(returnValue: true)] IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return IsSpecifiedProperty(symbol, xmlTypes.XmlReaderSettings, SecurityMemberNames.XmlResolver);
        }

        public static bool IsXmlReaderSettingsDtdProcessingProperty([NotNullWhen(returnValue: true)] IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return IsSpecifiedProperty(symbol, xmlTypes.XmlReaderSettings, SecurityMemberNames.DtdProcessing);
        }

        public static bool IsXmlReaderSettingsMaxCharactersFromEntitiesProperty([NotNullWhen(returnValue: true)] IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return IsSpecifiedProperty(symbol, xmlTypes.XmlReaderSettings, SecurityMemberNames.MaxCharactersFromEntities);
        }

        public static bool IsXsltSettingsCtor([NotNullWhen(returnValue: true)] this IMethodSymbol? method, CompilationSecurityTypes xmlTypes)
            => method != null
                && xmlTypes.XsltSettings != null
                && method.MatchMethodByName(xmlTypes.XsltSettings, WellKnownMemberNames.InstanceConstructorName);

        public static bool IsXsltSettingsTrustedXsltProperty([NotNullWhen(returnValue: true)] this IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
            => IsSpecifiedProperty(symbol, xmlTypes.XsltSettings, SecurityMemberNames.TrustedXslt);

        public static bool IsXsltSettingsDefaultProperty([NotNullWhen(returnValue: true)] this IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
            => IsSpecifiedProperty(symbol, xmlTypes.XsltSettings, SecurityMemberNames.Default);

        public static bool IsXsltSettingsEnableDocumentFunctionProperty([NotNullWhen(returnValue: true)] this IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
            => IsSpecifiedProperty(symbol, xmlTypes.XsltSettings, SecurityMemberNames.EnableDocumentFunction);

        public static bool IsXsltSettingsEnableScriptProperty([NotNullWhen(returnValue: true)] this IPropertySymbol? symbol, CompilationSecurityTypes xmlTypes)
            => IsSpecifiedProperty(symbol, xmlTypes.XsltSettings, SecurityMemberNames.EnableScript);

        public static bool IsXmlResolverType([NotNullWhen(returnValue: true)] ITypeSymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return symbol != null
                && symbol.DerivesFrom(xmlTypes.XmlResolver, baseTypesOnly: true);
        }

        public static bool IsXmlSecureResolverType([NotNullWhen(returnValue: true)] this ITypeSymbol? symbol, CompilationSecurityTypes xmlTypes)
            => symbol != null
                && symbol.DerivesFrom(xmlTypes.XmlSecureResolver, baseTypesOnly: true);

        public static bool IsXsltSettingsType([NotNullWhen(returnValue: true)] ITypeSymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return Equals(symbol, xmlTypes.XsltSettings);
        }

        public static bool IsXmlReaderSettingsType([NotNullWhen(returnValue: true)] ITypeSymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return Equals(symbol, xmlTypes.XmlReaderSettings);
        }

        public static int GetXmlResolverParameterIndex(this IMethodSymbol? method, CompilationSecurityTypes xmlTypes)
            => GetSpecifiedParameterIndex(method, xmlTypes, IsXmlResolverType);

        public static int GetXsltSettingsParameterIndex(this IMethodSymbol? method, CompilationSecurityTypes xmlTypes)
            => GetSpecifiedParameterIndex(method, xmlTypes, IsXsltSettingsType);

        public static int GetXmlReaderSettingsParameterIndex(this IMethodSymbol? method, CompilationSecurityTypes xmlTypes)
        {
            return GetSpecifiedParameterIndex(method, xmlTypes, IsXmlReaderSettingsType);
        }

        public static bool IsXmlReaderType([NotNullWhen(returnValue: true)] ITypeSymbol? symbol, CompilationSecurityTypes xmlTypes)
        {
            return symbol != null
                && symbol.DerivesFrom(xmlTypes.XmlReader, baseTypesOnly: true);
        }

        public static int HasXmlReaderParameter(IMethodSymbol? method, CompilationSecurityTypes xmlTypes)
        {
            return GetSpecifiedParameterIndex(method, xmlTypes, IsXmlReaderType);
        }

        public static bool IsExpressionEqualsNull([NotNullWhen(returnValue: true)] IOperation? operation)
        {
            return operation is ILiteralOperation literal && literal.HasNullConstantValue();
        }

        public static bool IsExpressionEqualsDtdProcessingParse([NotNullWhen(returnValue: true)] IOperation? operation)
        {
            return operation is IFieldReferenceOperation enumRef && enumRef.HasConstantValue(2); // DtdProcessing.Parse
        }

        public static bool IsExpressionEqualsIntZero([NotNullWhen(returnValue: true)] IOperation? operation)
        {

            if (operation is not ILiteralOperation literal || !literal.ConstantValue.HasValue)
            {
                return false;
            }

            return literal.HasConstantValue(0);
        }

        private static bool IsSpecifiedProperty([NotNullWhen(returnValue: true)] IPropertySymbol? symbol, [NotNullWhen(returnValue: true)] INamedTypeSymbol? namedType, string propertyName)
        {
            if (symbol != null && namedType != null)
            {
                return symbol.MatchPropertyByName(namedType, propertyName);
            }

            return false;
        }

        private static bool IsSpecifiedPropertyDerived([NotNullWhen(returnValue: true)] IPropertySymbol? symbol, [NotNullWhen(returnValue: true)] INamedTypeSymbol? namedType, string propertyName)
        {
            if (symbol != null && namedType != null)
            {
                return symbol.MatchPropertyDerivedByName(namedType, propertyName);
            }

            return false;
        }

        private static int GetSpecifiedParameterIndex(IMethodSymbol? method, CompilationSecurityTypes xmlTypes, Func<ITypeSymbol, CompilationSecurityTypes, bool> func)
        {
            int index = -1;

            if (method == null)
            {
                return index;
            }
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                ITypeSymbol parameter = method.Parameters[i].Type;
                if (func(parameter, xmlTypes))
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        /// <summary>
        /// Determine whether a type (given by name) is actually declared in the expected assembly (also given by name)
        /// </summary>
        /// <remarks>
        /// This can be used to decide whether we are referencing the expected framework for a given type.
        /// For example, System.String exists in mscorlib for .NET Framework and System.Runtime for other framework (e.g. .NET Core).
        /// </remarks>
        public static bool? IsTypeDeclaredInExpectedAssembly([NotNullWhen(returnValue: true)] Compilation? compilation, string typeName, string assemblyName)
        {
            if (compilation == null)
            {
                return null;
            }

            INamedTypeSymbol? typeSymbol = compilation.GetOrCreateTypeByMetadataName(typeName);
            return typeSymbol?.ContainingAssembly.Identity.Name.Equals(assemblyName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Get non-empty class or method name which encloses the current syntax node
        /// </summary>
        /// <param name="current">Current syntax not to examine</param>
        /// <param name="model">The semantic model</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        public static string GetNonEmptyParentName(SyntaxNode current, SemanticModel model, CancellationToken cancellationToken)
        {
            while (current.Parent != null)
            {
                SyntaxNode parent = current.Parent;
                ISymbol sym = model.GetDeclaredSymbol(current, cancellationToken);

                switch (sym)
                {
                    case IMethodSymbol method:
                        return method.MethodKind == MethodKind.Ordinary ? method.Name : method.ContainingType.Name;
                    case INamedTypeSymbol namedType:
                        return namedType.Name;
                }

                current = parent;
            }

            return string.Empty;
        }

        /// <summary>
        /// Get class or method name which encloses the current symbol node
        /// </summary>
        public static string GetNonEmptyParentName(ISymbol symbol)
        {
            var current = symbol;
            while (current.ContainingSymbol != null)
            {
                switch (current)
                {
                    case IMethodSymbol method:
                        return method.MethodKind == MethodKind.Ordinary
                            ? method.Name
                            : method.ContainingType.Name;
                    case INamedTypeSymbol namedType:
                        return namedType.Name;
                }

                current = symbol.ContainingSymbol;
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the version of the target .NET framework of the compilation.
        /// </summary>
        /// <returns>
        /// Null if the target framenwork is not .NET Framework.
        /// </returns>
        /// <remarks>
        /// This method returns the assembly version of mscorlib for .NET Framework prior version 4.0.
        /// It is using API diff tool to compare new classes in different versions and decide which version it is referencing
        /// i.e. for .NET framework 3.5, the returned version would be 2.0.0.0.
        /// For .NET Framework 4.X, this method returns the actual framework version instead of assembly verison of mscorlib,
        /// i.e. for .NET framework 4.5.2, this method return 4.5.2 instead of 4.0.0.0.
        /// </remarks>
        public static Version? GetDotNetFrameworkVersion([NotNullWhen(returnValue: true)] Compilation? compilation)
        {
            if (compilation == null || !IsTypeDeclaredInExpectedAssembly(compilation, "System.String", "mscorlib").GetValueOrDefault())
            {
                return null;
            }

            IAssemblySymbol mscorlibAssembly = compilation.GetSpecialType(SpecialType.System_String).ContainingAssembly;
            if (mscorlibAssembly.Identity.Version.Major < 4)
            {
                return mscorlibAssembly.Identity.Version;
            }

            if (mscorlibAssembly.GetTypeByMetadataName(WellKnownTypeNames.SystemAppContext) != null)
            {
                return new Version(4, 6);
            }
            INamedTypeSymbol typeSymbol = mscorlibAssembly.GetTypeByMetadataName(WellKnownTypeNames.SystemIOUnmanagedMemoryStream);
            if (!typeSymbol.GetMembers("FlushAsync").IsEmpty)
            {
                return new Version(4, 5, 2);
            }
            typeSymbol = mscorlibAssembly.GetTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsTracingEventSource);
            if (typeSymbol != null)
            {
                return typeSymbol.GetMembers("CurrentThreadActivityId").IsEmpty ? new Version(4, 5) : new Version(4, 5, 1);
            }
            return new Version(4, 0);
        }

        public static LocalizableResourceString GetLocalizableResourceString(string resourceName)
        {
            return new LocalizableResourceString(resourceName, MicrosoftNetFrameworkAnalyzersResources.ResourceManager, typeof(MicrosoftNetFrameworkAnalyzersResources));
        }

        public static LocalizableResourceString GetLocalizableResourceString(string resourceName, params string[] formatArguments)
        {
            return new LocalizableResourceString(resourceName, MicrosoftNetFrameworkAnalyzersResources.ResourceManager, typeof(MicrosoftNetFrameworkAnalyzersResources), formatArguments);
        }
    }
}
