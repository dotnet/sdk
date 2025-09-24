﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA2244: <inheritdoc cref="AvoidDuplicateElementInitializationTitle"/>
    /// </summary>
#pragma warning disable RS1004 // Recommend adding language support to diagnostic analyzer - Construct impossible in VB.NET
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1004 // Recommend adding language support to diagnostic analyzer
    public sealed class AvoidDuplicateElementInitialization : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2244";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidDuplicateElementInitializationTitle)),
            CreateLocalizableResourceString(nameof(AvoidDuplicateElementInitializationMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(AvoidDuplicateElementInitializationDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(AnalyzeOperation, OperationKind.ObjectCreation);
        }

        private static void AnalyzeOperation(OperationAnalysisContext context)
        {
            var objectInitializer = (IObjectCreationOperation)context.Operation;
            if (objectInitializer.Initializer == null ||
                objectInitializer.Initializer.Initializers.Length <= 1)
            {
                return;
            }

            var initializedElementIndexes = new HashSet<ImmutableArray<object>>(ConstantArgumentEqualityComparer.Instance);

            for (int i = objectInitializer.Initializer.Initializers.Length - 1; i >= 0; i--)
            {
                if (objectInitializer.Initializer.Initializers[i] is ISimpleAssignmentOperation assignment &&
                    assignment.Target is IPropertyReferenceOperation propertyReference &&
                    !propertyReference.Arguments.IsEmpty)
                {
                    var values = GetConstantArgumentValues(propertyReference.Arguments);
                    if (!values.IsEmpty && !initializedElementIndexes.Add(values))
                    {
                        var indexesText = string.Join(", ", values.Select(value => value?.ToString() ?? "null"));
                        context.ReportDiagnostic(
                            Diagnostic.Create(Rule, propertyReference.Syntax.GetLocation(),
                                additionalLocations: ImmutableArray.Create(assignment.Syntax.GetLocation()),
                                messageArgs: indexesText));
                    }
                }
            }
        }

        /// <summary>
        /// Gets the argument values in parameter order, filling in defaults if necessary, if all
        /// arguments are constants. Otherwise, returns null.
        /// </summary>
        private static ImmutableArray<object> GetConstantArgumentValues(ImmutableArray<IArgumentOperation> arguments)
        {
            var result = new object[arguments.Length];
            foreach (var argument in arguments)
            {
                var parameter = argument.Parameter;
                if (parameter == null ||
                    parameter.Ordinal >= result.Length ||
                    !argument.Value.ConstantValue.HasValue ||
                    argument.Value.ConstantValue.Value is not { } value)
                {
                    return ImmutableArray<object>.Empty;
                }

                result[parameter.Ordinal] = value;
            }

            return result.ToImmutableArray();
        }

        private sealed class ConstantArgumentEqualityComparer : IEqualityComparer<ImmutableArray<object>>
        {
            public static readonly ConstantArgumentEqualityComparer Instance = new();

            private readonly EqualityComparer<object> _objectComparer = EqualityComparer<object>.Default;

            private ConstantArgumentEqualityComparer() { }

            bool IEqualityComparer<ImmutableArray<object>>.Equals(ImmutableArray<object> x, ImmutableArray<object> y)
            {
                if (x == y)
                {
                    return true;
                }

                return x != null && y != null && x.SequenceEqual(y, _objectComparer);
            }

            int IEqualityComparer<ImmutableArray<object>>.GetHashCode(ImmutableArray<object> obj)
            {
                int hash = 0;
                if (obj != null)
                {
                    foreach (var item in obj)
                    {
                        hash = unchecked((hash * (int)0xA5555529) + _objectComparer.GetHashCode(item));
                    }
                }

                return hash;
            }
        }
    }
}