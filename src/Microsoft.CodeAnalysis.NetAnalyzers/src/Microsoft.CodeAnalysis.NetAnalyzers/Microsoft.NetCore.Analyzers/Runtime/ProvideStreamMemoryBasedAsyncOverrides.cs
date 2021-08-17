// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
        internal const string RuleId = "CA1844";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(Resx.ProvideStreamMemoryBasedAsyncOverridesTitle), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(Resx.ProvideStreamMemoryBasedAsyncOverridesMessage), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(Resx.ProvideStreamMemoryBasedAsyncOverridesDescription), Resx.ResourceManager, typeof(Resx));

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

        private const string ReadAsyncName = nameof(System.IO.Stream.ReadAsync);
        private const string WriteAsyncName = nameof(System.IO.Stream.WriteAsync);

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

            return;

            // Local functions.
            void AnalyzeNamedType(SymbolAnalysisContext context)
            {
                var type = (INamedTypeSymbol)context.Symbol;

                //  We only report a diagnostic if the type directly subclasses stream. We don't report diagnostics
                //  if there are any bases in the middle.

                if (!symbols.StreamType.Equals(type.BaseType, SymbolEqualityComparer.Default))
                    return;

                //  We use 'FirstOrDefault' because there could be multiple overrides for the same method if
                //  there are compiler errors.

                IMethodSymbol? readAsyncArrayOverride = GetOverridingMethodSymbols(type, symbols.ReadAsyncArrayMethod).FirstOrDefault();
                IMethodSymbol? readAsyncMemoryOverride = GetOverridingMethodSymbols(type, symbols.ReadAsyncMemoryMethod).FirstOrDefault();
                IMethodSymbol? writeAsyncArrayOverride = GetOverridingMethodSymbols(type, symbols.WriteAsyncArrayMethod).FirstOrDefault();
                IMethodSymbol? writeAsyncMemoryOverride = GetOverridingMethodSymbols(type, symbols.WriteAsyncMemoryMethod).FirstOrDefault();

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

                return violatingType.CreateDiagnostic(
                    Rule,
                    violatingType.Name,
                    arrayBasedOverride.Name,
                    memoryBasedMethod.Name);
            }
        }

        private static bool TryGetRequiredSymbols(Compilation compilation, out RequiredSymbols requiredSymbols)
        {
            var int32Type = compilation.GetSpecialType(SpecialType.System_Int32);
            var byteType = compilation.GetSpecialType(SpecialType.System_Byte);

            if (int32Type is null || byteType is null)
            {
                requiredSymbols = default;
                return false;
            }

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

            //  Even though framework types should never contain compiler errors, we still use 'FirstOrDefault' to avoid having the 
            //  analyzer crash if it runs against the source code for 'System.Private.CoreLib', which could be malformed.

            var readAsyncArrayMethod = GetOverloads(streamType, ReadAsyncName, byteArrayType, int32Type, int32Type, cancellationTokenType).FirstOrDefault();
            var readAsyncMemoryMethod = GetOverloads(streamType, ReadAsyncName, memoryOfByteType, cancellationTokenType).FirstOrDefault();
            var writeAsyncArrayMethod = GetOverloads(streamType, WriteAsyncName, byteArrayType, int32Type, int32Type, cancellationTokenType).FirstOrDefault();
            var writeAsyncMemoryMethod = GetOverloads(streamType, WriteAsyncName, readOnlyMemoryOfByteType, cancellationTokenType).FirstOrDefault();

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

        //  There could be more than one overload with an exact match if there are compiler errors.
        private static ImmutableArray<IMethodSymbol> GetOverloads(ITypeSymbol containingType, string methodName, params ITypeSymbol[] argumentTypes)
        {
            return containingType.GetMembers(methodName)
                .OfType<IMethodSymbol>()
                .WhereAsArray(m => IsMatch(m, argumentTypes));

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

        /// <summary>
        /// If <paramref name="derivedType"/> overrides <paramref name="overriddenMethod"/> on its immediate base class, returns the <see cref="IMethodSymbol"/>
        /// for the overriding method. Returns null if <paramref name="derivedType"/> does not override <paramref name="overriddenMethod"/>.
        /// </summary>
        /// <param name="derivedType">The type that may have overridden a method on its immediate base-class.</param>
        /// <param name="overriddenMethod"></param>
        /// <returns>The <see cref="IMethodSymbol"/> for the method that overrides <paramref name="overriddenMethod"/>, or null if
        /// <paramref name="overriddenMethod"/> is not overridden.</returns>
        private static ImmutableArray<IMethodSymbol> GetOverridingMethodSymbols(ITypeSymbol derivedType, IMethodSymbol overriddenMethod)
        {
            RoslynDebug.Assert(derivedType.BaseType.Equals(overriddenMethod.ContainingType, SymbolEqualityComparer.Default));

            return derivedType.GetMembers(overriddenMethod.Name)
                .OfType<IMethodSymbol>()
                .WhereAsArray(m => m.IsOverride && overriddenMethod.Equals(m.GetOverriddenMember(), SymbolEqualityComparer.Default));
        }

        //  We use a struct instead of a record-type to save on allocations.
        //  There is no trade-off for doing so because this type is never passed by-value.
        //  We never do equality operations on instances. 
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
    }
}
