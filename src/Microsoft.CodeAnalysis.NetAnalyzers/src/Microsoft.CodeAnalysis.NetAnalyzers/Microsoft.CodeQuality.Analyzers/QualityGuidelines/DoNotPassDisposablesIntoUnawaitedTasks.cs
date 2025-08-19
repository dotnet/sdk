// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA2025: <inheritdoc cref="DoNotPassDisposablesIntoUnawaitedTasksTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotPassDisposablesIntoUnawaitedTasksAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2025";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotPassDisposablesIntoUnawaitedTasksTitle)),
            CreateLocalizableResourceString(nameof(DoNotPassDisposablesIntoUnawaitedTasksMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(DoNotPassDisposablesIntoUnawaitedTasksDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(context =>
            {
                var provider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                if (!provider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIDisposable, out var iDisposable))
                {
                    return;
                }

                var invocation = (IInvocationOperation)context.Operation;

                // Only care about tasks
                if (!invocation.IsTask())
                {
                    return;
                }

                // Ignore if awaited or run synchronously with task.Result or task.Wait()
                if (invocation.IsAwaited())
                {
                    return;
                }

                // Only care about invocations that receive IDisposable's as args
                if (!invocation.Arguments.AnyWhere(arg => arg.Parameter?.Type?.AllInterfaces is { Length: > 0 } allInterfaces &&
                    allInterfaces.Any(i => i.ToString() == WellKnownTypeNames.SystemIDisposable), out var disposableArguments))
                {
                    return;
                }

                var referencedDisposableArgs = GetReferencedDisposableArguments(invocation, disposableArguments);

                foreach (var referencedArg in referencedDisposableArgs)
                {
                    context.ReportDiagnostic(referencedArg.CreateDiagnostic(Rule));
                }
            }, OperationKind.Invocation);
        }

        private static List<ILocalReferenceOperation> GetReferencedDisposableArguments(IInvocationOperation invocation,
            IList<IArgumentOperation> disposableArguments)
        {
            // Get the references of the disposable arguments
            var disposableArgumentReferences = GetLocalReferencesFromArguments(disposableArguments).ToList();

            // We use the inner method body only for checking disposable usage
            IOperation? containingBlock = invocation.GetAncestor<IMethodBodyOperation>(OperationKind.MethodBody);
            // In VB the real containing block is higher, especially if there are using blocks
            containingBlock ??= invocation.GetRoot();

            var descendants = containingBlock.Descendants().ToList();
            var localReferences = descendants.OfType<ILocalReferenceOperation>();

            // Get declarator for invocation and verify the invocation is the retrieved declarator's initializer value
            var declaratorForInvocation = invocation.GetAncestor<IVariableDeclaratorOperation>(OperationKind.VariableDeclarator,
                decl => decl.Initializer?.Value == invocation);
            // VB has slightly different structure for getting to declarator
            declaratorForInvocation ??= invocation.GetAncestor<IVariableDeclarationOperation>(OperationKind.VariableDeclaration)?
                .Declarators.FirstOrDefault();

            bool contextContainsDisposeCalls = localReferences.AnyWhere(r => r.Parent
                is IInvocationOperation { TargetMethod.Name: nameof(IDisposable.Dispose) }, out var disposeCalls);

            // We can skip reporting ONLY if the disposable arguments are disposed AFTER the task is awaited
            if (declaratorForInvocation is { } && contextContainsDisposeCalls)
            {
                var disposeCallsThatDisposeReferencedArgs = disposeCalls.Where(disposeCall =>
                    disposableArgumentReferences.Any(disposableArg => disposeCall.Local.Equals(disposableArg.Local)));

                // See if the task is referenced and awaited elsewhere
                var awaitedInvocationReference = localReferences.FirstOrDefault(r => r.IsAwaited() &&
                    r.Local.Equals(declaratorForInvocation.Symbol));
                if (awaitedInvocationReference is not null)
                {
                    // Check if all disposals of arguments into the task are after the task is awaited
                    bool eachDisposeIsAferTaskIsAwaited = true;
                    foreach (var disposeCall in disposeCallsThatDisposeReferencedArgs)
                    {
                        if (disposeCall.Syntax.SpanStart < awaitedInvocationReference.Syntax.SpanStart)
                        {
                            eachDisposeIsAferTaskIsAwaited = false;
                            break;
                        }
                    }

                    // Do not report if arguments are disposed after the task is awaited elsewhere
                    if (eachDisposeIsAferTaskIsAwaited)
                    {
                        return new List<ILocalReferenceOperation>(0);
                    }
                }
            }

            var referencedDisposableArgs = new List<ILocalReferenceOperation>();

            // Check if the disposable argument references originate from using statements
            if (descendants.OfType<IUsingOperation>().ToList() is { Count: > 0 } usingBlocks)
            {
                List<ILocalSymbol> usingLocals = new();
                usingBlocks.ForEach(u => usingLocals.AddRange(u.Locals));

                // Add all argument references that originate from using statements
                referencedDisposableArgs.AddRange(disposableArgumentReferences.Where(disposableRef =>
                {
                    foreach (var usingLocal in usingLocals)
                    {
                        if (usingLocal.Equals(disposableRef))
                        {
                            return true;
                        }
                    }

                    return false;
                }));
            }
            else if (!descendants.OfType<IUsingDeclarationOperation>().Any() &&
                !contextContainsDisposeCalls)
            {
                // If we have no using blocks/statements and no Dispose calls, nothing to report
                return new List<ILocalReferenceOperation>(0);
            }

            // Add all references to disposable args not already caught with previous logic
            referencedDisposableArgs.AddRange(localReferences.Intersect(disposableArgumentReferences));

            return referencedDisposableArgs;
        }

        private static IEnumerable<ILocalReferenceOperation> GetLocalReferencesFromArguments(IList<IArgumentOperation> args)
        {
            return args.Select(disposableArg =>
            {
                // Either a converted reference
                if (disposableArg.Value is IConversionOperation conversion
                    && conversion.Operand is ILocalReferenceOperation convertedLocalReference)
                {
                    return convertedLocalReference;
                }

                // Or an unconverted reference
                return (disposableArg.Value as ILocalReferenceOperation)!;
            });
        }
    }

    internal static class TaskOperationExtensions
    {
        public static bool IsAwaited(this IOperation op)
        {
            return op.GetAncestor<IAwaitOperation>(OperationKind.Await) is not null ||
                op.Parent is IInvocationOperation { TargetMethod.Name: "Wait" } or
                IPropertyReferenceOperation { Property.Name: "Result" };
        }

        public static bool IsTask(this IOperation op)
        {
            return op.Type?.ToString() == WellKnownTypeNames.SystemThreadingTasksTask ||
                op.Type?.BaseType?.ToString() == WellKnownTypeNames.SystemThreadingTasksTask;
        }
    }

    internal static class EnumerableExtensions
    {
        public static bool AnyWhere<T>(this IEnumerable<T> collection, Predicate<T> predicate, out IList<T> matches)
        {
            bool anyMatches = false;
            IEnumerable<T> GetWhere()
            {
                foreach (var item in collection)
                {
                    if (predicate(item))
                    {
                        anyMatches = true;
                        yield return item;
                    }
                }
            }

            // ToList actually evaluates enumerable so anyMatches will be accurate
            matches = [.. GetWhere()];
            return anyMatches;
        }
    }
}
