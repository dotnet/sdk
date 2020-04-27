// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
#pragma warning disable CA1200 // Avoid using cref tags with a prefix
    /// <summary>
    /// CA1827: Do not use Count()/LongCount() when Any() can be used.
    /// CA1828: Do not use CountAsync()/LongCountAsync() when AnyAsync() can be used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For non-empty sequences, <c>Count</c>/<c>CountAsync</c> enumerates the entire sequence,
    /// while <c>Any</c>/<c>AnyAsync</c> stops at the first item or the first item that satisfies a condition.
    /// </para>
    /// <para>
    /// <b>CA1827</b> applies to <see cref="System.Linq.Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/> and
    /// <see cref="System.Linq.Enumerable.Any{TSource}(System.Collections.Generic.IEnumerable{TSource})"/>
    /// and covers the following use cases:
    /// </para>
    /// <list type="table">
    /// <listheader><term>detected</term><term>fix</term></listheader>
    /// <item><term><c> enumerable.Count() == 0               </c></term><description><c> !enumerable.Any() </c></description></item>
    /// <item><term><c> enumerable.Count() != 0               </c></term><description><c> enumerable.Any()  </c></description></item>
    /// <item><term><c> enumerable.Count() &lt;= 0            </c></term><description><c> !enumerable.Any() </c></description></item>
    /// <item><term><c> enumerable.Count() > 0                </c></term><description><c> enumerable.Any()  </c></description></item>
    /// <item><term><c> enumerable.Count() &lt; 1             </c></term><description><c> !enumerable.Any() </c></description></item>
    /// <item><term><c> enumerable.Count() >= 1               </c></term><description><c> enumerable.Any()  </c></description></item>
    /// <item><term><c> 0 == enumerable.Count()               </c></term><description><c> !enumerable.Any() </c></description></item>
    /// <item><term><c> 0 != enumerable.Count()               </c></term><description><c> enumerable.Any()  </c></description></item>
    /// <item><term><c> 0 >= enumerable.Count()               </c></term><description><c> !enumerable.Any() </c></description></item>
    /// <item><term><c> 0 &lt; enumerable.Count()             </c></term><description><c> enumerable.Any()  </c></description></item>
    /// <item><term><c> 1 > enumerable.Count()                </c></term><description><c> !enumerable.Any() </c></description></item>
    /// <item><term><c> 1 &lt;= enumerable.Count()            </c></term><description><c> enumerable.Any()  </c></description></item>
    /// <item><term><c> enumerable.Count().Equals(0)          </c></term><description><c> !enumerable.Any() </c></description></item>
    /// <item><term><c> 0.Equals(enumerable.Count())          </c></term><description><c> !enumerable.Any() </c></description></item>
    /// <item><term><c> enumerable.Count(_ => true) == 0      </c></term><description><c> !enumerable.Any(_ => true) </c></description></item>
    /// <item><term><c> enumerable.Count(_ => true) != 0      </c></term><description><c> enumerable.Any(_ => true)  </c></description></item>
    /// <item><term><c> enumerable.Count(_ => true) &lt;= 0   </c></term><description><c> !enumerable.Any(_ => true) </c></description></item>
    /// <item><term><c> enumerable.Count(_ => true) > 0       </c></term><description><c> enumerable.Any(_ => true)  </c></description></item>
    /// <item><term><c> enumerable.Count(_ => true) &lt; 1    </c></term><description><c> !enumerable.Any(_ => true) </c></description></item>
    /// <item><term><c> enumerable.Count(_ => true) >= 1      </c></term><description><c> enumerable.Any(_ => true)  </c></description></item>
    /// <item><term><c> 0 == enumerable.Count(_ => true)      </c></term><description><c> !enumerable.Any(_ => true) </c></description></item>
    /// <item><term><c> 0 != enumerable.Count(_ => true)      </c></term><description><c> enumerable.Any(_ => true)  </c></description></item>
    /// <item><term><c> 0 &lt; enumerable.Count(_ => true)    </c></term><description><c> !enumerable.Any(_ => true) </c></description></item>
    /// <item><term><c> 0 >= enumerable.Count(_ => true)      </c></term><description><c> enumerable.Any(_ => true)  </c></description></item>
    /// <item><term><c> 1 > enumerable.Count(_ => true)       </c></term><description><c> !enumerable.Any(_ => true) </c></description></item>
    /// <item><term><c> 1 &lt;= enumerable.Count(_ => true)   </c></term><description><c> enumerable.Any(_ => true)  </c></description></item>
    /// <item><term><c> enumerable.Count(_ => true).Equals(0) </c></term><description><c> !enumerable.Any(_ => true) </c></description></item>
    /// <item><term><c> 0.Equals(enumerable.Count(_ => true)) </c></term><description><c> !enumerable.Any(_ => true) </c></description></item>
    /// </list>
    /// </remarks>
    /// <remarks>
    /// <para>
    /// <b>CA1828</b> applies to <see cref="T:Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync{TSource}(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IQueryable{TSource})"/> and
    /// <see cref="T:Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync{TSource}(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IQueryable{TSource}, System.Linq.Expressions.Expression{System.Func{TSource}, bool})"/>
    /// and covers the following use cases:
    /// </para>
    /// <list type="table">
    /// <listheader><term>detected</term><term>fix</term></listheader>
    /// <item><term><c> await queryable.CountAsync() == 0               </c></term><description><c> !await queryable.AnyAsync() </c></description></item>
    /// <item><term><c> await queryable.CountAsync() != 0               </c></term><description><c> await queryable.AnyAsync()  </c></description></item>
    /// <item><term><c> await queryable.CountAsync() &lt;= 0            </c></term><description><c> !await queryable.AnyAsync() </c></description></item>
    /// <item><term><c> await queryable.CountAsync() > 0                </c></term><description><c> await queryable.AnyAsync()  </c></description></item>
    /// <item><term><c> await queryable.CountAsync() &lt; 1             </c></term><description><c> !await queryable.AnyAsync() </c></description></item>
    /// <item><term><c> await queryable.CountAsync() >= 1               </c></term><description><c> await queryable.AnyAsync()  </c></description></item>
    /// <item><term><c> 0 == await queryable.CountAsync()               </c></term><description><c> !await queryable.AnyAsync() </c></description></item>
    /// <item><term><c> 0 != await queryable.CountAsync()               </c></term><description><c> await queryable.AnyAsync()  </c></description></item>
    /// <item><term><c> 0 >= await queryable.CountAsync()               </c></term><description><c> !await queryable.AnyAsync() </c></description></item>
    /// <item><term><c> 0 &lt; await queryable.CountAsync()             </c></term><description><c> await queryable.AnyAsync()  </c></description></item>
    /// <item><term><c> 1 > await queryable.CountAsync()                </c></term><description><c> !await queryable.AnyAsync() </c></description></item>
    /// <item><term><c> 1 &lt;= await queryable.CountAsync()            </c></term><description><c> await queryable.AnyAsync()  </c></description></item>
    /// <item><term><c> (await queryable.CountAsync()).Equals(0)          </c></term><description><c> !await queryable.AnyAsync() </c></description></item>
    /// <item><term><c> 0.Equals(await queryable.CountAsync())          </c></term><description><c> !await queryable.AnyAsync() </c></description></item>
    /// <item><term><c> await queryable.CountAsync(_ => true) == 0      </c></term><description><c> !await queryable.AnyAsync(_ => true) </c></description></item>
    /// <item><term><c> await queryable.CountAsync(_ => true) != 0      </c></term><description><c> await queryable.AnyAsync(_ => true)  </c></description></item>
    /// <item><term><c> await queryable.CountAsync(_ => true) &lt;= 0   </c></term><description><c> !await queryable.AnyAsync(_ => true) </c></description></item>
    /// <item><term><c> await queryable.CountAsync(_ => true) > 0       </c></term><description><c> await queryable.AnyAsync(_ => true)  </c></description></item>
    /// <item><term><c> await queryable.CountAsync(_ => true) &lt; 1    </c></term><description><c> !await queryable.AnyAsync(_ => true) </c></description></item>
    /// <item><term><c> await queryable.CountAsync(_ => true) >= 1      </c></term><description><c> await queryable.AnyAsync(_ => true)  </c></description></item>
    /// <item><term><c> 0 == await queryable.CountAsync(_ => true)      </c></term><description><c> !await queryable.AnyAsync(_ => true) </c></description></item>
    /// <item><term><c> 0 != await queryable.CountAsync(_ => true)      </c></term><description><c> await queryable.AnyAsync(_ => true)  </c></description></item>
    /// <item><term><c> 0 &lt; await queryable.CountAsync(_ => true)    </c></term><description><c> !await queryable.AnyAsync(_ => true) </c></description></item>
    /// <item><term><c> 0 >= await queryable.CountAsync(_ => true)      </c></term><description><c> await queryable.AnyAsync(_ => true)  </c></description></item>
    /// <item><term><c> 1 > await queryable.CountAsync(_ => true)       </c></term><description><c> !await queryable.AnyAsync(_ => true) </c></description></item>
    /// <item><term><c> 1 &lt;= await queryable.CountAsync(_ => true)   </c></term><description><c> await queryable.AnyAsync(_ => true)  </c></description></item>
    /// <item><term><c> await queryable.CountAsync(_ => true).Equals(0) </c></term><description><c> !await queryable.AnyAsync(_ => true) </c></description></item>
    /// <item><term><c> 0.Equals(await queryable.CountAsync(_ => true)) </c></term><description><c> !await queryable.AnyAsync(_ => true) </c></description></item>
    /// </list>
    /// </remarks>
