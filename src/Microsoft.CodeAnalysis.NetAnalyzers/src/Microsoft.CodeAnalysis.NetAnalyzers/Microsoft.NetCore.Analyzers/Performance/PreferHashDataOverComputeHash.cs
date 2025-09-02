// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1850: <inheritdoc cref="PreferHashDataOverComputeHashAnalyzerTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferHashDataOverComputeHashAnalyzer : DiagnosticAnalyzer
    {
        internal const string CA1850 = nameof(CA1850);
        internal const string TargetHashTypeDiagnosticPropertyKey = nameof(TargetHashTypeDiagnosticPropertyKey);
        internal const string DeleteHashCreationPropertyKey = nameof(DeleteHashCreationPropertyKey);
        internal const string ComputeTypePropertyKey = nameof(ComputeTypePropertyKey);
        internal const string HashCreationIndexPropertyKey = nameof(HashCreationIndexPropertyKey);
        internal const string HashDataMethodName = "HashData";
        internal const string TryHashDataMethodName = "TryHashData";
        private const string ComputeHashMethodName = nameof(System.Security.Cryptography.HashAlgorithm.ComputeHash);
        private const string DisposeMethodName = nameof(System.Security.Cryptography.HashAlgorithm.Dispose);
        private const string TryComputeHashMethodName = "TryComputeHash";
        private const string CreateMethodName = nameof(System.Security.Cryptography.SHA256.Create);

        internal static readonly DiagnosticDescriptor StringRule = DiagnosticDescriptorHelper.Create(
            CA1850,
            CreateLocalizableResourceString(nameof(PreferHashDataOverComputeHashAnalyzerTitle)),
            CreateLocalizableResourceString(nameof(PreferHashDataOverComputeHashAnalyzerMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(PreferHashDataOverComputeHashAnalyzerDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(StringRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var methodHelper = MethodHelper.Init(context.Compilation);
            if (methodHelper is null)
            {
                return;
            }

            context.RegisterOperationBlockStartAction(OnOperationBlockStart);
            return;

            void OnOperationBlockStart(OperationBlockStartAnalysisContext context)
            {
                // Patterns we are looking for:
                // Pattern #1                                      Pattern #2
                // var obj = #HashCreation#;           or          #HashCreation#.CompuateHash(buffer);  
                // ...
                // obj.CompuateHash(buffer);

                // The core search logic is split into 3 groups
                // 1. Scan all invocations:
                //  a. if it is a Hash Create static method, store its local symbol + declaration operation and type symbols
                //  b. if it is HashAlgorithm.ComputeHash /  HashAlgorithm.TryComputeHash
                //      1. if its instance is a local reference, store its local reference + invocation
                //      2. if its instance is the creation of a hash instance, we found pattern #2. Report diagnostic.
                //  c. if it is HashAlgorithm.Dispose, store the invocation operation
                // 2. Find all HashAlgorithm object creation (new) and store its local symbol + declaration operation and type symbols
                // 3. Find all HashAlgorithm local references and store them

                // At OperationBlockEnd:
                // 1. Compile a 'disposeMap' map of symbol -> dispose invocations (Dictionary<ILocalSymbol, ImmutableArray<IInvocationOperation>>)
                // 2. Compile a 'computeHashOnlyCountMap' map of symbol -> computehash invocation count (Dictionary<ILocalSymbol, int>)
                //    a. if the count for its local references in the block does not match the count of computehash invocations + dispose invocations, it is excluded from this map
                // 3. Iterate the invocation, only report the invocation
                //    a. hashAlgorithm type has a static HashData method
                //    b. hashAlgorithm instance was created in the block
                //    c. hashAlgorithm instance exist in the 'computeHashOnlyCountMap' map

                // Reporting of Diagnostic
                // The main span reported is at the ComputeHash method
                //
                // Properties:
                //  if there is only 1 local reference of a symbol excluding dispose references, DeleteHashCreationPropertyKey is set
                //
                // Additional locations:
                // ComputeHash/ComputeHash(a,b,c)/TryComputeHash:
                // Pattern #1                                      Pattern #2        
                // 1. span where the hash instance was created
                // 2-N. dispose invocations

                var dataCollector = new DataCollector();

                context.RegisterOperationAction(CaptureHashLocalReferenceOperation, OperationKind.LocalReference);
                context.RegisterOperationAction(CaptureCreateOrComputeHashInvocationOperation, OperationKind.Invocation);
                context.RegisterOperationAction(CaptureHashObjectCreationOperation, OperationKind.ObjectCreation);
                context.RegisterOperationBlockEndAction(OnOperationBlockEnd);
                return;

                void CaptureHashLocalReferenceOperation(OperationAnalysisContext context)
                {
                    var localReferenceOperation = (ILocalReferenceOperation)context.Operation;
                    if (methodHelper.IsLocalReferenceInheritingHashAlgorithm(localReferenceOperation))
                    {
                        dataCollector.CollectLocalReferenceInheritingHashAlgorithm(localReferenceOperation);
                    }
                }

                void CaptureCreateOrComputeHashInvocationOperation(OperationAnalysisContext context)
                {
                    var invocationOperation = (IInvocationOperation)context.Operation;
                    if (methodHelper.IsHashCreateMethod(invocationOperation))
                    {
                        CaptureHashCreateInvocationOperation(dataCollector, invocationOperation);
                    }
                    else if (methodHelper.IsComputeHashMethod(invocationOperation, out ComputeType computeType))
                    {
                        CaptureOrReportComputeHashInvocationOperation(context, methodHelper, dataCollector, invocationOperation, computeType);
                    }
                    else if (invocationOperation.Instance is ILocalReferenceOperation && methodHelper.IsDisposeMethod(invocationOperation))
                    {
                        dataCollector.CollectDisposeInvocation(invocationOperation);
                    }
                }

                void CaptureHashObjectCreationOperation(OperationAnalysisContext context)
                {
                    var objectCreationOperation = (IObjectCreationOperation)context.Operation;
                    if (!methodHelper.IsObjectCreationInheritingHashAlgorithm(objectCreationOperation))
                    {
                        return;
                    }

                    if (TryGetVariableInitializerOperation(objectCreationOperation.Parent, out var variableInitializerOperation))
                    {
                        CaptureVariableDeclaratorOperation(dataCollector, objectCreationOperation.Type!, variableInitializerOperation);
                    }
                }

                void OnOperationBlockEnd(OperationBlockAnalysisContext context)
                {
                    var cancellationToken = context.CancellationToken;
                    var dataResult = dataCollector.Compile(cancellationToken);

                    if (dataResult is null)
                    {
                        return;
                    }

                    foreach (var (computeHash, type) in dataResult.ComputeHashMap)
                    {
                        ImmutableArray<Location> codefixerLocations;
                        var localSymbol = ((ILocalReferenceOperation)computeHash.Instance!).Local;
                        var isToDeleteHashCreation = false;

                        if (dataResult.TryGetDeclarationTuple(localSymbol, out var declarationTuple) &&
                            dataResult.TryGetSymbolComputeHashRefCountTuple(localSymbol, out var refCount) &&
                            methodHelper.TryGetHashDataMethod(declarationTuple.OriginalType, type, out var hashDataMethodSymbol))
                        {
                            var disposeArray = dataResult.GetDisposeInvocationArray(localSymbol);
                            isToDeleteHashCreation = refCount == 1;
                            codefixerLocations = GetFixerLocations(declarationTuple.VariableIntitializerOperation, disposeArray, out var hashCreationLocationIndex);
                            var diagnostics = CreateDiagnostics(computeHash, hashDataMethodSymbol.ContainingType, codefixerLocations, type, isToDeleteHashCreation, hashCreationLocationIndex);
                            context.ReportDiagnostic(diagnostics);
                        }
                    }

                    dataResult.Free(cancellationToken);
                }
            }

            static void CaptureHashCreateInvocationOperation(DataCollector dataCollector, IInvocationOperation hashCreateInvocation)
            {
                if (TryGetVariableInitializerOperation(hashCreateInvocation.Parent, out var variableInitializerOperation))
                {
                    var ownerType = hashCreateInvocation.TargetMethod.ContainingType;
                    CaptureVariableDeclaratorOperation(dataCollector, ownerType, variableInitializerOperation);
                }
            }

            static void CaptureVariableDeclaratorOperation(DataCollector dataCollector, ITypeSymbol createdType, IVariableInitializerOperation variableInitializerOperation)
            {
                switch (variableInitializerOperation.Parent)
                {
                    case IVariableDeclaratorOperation declaratorOperation:
                        dataCollector.CollectVariableDeclaratorOperation(declaratorOperation.Symbol, variableInitializerOperation, createdType);
                        break;
                    case IVariableDeclarationOperation declarationOperation when declarationOperation.Declarators.Length == 1:
                        {
                            var declaratorOperationAlt = declarationOperation.Declarators[0];
                            dataCollector.CollectVariableDeclaratorOperation(declaratorOperationAlt.Symbol, variableInitializerOperation, createdType);
                            break;
                        }
                }
            }

            static void CaptureOrReportComputeHashInvocationOperation(OperationAnalysisContext context, MethodHelper methodHelper, DataCollector dataCollector, IInvocationOperation computeHashInvocation, ComputeType computeType)
            {
                switch (computeHashInvocation.Instance)
                {
                    case ILocalReferenceOperation:
                        dataCollector.CollectComputeHashInvocation(computeHashInvocation, computeType);
                        break;
                    case IInvocationOperation chainedInvocationOperation when methodHelper.IsHashCreateMethod(chainedInvocationOperation):
                        ReportChainedComputeHashInvocationOperation(chainedInvocationOperation.TargetMethod.ContainingType);
                        break;
                    case IObjectCreationOperation chainObjectCreationOperation when methodHelper.IsObjectCreationInheritingHashAlgorithm(chainObjectCreationOperation):
                        ReportChainedComputeHashInvocationOperation(chainObjectCreationOperation.Type);
                        break;
                }

                void ReportChainedComputeHashInvocationOperation(ITypeSymbol? originalHashType)
                {
                    if (!methodHelper.TryGetHashDataMethod(originalHashType, computeType, out var staticHashMethod))
                    {
                        return;
                    }

                    var diagnostics = CreateDiagnostics(computeHashInvocation, staticHashMethod.ContainingType, computeType);
                    context.ReportDiagnostic(diagnostics);
                }
            }
        }

        private static bool TryGetVariableInitializerOperation([NotNullWhen(true)] IOperation? symbol, [NotNullWhen(true)] out IVariableInitializerOperation? variableInitializerOperation)
        {
            switch (symbol)
            {
                case IVariableInitializerOperation op:
                    variableInitializerOperation = op;
                    return true;
                case IConversionOperation { Parent: IVariableInitializerOperation variableInitializerOperationAlt }:
                    variableInitializerOperation = variableInitializerOperationAlt;
                    return true;
                default:
                    variableInitializerOperation = null;
                    return false;
            }
        }

        private static Diagnostic CreateDiagnostics(IInvocationOperation computeHashMethod,
            INamedTypeSymbol staticHashMethodType,
            ComputeType computeType)
        {
            var dictBuilder = ImmutableDictionary.CreateBuilder<string, string?>();
            dictBuilder.Add(TargetHashTypeDiagnosticPropertyKey, staticHashMethodType.Name);
            dictBuilder.Add(ComputeTypePropertyKey, computeType.ToString());

            return computeHashMethod.CreateDiagnostic(StringRule,
                dictBuilder.ToImmutable(),
                staticHashMethodType.ToDisplayString());
        }

        private static Diagnostic CreateDiagnostics(IInvocationOperation computeHashMethod,
            INamedTypeSymbol staticHashMethodType,
            ImmutableArray<Location> fixerLocations,
            ComputeType computeType,
            bool isSingleLocalRef,
            int hashCreationLocationIndex)
        {
            var dictBuilder = ImmutableDictionary.CreateBuilder<string, string?>();
            dictBuilder.Add(TargetHashTypeDiagnosticPropertyKey, staticHashMethodType.Name);
            dictBuilder.Add(ComputeTypePropertyKey, computeType.ToString());
            dictBuilder.Add(HashCreationIndexPropertyKey, hashCreationLocationIndex.ToString(CultureInfo.InvariantCulture));

            if (isSingleLocalRef)
            {
                dictBuilder.Add(DeleteHashCreationPropertyKey, DeleteHashCreationPropertyKey);
            }

            return computeHashMethod.CreateDiagnostic(StringRule,
                fixerLocations,
                dictBuilder.ToImmutable(),
                staticHashMethodType.ToDisplayString());
        }

        private static ImmutableArray<Location> GetFixerLocations(
            IVariableInitializerOperation variableInitializerOperation,
            ImmutableArray<IInvocationOperation> disposeArray,
            out int hashCreationLocationIndex)
        {
            ImmutableArray<Location>.Builder builder = ImmutableArray.CreateBuilder<Location>(1 + disposeArray.Length);
            Location hashCreation = variableInitializerOperation.Syntax.Parent!.GetLocation();
            hashCreationLocationIndex = builder.Count;
            builder.Add(hashCreation);

            foreach (var disposeInvocation in disposeArray)
            {
                var disposeLocation = disposeInvocation.Syntax.Parent!.GetLocation();
                builder.Add(disposeLocation);
            }

            return builder.MoveToImmutable();
        }

        private static ImmutableArray<IInvocationOperation> GetValueOrEmpty(PooledDictionary<ILocalSymbol, ImmutableArray<IInvocationOperation>>? dictionary, ILocalSymbol key)
        {
            if (dictionary is not null && dictionary.TryGetValue(key, out var value))
            {
                return value;
            }

            return ImmutableArray<IInvocationOperation>.Empty;
        }

#pragma warning disable CA1815 // Override equals and operator equals on value types
        private readonly struct DeclarationTuple
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public IVariableInitializerOperation VariableIntitializerOperation { get; }

            public ITypeSymbol OriginalType { get; }

            public DeclarationTuple(IVariableInitializerOperation variableIntitializerOperation, ITypeSymbol type)
            {
                VariableIntitializerOperation = variableIntitializerOperation;
                OriginalType = type;
            }
        }

        public enum ComputeType
        {
            ComputeHash,
            ComputeHashSection,
            TryComputeHash
        }

        private sealed class MethodHelper
        {
            private readonly INamedTypeSymbol _hashAlgorithmBaseType;
            private readonly IMethodSymbol _computeHashMethodSymbol;
            private readonly IMethodSymbol _disposeMethodSymbol;
            private readonly IMethodSymbol? _computeHashSectionMethodSymbol;
            private readonly IMethodSymbol? _tryComputeHashMethodSymbol;

            // for finding HashData methods
            private readonly ParameterInfo _byteArrayParameter;
            private readonly ParameterInfo _rosByteParameter;
            private readonly ParameterInfo _spanByteParameter;
            private readonly ParameterInfo _intParameter;

            private static readonly ImmutableHashSet<string> SpecialManagedHashAlgorithms = ImmutableHashSet.CreateRange(new[] {
                nameof(System.Security.Cryptography.SHA1Managed),
                nameof(System.Security.Cryptography.SHA256Managed),
                nameof(System.Security.Cryptography.SHA384Managed),
                nameof(System.Security.Cryptography.SHA512Managed),
                nameof(System.Security.Cryptography.MD5CryptoServiceProvider),
                nameof(System.Security.Cryptography.SHA1CryptoServiceProvider),
                nameof(System.Security.Cryptography.SHA256CryptoServiceProvider),
                nameof(System.Security.Cryptography.SHA384CryptoServiceProvider),
                nameof(System.Security.Cryptography.SHA512CryptoServiceProvider),
            });

            private MethodHelper(INamedTypeSymbol hashAlgorithmBaseType,
                IMethodSymbol computeHashMethodSymbol,
                IMethodSymbol disposeMethodSymbol,
                IMethodSymbol? computeHashSectionMethodSymbol,
                IMethodSymbol? tryComputeHashMethodSymbol,
                ParameterInfo byteArrayParameter,
                ParameterInfo rosByteParameter,
                ParameterInfo spanByteParameter,
                ParameterInfo intParameter)
            {
                _hashAlgorithmBaseType = hashAlgorithmBaseType;
                _computeHashMethodSymbol = computeHashMethodSymbol;
                _disposeMethodSymbol = disposeMethodSymbol;
                _computeHashSectionMethodSymbol = computeHashSectionMethodSymbol;
                _tryComputeHashMethodSymbol = tryComputeHashMethodSymbol;

                _byteArrayParameter = byteArrayParameter;
                _rosByteParameter = rosByteParameter;
                _spanByteParameter = spanByteParameter;
                _intParameter = intParameter;
            }

            public bool IsLocalReferenceInheritingHashAlgorithm(ILocalReferenceOperation localReferenceOperation) => localReferenceOperation.Local.Type.Inherits(_hashAlgorithmBaseType);

            public bool IsObjectCreationInheritingHashAlgorithm(IObjectCreationOperation objectCreationOperation) => objectCreationOperation.Type.Inherits(_hashAlgorithmBaseType);

            public bool IsHashCreateMethod(IInvocationOperation invocationOperation)
            {
                IMethodSymbol methodSymbol = invocationOperation.TargetMethod;
                return methodSymbol.ContainingType.Inherits(_hashAlgorithmBaseType) &&
                    methodSymbol.ReturnType.Inherits(_hashAlgorithmBaseType) &&
                    methodSymbol.Name.Equals(CreateMethodName, StringComparison.Ordinal);
            }

            public bool IsComputeHashMethod(IInvocationOperation invocationOperation, out ComputeType computeType)
            {
                if (IsComputeHashMethod(invocationOperation))
                {
                    computeType = ComputeType.ComputeHash;
                    return true;
                }

                if (IsComputeHashSectionMethod(invocationOperation))
                {
                    computeType = ComputeType.ComputeHashSection;
                    return true;
                }

                if (IsTryComputeHashMethod(invocationOperation))
                {
                    computeType = ComputeType.TryComputeHash;
                    return true;
                }

                computeType = default;
                return false;
            }

            public bool IsComputeHashMethod(IInvocationOperation invocationOperation) => invocationOperation.TargetMethod.Equals(_computeHashMethodSymbol, SymbolEqualityComparer.Default);

            public bool IsComputeHashSectionMethod(IInvocationOperation invocationOperation) => invocationOperation.TargetMethod.Equals(_computeHashSectionMethodSymbol, SymbolEqualityComparer.Default);

            public bool IsTryComputeHashMethod(IInvocationOperation invocationOperation) => invocationOperation.TargetMethod.Equals(_tryComputeHashMethodSymbol, SymbolEqualityComparer.Default);

            public bool IsDisposeMethod(IInvocationOperation invocationOperation) => invocationOperation.TargetMethod.Equals(_disposeMethodSymbol, SymbolEqualityComparer.Default);

            public bool TryGetHashDataMethod(ITypeSymbol? originalHashType, ComputeType computeType, [NotNullWhen(true)] out IMethodSymbol? staticHashMethod)
            {
                staticHashMethod = null;
                return computeType switch
                {
                    ComputeType.ComputeHash => TryGetHashDataMethodByteArg(originalHashType, out staticHashMethod),
                    ComputeType.ComputeHashSection => TryGetHashDataMethodSpanArg(originalHashType, out staticHashMethod),
                    ComputeType.TryComputeHash => TryGetTryHashDataMethod(originalHashType, out staticHashMethod),
                    _ => false,
                };
            }

            public bool TryGetHashDataMethodByteArg(ITypeSymbol? originalHashType, [NotNullWhen(true)] out IMethodSymbol? staticHashMethod) => TryGetHashDataMethodOneArg(originalHashType, _byteArrayParameter, out staticHashMethod);

            public bool TryGetHashDataMethodSpanArg(ITypeSymbol? originalHashType, [NotNullWhen(true)] out IMethodSymbol? staticHashMethod) => TryGetHashDataMethodOneArg(originalHashType, _rosByteParameter, out staticHashMethod);

            public bool TryGetTryHashDataMethod(ITypeSymbol? originalHashType, [NotNullWhen(true)] out IMethodSymbol? staticHashMethod) => TryGetTryHashDataMethod(originalHashType, _rosByteParameter, _spanByteParameter, _intParameter, out staticHashMethod);

            private static bool TryGetHashDataMethodOneArg(ITypeSymbol? originalHashType, ParameterInfo argOne, [NotNullWhen(true)] out IMethodSymbol? staticHashMethod)
            {
                if (originalHashType == null)
                {
                    staticHashMethod = null;
                    return false;
                }

                if (IsSpecialManagedHashAlgorithms(originalHashType))
                {
                    originalHashType = originalHashType.BaseType!;
                }

                staticHashMethod = originalHashType.GetMembers(HashDataMethodName).OfType<IMethodSymbol>()
                    .GetFirstOrDefaultMemberWithParameterInfos(argOne);

                return staticHashMethod is not null;
            }

            private static bool TryGetTryHashDataMethod(
                [NotNullWhen(true)] ITypeSymbol? originalHashType,
                ParameterInfo sourceArgument,
                ParameterInfo destArgument,
                ParameterInfo intArgument,
                [NotNullWhen(true)] out IMethodSymbol? staticHashMethod)
            {
                if (originalHashType == null)
                {
                    staticHashMethod = null;
                    return false;
                }

                if (IsSpecialManagedHashAlgorithms(originalHashType))
                {
                    originalHashType = originalHashType.BaseType!;
                }

                staticHashMethod = originalHashType.GetMembers(TryHashDataMethodName).OfType<IMethodSymbol>()
                    .GetFirstOrDefaultMemberWithParameterInfos(sourceArgument, destArgument, intArgument);

                return staticHashMethod is not null;
            }

            private static bool IsSpecialManagedHashAlgorithms(ITypeSymbol originalHashType)
            {
                if (!SpecialManagedHashAlgorithms.Contains(originalHashType.Name))
                {
                    return false;
                }

                return originalHashType.ContainingNamespace is
                {
                    Name: nameof(System.Security.Cryptography),
                    ContainingNamespace:
                    {
                        Name: nameof(System.Security),
                        ContainingNamespace.Name: nameof(System)
                    }
                };
            }

            public static MethodHelper? Init(Compilation compilation)
            {
                if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyHashAlgorithm, out var hashAlgoBaseType) ||
                    !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographySHA256, out var sha256Type) ||
                    !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1, out var rosType) ||
                    !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSpan1, out var spanType))
                {
                    return null;
                }

                var byteType = compilation.GetSpecialType(SpecialType.System_Byte);
                var intType = compilation.GetSpecialType(SpecialType.System_Int32);
                if (byteType.IsErrorType() || intType.IsErrorType())
                {
                    return null;
                }

                var rosByteType = rosType.Construct(byteType);
                var spanByteType = spanType.Construct(byteType);

                var byteArrayParameter = ParameterInfo.GetParameterInfo(byteType, isArray: true, arrayRank: 1);
                var rosByteParameter = ParameterInfo.GetParameterInfo(rosByteType);
                var spanByteParameter = ParameterInfo.GetParameterInfo(spanByteType);
                var intParameter = ParameterInfo.GetParameterInfo(intType);

                // method introduced in .NET 5.0
                var hashDataMethodType = sha256Type.GetMembers(HashDataMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos(byteArrayParameter);
                if (hashDataMethodType is null)
                {
                    return null;
                }

                var disposeHashMethodBaseType = hashAlgoBaseType.GetMembers(DisposeMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos();
                if (disposeHashMethodBaseType is null)
                {
                    return null;
                }

                var computeHashMethodBaseType = hashAlgoBaseType.GetMembers(ComputeHashMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos(byteArrayParameter);
                if (computeHashMethodBaseType is null)
                {
                    return null;
                }

                var computeHashSectionMethodBaseType = hashAlgoBaseType.GetMembers(ComputeHashMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos(byteArrayParameter, intParameter, intParameter);
                var tryComputeHashMethodBaseType = hashAlgoBaseType.GetMembers(TryComputeHashMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos(rosByteParameter, spanByteParameter, intParameter);

                var methodHelper = new MethodHelper(
                    hashAlgoBaseType,
                    computeHashMethodBaseType,
                    disposeHashMethodBaseType,
                    computeHashSectionMethodBaseType,
                    tryComputeHashMethodBaseType,
                    byteArrayParameter,
                    rosByteParameter,
                    spanByteParameter,
                    intParameter);

                return methodHelper;
            }
        }

        private sealed class DataCollector
        {
            private readonly PooledConcurrentSet<IInvocationOperation> _disposeHashSet = PooledConcurrentSet<IInvocationOperation>.GetInstance();
            private readonly PooledConcurrentDictionary<ILocalSymbol, DeclarationTuple> _createdSymbolMap = PooledConcurrentDictionary<ILocalSymbol, DeclarationTuple>.GetInstance(SymbolEqualityComparer.Default);
            private readonly PooledConcurrentDictionary<ILocalReferenceOperation, ILocalSymbol> _localReferenceMap = PooledConcurrentDictionary<ILocalReferenceOperation, ILocalSymbol>.GetInstance();
            private readonly PooledConcurrentDictionary<IInvocationOperation, ComputeType> _computeHashMap = PooledConcurrentDictionary<IInvocationOperation, ComputeType>.GetInstance();

            public void CollectLocalReferenceInheritingHashAlgorithm(ILocalReferenceOperation localReferenceOperation) => _localReferenceMap.TryAdd(localReferenceOperation, localReferenceOperation.Local);

            public void CollectVariableDeclaratorOperation(ILocalSymbol localSymbol, IVariableInitializerOperation declaratorOperation, ITypeSymbol createdType) => _createdSymbolMap.TryAdd(localSymbol, new DeclarationTuple(declaratorOperation, createdType));

            public void CollectComputeHashInvocation(IInvocationOperation computeHashInvocation, ComputeType computeType) => _computeHashMap.TryAdd(computeHashInvocation, computeType);

            public void CollectDisposeInvocation(IInvocationOperation disposeInvocation) => _disposeHashSet.Add(disposeInvocation);

            public DataResult? Compile(CancellationToken cancellationToken)
            {
                var disposeMap = CompileDisposeMap(cancellationToken);
                var computeHashOnlyMap = GetComputeHashOnlySymbols(disposeMap, cancellationToken);
                _disposeHashSet.Free(cancellationToken);
                _localReferenceMap.Free(cancellationToken);

                if (computeHashOnlyMap is null)
                {
                    disposeMap?.Free(cancellationToken);
                    _createdSymbolMap.Free(cancellationToken);
                    _computeHashMap.Free(cancellationToken);
                    return null;
                }

                return new DataResult(_createdSymbolMap, disposeMap, computeHashOnlyMap, _computeHashMap);
            }

            private PooledDictionary<ILocalSymbol, ImmutableArray<IInvocationOperation>>? CompileDisposeMap(CancellationToken cancellationToken)
            {
                if (_disposeHashSet.IsEmpty)
                {
                    return null;
                }

                var map = PooledDictionary<ILocalSymbol, ImmutableArray<IInvocationOperation>>.GetInstance(SymbolEqualityComparer.Default);
                var workingMap = PooledDictionary<ILocalSymbol, ArrayBuilder<IInvocationOperation>>.GetInstance(SymbolEqualityComparer.Default);

                foreach (var dispose in _disposeHashSet)
                {
                    var local = ((ILocalReferenceOperation)dispose.Instance!).Local;
                    if (!workingMap.TryGetValue(local, out var arrayBuilder))
                    {
                        arrayBuilder = ArrayBuilder<IInvocationOperation>.GetInstance();
                        workingMap.Add(local, arrayBuilder);
                    }

                    arrayBuilder.Add(dispose);
                }

                foreach (var kvp in workingMap)
                {
                    var local = kvp.Key;
                    var arrayBuilder = kvp.Value;
                    var disposeArray = arrayBuilder.ToImmutableAndFree();

                    map.Add(local, disposeArray);
                }

                workingMap.Free(cancellationToken);

                return map;
            }

            private PooledDictionary<ILocalSymbol, int>? GetComputeHashOnlySymbols(PooledDictionary<ILocalSymbol, ImmutableArray<IInvocationOperation>>? disposeMap, CancellationToken cancellationToken)
            {
                if (_localReferenceMap.IsEmpty || _computeHashMap.IsEmpty)
                {
                    return null;
                }

                // we find the symbol whose local ref count matches the count of computeHash invoked
                PooledDictionary<ILocalSymbol, int>? hashSet = null;
                var localRefSymbolCountMap = PooledDictionary<ILocalSymbol, int>.GetInstance(SymbolEqualityComparer.Default);
                var computeHashSymbolCountMap = PooledDictionary<ILocalSymbol, int>.GetInstance(SymbolEqualityComparer.Default);

                foreach (var (_, local) in _localReferenceMap)
                {
                    if (!localRefSymbolCountMap.TryGetValue(local, out var count))
                    {
                        count = 0;
                    }

                    localRefSymbolCountMap[local] = count + 1;
                }

                foreach (var (computeHash, _) in _computeHashMap)
                {
                    var local = ((ILocalReferenceOperation)computeHash.Instance!).Local;
                    if (!computeHashSymbolCountMap.TryGetValue(local, out var count))
                    {
                        count = 0;
                    }

                    computeHashSymbolCountMap[local] = count + 1;
                }

                foreach (var (local, refCount) in localRefSymbolCountMap)
                {
                    if (!computeHashSymbolCountMap.TryGetValue(local, out var computeHashCount))
                    {
                        continue;
                    }

                    var disposeArray = GetValueOrEmpty(disposeMap, local);

                    var refCountWithoutDispose = refCount - disposeArray.Length;
                    if (refCountWithoutDispose == computeHashCount)
                    {
                        hashSet ??= PooledDictionary<ILocalSymbol, int>.GetInstance(SymbolEqualityComparer.Default);
                        hashSet.Add(local, refCountWithoutDispose);
                    }
                }

                localRefSymbolCountMap.Free(cancellationToken);
                computeHashSymbolCountMap.Free(cancellationToken);

                return hashSet;
            }
        }

        private sealed class DataResult
        {
            private readonly PooledConcurrentDictionary<ILocalSymbol, DeclarationTuple> _createdSymbolMap;
            private readonly PooledDictionary<ILocalSymbol, int> _computeHashOnlySymbolCountMap;
            private readonly PooledDictionary<ILocalSymbol, ImmutableArray<IInvocationOperation>>? _disposeMap;

            public DataResult(PooledConcurrentDictionary<ILocalSymbol, DeclarationTuple> createdSymbolMap,
                PooledDictionary<ILocalSymbol, ImmutableArray<IInvocationOperation>>? disposeMap,
                PooledDictionary<ILocalSymbol, int> computeHashOnlySymbolCountMap,
                PooledConcurrentDictionary<IInvocationOperation, ComputeType> computeHashMap)
            {
                _createdSymbolMap = createdSymbolMap;
                _disposeMap = disposeMap;
                _computeHashOnlySymbolCountMap = computeHashOnlySymbolCountMap;
                ComputeHashMap = computeHashMap;
            }

            public PooledConcurrentDictionary<IInvocationOperation, ComputeType> ComputeHashMap { get; }

            public bool TryGetDeclarationTuple(ILocalSymbol localSymbol, out DeclarationTuple declarationTuple) => _createdSymbolMap.TryGetValue(localSymbol, out declarationTuple);

            public bool TryGetSymbolComputeHashRefCountTuple(ILocalSymbol localSymbol, out int refCount) => _computeHashOnlySymbolCountMap.TryGetValue(localSymbol, out refCount);

            public ImmutableArray<IInvocationOperation> GetDisposeInvocationArray(ILocalSymbol localSymbol) => GetValueOrEmpty(_disposeMap, localSymbol);

            public void Free(CancellationToken cancellationToken)
            {
                _disposeMap?.Free(cancellationToken);
                _computeHashOnlySymbolCountMap.Free(cancellationToken);
                _createdSymbolMap.Free(cancellationToken);
                ComputeHashMap.Free(cancellationToken);
            }
        }
    }
}
