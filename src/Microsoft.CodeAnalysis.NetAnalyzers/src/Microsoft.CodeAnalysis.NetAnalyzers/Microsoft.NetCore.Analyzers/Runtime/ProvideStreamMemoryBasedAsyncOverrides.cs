// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1840: Reports a diagnostic if a class that directly subclasses <see cref="System.IO.Stream"/> overrides 
    /// <see cref="System.IO.Stream.ReadAsync(byte[], int, int)"/> and/or <see cref="System.IO.Stream.WriteAsync(byte[], int, int)"/>, 
    /// and does not override the corrasponding memory-based version.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class ProvideStreamMemoryBasedAsyncOverrides : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1840";

        private static readonly LocalizableString s_localizableTitle = CreateResource(nameof(Resx.ProvideStreamMemoryBasedAsyncOverridesTitle));
        private static readonly LocalizableString s_localizableMessage = CreateResource(nameof(Resx.ProvideStreamMemoryBasedAsyncOverridesMessage));
        private static readonly LocalizableString s_localizableDescription = CreateResource(nameof(Resx.ProvideStreamMemoryBasedAsyncOverridesDescription));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        private const string ReadAsyncName = "ReadAsync";
        private const string WriteAsyncName = "WriteAsync";

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var compilation = context.Compilation;

            if (!TryGetRequiredSymbols(compilation, out RequiredSymbols symbols))
                return;

            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);

            void AnalyzeNamedType(SymbolAnalysisContext context)
            {
                var type = (INamedTypeSymbol)context.Symbol;

                //  We only report a diagnostic if the type directly subclasses stream. We don't report diagnostics
                //  if there are any bases in the middle.

                if (!symbols.StreamType.Equals(type.BaseType, SymbolEqualityComparer.Default))
                    return;

                IMethodSymbol? readAsyncArrayOverride = GetSymbolForOverridingMethod(type, symbols.ReadAsyncArrayMethod);
                IMethodSymbol? readAsyncMemoryOverride = GetSymbolForOverridingMethod(type, symbols.ReadAsyncMemoryMethod);
                IMethodSymbol? writeAsyncArrayOverride = GetSymbolForOverridingMethod(type, symbols.WriteAsyncArrayMethod);
                IMethodSymbol? writeAsyncMemoryOverride = GetSymbolForOverridingMethod(type, symbols.WriteAsyncMemoryMethod);

                //  For both ReadAsync and WriteAsync, if the array-based form is overridden and the memory-based
                //  form is not, we report a diagnostic. We report separate diagnostics for ReadAsync and WriteAsync. 

                if (readAsyncArrayOverride is not null && readAsyncMemoryOverride is null)
                {
                    var diagnostic = CreateDiagnostic(type, readAsyncArrayOverride, symbols.ReadAsyncMemoryMethod);
                    context.ReportDiagnostic(diagnostic);
                }

                if (writeAsyncArrayOverride is not null && writeAsyncMemoryOverride is null)
                {
                    var diagnostic = CreateDiagnostic(type, writeAsyncArrayOverride, symbols.WriteAsyncMemoryMethod);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            static Diagnostic CreateDiagnostic(INamedTypeSymbol violatingType, IMethodSymbol arrayBasedOverride, IMethodSymbol memoryBasedMethod)
            {
                RoslynDebug.Assert(arrayBasedOverride.OverriddenMethod is not null);

                //  We want to underline the name of the violating type in the class declaration. If the violating type
                //  is a partial class, we underline all partial declarations.

                return Diagnostic.Create(
                    Rule,
                    violatingType.Locations[0],
                    violatingType.Locations.Skip(1),
                    violatingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    arrayBasedOverride.OverriddenMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    memoryBasedMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }
        }

        private static bool TryGetRequiredSymbols(Compilation compilation, out RequiredSymbols requiredSymbols)
        {
            var int32Type = compilation.GetSpecialType(SpecialType.System_Int32);
            var byteType = compilation.GetSpecialType(SpecialType.System_Byte);
            var byteArrayType = compilation.CreateArrayTypeSymbol(byteType);
            var memoryOfByteType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemory1)?.Construct(byteType);
            var readOnlyMemoryOfByteType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlyMemory1)?.Construct(byteType);
            var streamType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOStream);
            var cancellationTokenType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationToken);

            if (memoryOfByteType is null || readOnlyMemoryOfByteType is null || streamType is null || cancellationTokenType is null)
            {
                requiredSymbols = default;
                return false;
            }

            var readAsyncArrayMethod = GetOverload(streamType, ReadAsyncName, byteArrayType, int32Type, int32Type, cancellationTokenType);
            var readAsyncMemoryMethod = GetOverload(streamType, ReadAsyncName, memoryOfByteType, cancellationTokenType);
            var writeAsyncArrayMethod = GetOverload(streamType, WriteAsyncName, byteArrayType, int32Type, int32Type, cancellationTokenType);
            var writeAsyncMemoryMethod = GetOverload(streamType, WriteAsyncName, readOnlyMemoryOfByteType, cancellationTokenType);

            if (readAsyncArrayMethod is null || readAsyncMemoryMethod is null || writeAsyncArrayMethod is null || writeAsyncMemoryMethod is null)
            {
                requiredSymbols = default;
                return false;
            }

            requiredSymbols = new RequiredSymbols(
                streamType, memoryOfByteType, readOnlyMemoryOfByteType,
                readAsyncArrayMethod, readAsyncMemoryMethod,
                writeAsyncArrayMethod, writeAsyncMemoryMethod);
            return true;
        }

        private static IMethodSymbol? GetOverload(ITypeSymbol containingType, string methodName, params ITypeSymbol[] argumentTypes)
        {
            return containingType.GetMembers(methodName)
                .SingleOrDefault(symbol => symbol is IMethodSymbol m && IsMatch(m, argumentTypes)) as IMethodSymbol;

            static bool IsMatch(IMethodSymbol method, ITypeSymbol[] argumentTypes)
            {
                if (method.Parameters.Length != argumentTypes.Length)
                    return false;

                for (int index = 0; index < argumentTypes.Length; ++index)
                {
                    if (!argumentTypes[index].Equals(method.Parameters[index].Type, SymbolEqualityComparer.Default))
                        return false;
                }

                return true;
            }
        }

        //  If the specified type overrides the specified method on its immediate base class, returns the method symbol representing the 
        //  overriding method. Returns null if the specified type does not override the specified method.
        private static IMethodSymbol? GetSymbolForOverridingMethod(ITypeSymbol derivedType, IMethodSymbol overriddenMethod)
        {
            RoslynDebug.Assert(derivedType.BaseType.Equals(overriddenMethod.ContainingType, SymbolEqualityComparer.Default));

            return derivedType.GetMembers(overriddenMethod.Name)
                .SingleOrDefault(x =>
                {
                    return x.IsOverride && overriddenMethod.Equals(x.GetOverriddenMember(), SymbolEqualityComparer.Default);
                }) as IMethodSymbol;
        }

        //  We will not be doing any comparisons on this type.