#pragma warning restore CA1200 // Avoid using cref tags with a prefix
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseCountWhenAnyCanBeUsedAnalyzer : DiagnosticAnalyzer
    {
        internal const string AsyncRuleId = "CA1828";
        internal const string SyncRuleId = "CA1827";
        internal const string OperationKey = nameof(OperationKey);
        internal const string IsAsyncKey = nameof(IsAsyncKey);
        internal const string ShouldNegateKey = nameof(ShouldNegateKey);
        internal const string OperationEqualsInstance = nameof(OperationEqualsInstance);
        internal const string OperationEqualsArgument = nameof(OperationEqualsArgument);
        internal const string OperationBinaryLeft = nameof(OperationBinaryLeft);
        internal const string OperationBinaryRight = nameof(OperationBinaryRight);
        private static readonly LocalizableString s_asyncLocalizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountAsyncWhenAnyAsyncCanBeUsedTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_asyncLocalizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountAsyncWhenAnyAsyncCanBeUsedMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountAsyncWhenAnyAsyncCanBeUsedDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_syncLocalizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountWhenAnyCanBeUsedTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_syncLocalizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountWhenAnyCanBeUsedMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_syncLocalizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountWhenAnyCanBeUsedDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly DiagnosticDescriptor s_asyncRule = DiagnosticDescriptorHelper.Create(
            AsyncRuleId,
            s_asyncLocalizableTitle,
            s_asyncLocalizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);
        private static readonly DiagnosticDescriptor s_syncRule = DiagnosticDescriptorHelper.Create(
            SyncRuleId,
            s_syncLocalizableTitle,
            s_syncLocalizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: s_syncLocalizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        private static readonly ImmutableHashSet<string> s_syncMethodNames = ImmutableHashSet.Create(StringComparer.Ordinal, "Count", "LongCount");
        private static readonly ImmutableHashSet<string> s_asyncMethodNames = ImmutableHashSet.Create(StringComparer.Ordinal, "CountAsync", "LongCountAsync");

        /// <summary>
        /// Returns a set of descriptors for the diagnostics that this analyzer is capable of producing.
        /// </summary>
        /// <value>The supported diagnostics.</value>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(s_syncRule, s_asyncRule);

        /// <summary>
        /// Called once at session start to register actions in the analysis context.
        /// </summary>
        /// <param name="context">The context.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        /// <summary>
        /// Called on compilation start.
        /// </summary>
        /// <param name="context">The context.</param>
        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable) is INamedTypeSymbol enumerableType)
            {
                var operationActionsHandler = new OperationActionsHandler(
                    targetType: enumerableType,
                    targetMethodNames: s_syncMethodNames,
                    isAsync: false,
                    rule: s_syncRule);

                context.RegisterOperationAction(
                    operationActionsHandler.AnalyzeInvocationOperation,
                    OperationKind.Invocation);

                context.RegisterOperationAction(
                    operationActionsHandler.AnalyzeBinaryOperation,
                    OperationKind.BinaryOperator);
            }

            if (context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqQueryable) is INamedTypeSymbol queryableType)
            {
                var operationActionsHandler = new OperationActionsHandler(
                    targetType: queryableType,
                    targetMethodNames: s_syncMethodNames,
                    isAsync: false,
                    rule: s_syncRule);

                context.RegisterOperationAction(
                    operationActionsHandler.AnalyzeInvocationOperation,
                    OperationKind.Invocation);

                context.RegisterOperationAction(
                    operationActionsHandler.AnalyzeBinaryOperation,
                    OperationKind.BinaryOperator);
            }

            if (context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftEntityFrameworkCoreEntityFrameworkQueryableExtensions) is INamedTypeSymbol entityFrameworkQueryableExtensionsType)
            {
                var operationActionsHandler = new OperationActionsHandler(
                    targetType: entityFrameworkQueryableExtensionsType,
                    targetMethodNames: s_asyncMethodNames,
                    isAsync: true,
                    rule: s_asyncRule);

                context.RegisterOperationAction(
                    operationActionsHandler.AnalyzeInvocationOperation,
                    OperationKind.Invocation);

                context.RegisterOperationAction(
                    operationActionsHandler.AnalyzeBinaryOperation,
                    OperationKind.BinaryOperator);
            }

            if (context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataEntityQueryableExtensions) is INamedTypeSymbol queryableExtensionsType)
            {
                var operationActionsHandler = new OperationActionsHandler(
                    targetType: queryableExtensionsType,
                    targetMethodNames: s_asyncMethodNames,
                    isAsync: true,
                    rule: s_asyncRule);

                context.RegisterOperationAction(
                    operationActionsHandler.AnalyzeInvocationOperation,
                    OperationKind.Invocation);

                context.RegisterOperationAction(
                    operationActionsHandler.AnalyzeBinaryOperation,
                    OperationKind.BinaryOperator);
            }
        }

        /// <summary>
        /// Handler for operaction actions. This class cannot be inherited.
        /// </summary>
        private sealed class OperationActionsHandler
        {
            private readonly INamedTypeSymbol _targetType;
            private readonly ImmutableHashSet<string> _targetMethodNames;
            private readonly bool _isAsync;
            private readonly DiagnosticDescriptor _rule;

            public OperationActionsHandler(INamedTypeSymbol targetType, ImmutableHashSet<string> targetMethodNames, bool isAsync, DiagnosticDescriptor rule)
            {
                this._targetType = targetType;
                this._targetMethodNames = targetMethodNames;
                this._isAsync = isAsync;
                this._rule = rule;
            }

            /// <summary>
            /// Analyzes the invocation operation.
            /// </summary>
            /// <param name="context">The context.</param>
            public void AnalyzeInvocationOperation(OperationAnalysisContext context)
            {
                var invocationOperation = (IInvocationOperation)context.Operation;
                var method = invocationOperation.TargetMethod;

                if (invocationOperation.Arguments.Length == 1 &&
                    IsEqualsMethod(method))
                {
                    string operationKey;

                    if (IsCountEqualsZero(invocationOperation, out var methodName))
                    {
                        operationKey = OperationEqualsInstance;
                    }
                    else if (IsZeroEqualsCount(invocationOperation, out methodName))
                    {
                        operationKey = OperationEqualsArgument;
                    }
                    else
                    {
                        return;
                    }

                    var propertiesBuilder = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
                    propertiesBuilder.Add(OperationKey, operationKey);
                    propertiesBuilder.Add(ShouldNegateKey, null);
                    if (this._isAsync) propertiesBuilder.Add(IsAsyncKey, null);
                    var properties = propertiesBuilder.ToImmutable();

                    context.ReportDiagnostic(
                        invocationOperation.Syntax.CreateDiagnostic(
                            rule: this._rule,
                            properties: properties,
                            args: methodName));
                }
            }

            /// <summary>
            /// Analyzes the binary operation.
            /// </summary>
            /// <param name="context">The context.</param>
            public void AnalyzeBinaryOperation(OperationAnalysisContext context)
            {
                var binaryOperation = (IBinaryOperation)context.Operation;

                if (binaryOperation.IsComparisonOperator())
                {
                    string operationKey;

                    if (IsLeftCountComparison(binaryOperation, out var methodName, out var shouldNegate))
                    {
                        operationKey = OperationBinaryLeft;
                    }
                    else if (IsRightCountComparison(binaryOperation, out methodName, out shouldNegate))
                    {
                        operationKey = OperationBinaryRight;
                    }
                    else
                    {
                        return;
                    }

                    var propertiesBuilder = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
                    propertiesBuilder.Add(OperationKey, operationKey);
                    if (shouldNegate) propertiesBuilder.Add(ShouldNegateKey, null);
                    if (this._isAsync) propertiesBuilder.Add(IsAsyncKey, null);
                    var properties = propertiesBuilder.ToImmutable();

                    context.ReportDiagnostic(
                        binaryOperation.Syntax.CreateDiagnostic(
                            rule: this._rule,
                            properties: properties,
                            args: methodName));
                }
            }

            /// <summary>
            /// Checks if the given method is the <see cref="int.Equals(int)"/> method.
            /// </summary>
            /// <param name="methodSymbol">The method symbol.</param>
            /// <returns><see langword="true"/> if the given method is the <see cref="int.Equals(int)"/> method; otherwise, <see langword="false"/>.</returns>
            private static bool IsEqualsMethod(IMethodSymbol methodSymbol)
            {
                return string.Equals(methodSymbol.Name, WellKnownMemberNames.ObjectEquals, StringComparison.Ordinal) &&
                       (methodSymbol.ContainingType.SpecialType == SpecialType.System_Int32 ||
                            methodSymbol.ContainingType.SpecialType == SpecialType.System_UInt32 ||
                            methodSymbol.ContainingType.SpecialType == SpecialType.System_Int64 ||
                            methodSymbol.ContainingType.SpecialType == SpecialType.System_UInt64);
            }

            /// <summary>
            /// Checks whether the value of the invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />
            /// is being compared with 0 using <see cref="int.Equals(int)"/>.
            /// </summary>
            /// <param name="invocationOperation">The invocation operation.</param>
            ///
            ///
            /// <returns><see langword="true" /> if the value of the invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />
            /// is being compared with 0 using <see cref="int.Equals(int)"/>; otherwise, <see langword="false" />.</returns>
            private bool IsCountEqualsZero(IInvocationOperation invocationOperation, [NotNullWhen(returnValue: true)] out string? methodName)
            {
                if (!TryGetZeroOrOneConstant(invocationOperation.Arguments[0].Value, out var constant) || constant != 0)
                {
                    methodName = null;
                    return false;
                }

                return IsCountMethodInvocation(invocationOperation.Instance, out methodName);
            }

            /// <summary>
            /// Checks whether 0 is being compared with the value of the invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />
            /// using <see cref="int.Equals(int)"/>.
            /// </summary>
            /// <param name="invocationOperation">The invocation operation.</param>
            /// <returns><see langword="true" /> if 0 is being compared with the value of the invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />
            /// using <see cref="int.Equals(int)"/>; otherwise, <see langword="false" />.</returns>
            private bool IsZeroEqualsCount(IInvocationOperation invocationOperation, [NotNullWhen(returnValue: true)] out string? methodName)
            {
                if (!TryGetZeroOrOneConstant(invocationOperation.Instance, out var constant) || constant != 0)
                {
                    methodName = null;
                    return false;
                }

                return IsCountMethodInvocation(invocationOperation.Arguments[0].Value, out methodName);
            }

            /// <summary>
            /// Checks whether the value of the invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />
            /// is being compared with 0 or 1 using <see cref="int" /> comparison operators.
            /// </summary>
            /// <param name="binaryOperation">The binary operation.</param>
            /// <param name="methodName">If the value of the invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />, contains the method name; <see langword="null"/> otherwise.</param>
            /// <returns><see langword="true" /> if the value of the invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />
            /// is being compared with 0 or 1 using <see cref="int" /> comparison operators; otherwise, <see langword="false" />.</returns>
            private bool IsLeftCountComparison(IBinaryOperation binaryOperation, [NotNullWhen(returnValue: true)] out string? methodName, out bool shouldNegate)
            {
                methodName = null;
                shouldNegate = false;

                if (!TryGetZeroOrOneConstant(binaryOperation.RightOperand, out var constant))
                {
                    return false;
                }

                switch (constant)
                {
                    case 0:
                        switch (binaryOperation.OperatorKind)
                        {
                            case BinaryOperatorKind.Equals:
                            case BinaryOperatorKind.LessThanOrEqual:
                                shouldNegate = true;
                                break;
                            case BinaryOperatorKind.NotEquals:
                            case BinaryOperatorKind.GreaterThan:
                                shouldNegate = false;
                                break;
                            default:
                                return false;
                        }

                        break;
                    case 1:
                        switch (binaryOperation.OperatorKind)
                        {
                            case BinaryOperatorKind.LessThan:
                                shouldNegate = true;
                                break;
                            case BinaryOperatorKind.GreaterThanOrEqual:
                                shouldNegate = false;
                                break;
                            default:
                                return false;
                        }

                        break;
                    default:
                        return false;
                }

                return IsCountMethodInvocation(binaryOperation.LeftOperand, out methodName);
            }

            /// <summary>
            /// Checks whether 0 or 1 is being compared with the value of the invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />
            /// using <see cref="int" /> comparison operators.
            /// </summary>
            /// <param name="binaryOperation">The binary operation.</param>
            /// <param name="methodName">If the value of the invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />, contains the method name; <see langword="null"/> otherwise.</param>
            /// <returns><see langword="true" /> if 0 or 1 is being compared with the value of the invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />
            /// using <see cref="int" /> comparison operators; otherwise, <see langword="false" />.</returns>
            private bool IsRightCountComparison(IBinaryOperation binaryOperation, [NotNullWhen(returnValue: true)] out string? methodName, out bool shouldNegate)
            {
                methodName = null;
                shouldNegate = false;

                if (!TryGetZeroOrOneConstant(binaryOperation.LeftOperand, out var constant))
                {
                    return false;
                }

                switch (constant)
                {
                    case 0:
                        switch (binaryOperation.OperatorKind)
                        {
                            case BinaryOperatorKind.Equals:
                            case BinaryOperatorKind.LessThan:
                                shouldNegate = true;
                                break;

                            case BinaryOperatorKind.NotEquals:
                            case BinaryOperatorKind.GreaterThanOrEqual:
                                shouldNegate = false;
                                break;

                            default:
                                return false;
                        }

                        break;
                    case 1:
                        switch (binaryOperation.OperatorKind)
                        {
                            case BinaryOperatorKind.LessThanOrEqual:
                                shouldNegate = false;
                                break;

                            case BinaryOperatorKind.GreaterThan:
                                shouldNegate = true;
                                break;

                            default:
                                return false;
                        }

                        break;
                    default:
                        return false;
                }

                return IsCountMethodInvocation(binaryOperation.RightOperand, out methodName);
            }

            /// <summary>
            /// Tries the get an <see cref="int"/> constant from the <paramref name="operation"/>.
            /// </summary>
            /// <param name="operation">The operation.</param>
            /// <param name="constant">If this method returns <see langword="true"/>, this parameter is guaranteed to be 0 or 1; otherwise, it's meaningless.</param>
            /// <returns><see langword="true" /> <paramref name="operation"/> is a 0 or 1 constant, <see langword="false" /> otherwise.</returns>
            private static bool TryGetZeroOrOneConstant(IOperation operation, out int constant)
            {
                constant = default;

                if (operation?.Type?.SpecialType != SpecialType.System_Int32 &&
                    operation?.Type?.SpecialType != SpecialType.System_Int64 &&
                    operation?.Type?.SpecialType != SpecialType.System_UInt32 &&
                    operation?.Type?.SpecialType != SpecialType.System_UInt64 &&
                    operation?.Type?.SpecialType != SpecialType.System_Object)
                {
                    return false;
                }

                operation = operation.WalkDownConversion();

                var comparandValueOpt = operation.ConstantValue;

                if (!comparandValueOpt.HasValue)
                {
                    return false;
                }

                switch (comparandValueOpt.Value)
                {
                    case int intValue:

                        if (intValue >= 0 && intValue <= 1)
                        {
                            constant = intValue;
                            return true;
                        }

                        break;

                    case uint uintValue:

                        if (uintValue >= 0 && uintValue <= 1)
                        {
                            constant = (int)uintValue;
                            return true;
                        }

                        break;

                    case long longValue:

                        if (longValue >= 0 && longValue <= 1)
                        {
                            constant = (int)longValue;
                            return true;
                        }

                        break;

                    case ulong ulongValue:

                        if (ulongValue >= 0 && ulongValue <= 1)
                        {
                            constant = (int)ulongValue;
                            return true;
                        }

                        break;
                }

                return false;
            }

            /// <summary>
            /// Checks the <paramref name="operation" /> is an invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />.
            /// </summary>
            /// <param name="operation">The operation.</param>
            /// <param name="methodName">If the <paramref name="operation" /> is an invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />, contains the method name; <see langword="null"/> otherwise.</param>
            /// <returns><see langword="true" /> if the <paramref name="operation" /> is an invocation of one of the <see cref="_targetMethodNames" /> in the <see cref="_targetType" />;
            /// <see langword="false" /> otherwise.</returns>
            private bool IsCountMethodInvocation(IOperation operation, [NotNullWhen(returnValue: true)] out string? methodName)
            {
                methodName = null;

                operation = operation.WalkDownParentheses();

                operation = operation.WalkDownConversion();

                if (this._isAsync)
                {
                    if (operation is IAwaitOperation awaitOperation)
                    {
                        operation = awaitOperation.Operation;
                    }
                    else
                    {
                        return false;
                    }
                }

                operation = operation.WalkDownConversion();

                if (operation is IInvocationOperation invocationOperation &&
                    this._targetMethodNames.Contains(invocationOperation.TargetMethod.Name) &&
                    invocationOperation.TargetMethod.ContainingSymbol.Equals(this._targetType))
                {
                    methodName = invocationOperation.TargetMethod.Name;
                    return true;
                }

                return false;
            }
        }
    }
}
