// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2002: Do not lock on objects with weak identities
    ///
    /// Cause:
    /// A thread that attempts to acquire a lock on an object that has a weak identity could cause hangs.
    ///
    /// Description:
    /// An object is said to have a weak identity when it can be directly accessed across application domain boundaries.
    /// A thread that tries to acquire a lock on an object that has a weak identity can be blocked by a second thread in
    /// a different application domain that has a lock on the same object.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotLockOnObjectsWithWeakIdentityAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2002";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotLockOnObjectsWithWeakIdentityTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotLockOnObjectsWithWeakIdentityMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotLockOnObjectsWithWeakIdentityDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                         s_localizableTitle,
                                                                         s_localizableMessage,
                                                                         DiagnosticCategory.Reliability,
                                                                         RuleLevel.CandidateForRemoval,     // .NET core only has one appdomain
                                                                         description: s_localizableDescription,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(compilationStartContext =>
            {
                Compilation compilation = compilationStartContext.Compilation;
                compilationStartContext.RegisterOperationAction(context =>
                {
                    var lockStatement = (ILockOperation)context.Operation;
                    if (lockStatement.LockedValue?.Type is ITypeSymbol type &&
                        TypeHasWeakIdentity(type, compilation))
                    {
                        context.ReportDiagnostic(lockStatement.LockedValue.Syntax.CreateDiagnostic(Rule, type.ToDisplayString()));
                    }
                },
                OperationKind.Lock);
            });
        }

        private static bool TypeHasWeakIdentity(ITypeSymbol type, Compilation compilation)
        {
            switch (type.TypeKind)
            {
                case TypeKind.Array:
                    return type is IArrayTypeSymbol arrayType && IsPrimitiveType(arrayType.ElementType);
                case TypeKind.Class:
                case TypeKind.TypeParameter:
                    INamedTypeSymbol? marshalByRefObjectTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMarshalByRefObject);
                    INamedTypeSymbol? executionEngineExceptionTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemExecutionEngineException);
                    INamedTypeSymbol? outOfMemoryExceptionTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemOutOfMemoryException);
                    INamedTypeSymbol? stackOverflowExceptionTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStackOverflowException);
                    INamedTypeSymbol? memberInfoTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReflectionMemberInfo);
                    INamedTypeSymbol? parameterInfoTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReflectionParameterInfo);
                    INamedTypeSymbol? threadTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingThread);
                    return
                        type.SpecialType == SpecialType.System_String ||
                        type.Equals(executionEngineExceptionTypeSymbol) ||
                        type.Equals(outOfMemoryExceptionTypeSymbol) ||
                        type.Equals(stackOverflowExceptionTypeSymbol) ||
                        type.Inherits(marshalByRefObjectTypeSymbol) ||
                        type.Inherits(memberInfoTypeSymbol) ||
                        type.Inherits(parameterInfoTypeSymbol) ||
                        type.Inherits(threadTypeSymbol);

                // What about struct types?
                default:
                    return false;
            }
        }

        public static bool IsPrimitiveType(ITypeSymbol type)
        {
            return type.SpecialType switch
            {
                SpecialType.System_Boolean
                or SpecialType.System_Byte
                or SpecialType.System_Char
                or SpecialType.System_Double
                or SpecialType.System_Int16
                or SpecialType.System_Int32
                or SpecialType.System_Int64
                or SpecialType.System_UInt16
                or SpecialType.System_UInt32
                or SpecialType.System_UInt64
                or SpecialType.System_IntPtr
                or SpecialType.System_UIntPtr
                or SpecialType.System_SByte
                or SpecialType.System_Single => true,
                _ => false,
            };
        }
    }
}
