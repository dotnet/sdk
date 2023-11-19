// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1861: Avoid constant arrays as arguments. Replace with static readonly arrays.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidConstArraysAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1861";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
            CreateLocalizableResourceString(nameof(AvoidConstArraysTitle)),
            CreateLocalizableResourceString(nameof(AvoidConstArraysMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(AvoidConstArraysDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var knownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                INamedTypeSymbol? readonlySpanType = knownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1);
                INamedTypeSymbol? functionType = knownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemFunc2);
                INamedTypeSymbol? attributeType = knownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttribute);

                // Analyzes an argument operation
                context.RegisterOperationAction(context =>
                {
                    IArgumentOperation? argumentOperation;

                    if (context.ContainingSymbol is IMethodSymbol method && method.MethodKind == MethodKind.StaticConstructor)
                    {
                        return;
                    }

                    if (context.Operation is IArrayCreationOperation arrayCreationOperation) // For arrays passed as arguments
                    {
                        argumentOperation = arrayCreationOperation.GetAncestor<IArgumentOperation>(OperationKind.Argument);

                        // If no argument, return
                        // If argument is passed as a params array but isn't itself an array, return
                        // If array is declared as an attribute argument, return
                        if (argumentOperation?.Parameter is null
                            || (argumentOperation.Parameter.IsParams && arrayCreationOperation.IsImplicit)
                            || (argumentOperation.Parent is IObjectCreationOperation objectCreation && objectCreation.Type.Inherits(attributeType)))
                        {
                            return;
                        }
                    }
                    else if (context.Operation is IInvocationOperation invocationOperation) // For arrays passed in extension methods, like in LINQ
                    {
                        IEnumerable<IOperation> invocationDescendants = invocationOperation.Descendants();
                        if (invocationDescendants.Any(x => x is IArrayCreationOperation)
                            && invocationDescendants.Any(x => x is IArgumentOperation))
                        {
                            // This is an invocation that contains an array as an argument
                            // This will get caught by the first case in another cycle
                            return;
                        }

                        argumentOperation = invocationOperation.Arguments.FirstOrDefault();
                        if (argumentOperation is not null)
                        {
                            if (argumentOperation.Value is not IConversionOperation conversionOperation
                                || conversionOperation.Operand is not IArrayCreationOperation arrayCreation)
                            {
                                return;
                            }

                            arrayCreationOperation = arrayCreation;
                        }
                        else // An invocation, extension or regular, has an argument, unless it's a VB extension method call
                        {
                            // For VB extension method invocations, find a matching child
                            arrayCreationOperation = (IArrayCreationOperation)invocationDescendants
                                .FirstOrDefault(x => x is IArrayCreationOperation);
                            if (arrayCreationOperation is null)
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        return;
                    }

                    // Must be literal array
                    if (arrayCreationOperation.Initializer is not { } initializer ||
                        initializer.ElementValues.Any(x => x is not ILiteralOperation))
                    {
                        return;
                    }

                    string? paramName = null;
                    if (argumentOperation?.Parameter is not null)
                    {
                        if (IsInitializingStaticOrReadOnlyFieldOrProperty(argumentOperation))
                        {
                            return;
                        }

                        ITypeSymbol? originalDefinition = argumentOperation.Parameter?.Type.OriginalDefinition;

                        // Can't be a ReadOnlySpan, as those are already optimized
                        if (originalDefinition == null ||
                            SymbolEqualityComparer.Default.Equals(readonlySpanType, originalDefinition))
                        {
                            return;
                        }

                        // Check if the parameter is a function so the name can be set to null
                        // Otherwise, the parameter name doesn't reflect the array creation as well
                        bool isDirectlyInsideLambda = originalDefinition.Equals(functionType);

                        // Parameter shouldn't have same containing type as the context, to prevent naming ambiguity
                        // Ignore parameter name if we're inside a lambda function
                        if (!isDirectlyInsideLambda && !argumentOperation.Parameter!.ContainingType.Equals(context.ContainingSymbol.ContainingType))
                        {
                            paramName = argumentOperation.Parameter.Name;
                        }
                    }

                    ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
                    properties.Add("paramName", paramName);

                    context.ReportDiagnostic(arrayCreationOperation.CreateDiagnostic(Rule, properties.ToImmutable()));
                },
                OperationKind.ArrayCreation,
                OperationKind.Invocation);
            });
        }

        private static bool IsInitializingStaticOrReadOnlyFieldOrProperty(IOperation operation)
        {
            var ancestor = operation;
            do
            {
                ancestor = ancestor!.Parent;
            } while (ancestor != null && !(ancestor.Kind == OperationKind.FieldInitializer || ancestor.Kind == OperationKind.PropertyInitializer ||
                        ancestor.Kind == OperationKind.CoalesceAssignment || ancestor.Kind == OperationKind.SimpleAssignment));

            if (ancestor != null)
            {
                switch (ancestor)
                {
                    case IFieldInitializerOperation fieldInitializer:
                        return fieldInitializer.InitializedFields.Any(x => x.IsStatic || x.IsReadOnly);
                    case IPropertyInitializerOperation propertyInitializer:
                        return propertyInitializer.InitializedProperties.Any(x => x.IsStatic || x.IsReadOnly);
                    case IAssignmentOperation assignmentOperation:
                        if (assignmentOperation.Target is IFieldReferenceOperation fieldReference && fieldReference.Field.IsStatic)
                        {
                            return true;
                        }

                        if (assignmentOperation.Target is IPropertyReferenceOperation propertyReference && propertyReference.Property.IsStatic)
                        {
                            return true;
                        }

                        break;
                }
            }

            return false;
        }
    }
}