#pragma warning disable CA1815 // Override equals and operator equals on value types
        private readonly struct RequiredSymbols
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public RequiredSymbols(
                ITypeSymbol streamType, ITypeSymbol memoryOfByteType, ITypeSymbol readOnlyMemoryOfByteType,
                IMethodSymbol readAsyncArrayMethod, IMethodSymbol readAsyncMemoryMethod,
                IMethodSymbol writeAsyncArrayMethod, IMethodSymbol writeAsyncMemoryMethod)
            {
                StreamType = streamType;
                MemoryOfByteType = memoryOfByteType;
                ReadOnlyMemoryOfByteType = readOnlyMemoryOfByteType;
                ReadAsyncArrayMethod = readAsyncArrayMethod;
                ReadAsyncMemoryMethod = readAsyncMemoryMethod;
                WriteAsyncArrayMethod = writeAsyncArrayMethod;
                WriteAsyncMemoryMethod = writeAsyncMemoryMethod;
            }

            public ITypeSymbol StreamType { get; }
            public ITypeSymbol MemoryOfByteType { get; }
            public ITypeSymbol ReadOnlyMemoryOfByteType { get; }
            public IMethodSymbol ReadAsyncArrayMethod { get; }
            public IMethodSymbol ReadAsyncMemoryMethod { get; }
            public IMethodSymbol WriteAsyncArrayMethod { get; }
            public IMethodSymbol WriteAsyncMemoryMethod { get; }
        }

        private static LocalizableString CreateResource(string resourceName)
        {
            return new LocalizableResourceString(resourceName, Resx.ResourceManager, typeof(Resx));
        }
    }
}
