// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>
    /// CA1848: Use the LoggerMessage delegates
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    [Shared]
    public sealed partial class CSharpUseCompiledLogMessagesFixer : CodeFixProvider
    {
        // Mimics the definition from Microsoft.Extensions.Logging.Abstractions
        internal enum LogLevel
        {
            Trace = 0,
            Debug = 1,
            Information = 2,
            Warning = 3,
            Error = 4,
            Critical = 5,
            None = 6,
        }

        // Function pointers that can be patched by test code to exercise obscure failure paths
        internal Func<Document, CancellationToken, Task<SyntaxNode?>> GetSyntaxRootAsync = (d, t) => d.GetSyntaxRootAsync(t);
        internal Func<Document, CancellationToken, Task<SemanticModel?>> GetSemanticModelAsync = (d, t) => d.GetSemanticModelAsync(t);
        internal Func<SemanticModel, SyntaxNode, CancellationToken, IOperation?> GetOperation = (sm, sn, t) => sm.GetOperation(sn, t);
        internal Func<Compilation, string, INamedTypeSymbol?> GetTypeByMetadataName1 = (c, n) => c.GetTypeByMetadataName(n);
        internal Func<Compilation, string, INamedTypeSymbol?> GetTypeByMetadataName2 = (c, n) => c.GetTypeByMetadataName(n);
        internal Func<Compilation, string, INamedTypeSymbol?> GetTypeByMetadataName3 = (c, n) => c.GetTypeByMetadataName(n);
        internal Func<SemanticModel, BaseMethodDeclarationSyntax, CancellationToken, IMethodSymbol?> GetDeclaredSymbol = (sm, m, t) => sm.GetDeclaredSymbol(m, t);

        private const string LoggerMessageAttribute = "Microsoft.Extensions.Logging.LoggerMessageAttribute";
        private const string CA1848RuleId = "CA1848";

        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(CA1848RuleId);

        /// <inheritdoc/>
        public override FixAllProvider? GetFixAllProvider() => null;

        private const string CodeFixTitle = "Generate strongly-typed logging method";

        /// <inheritdoc/>
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var (invocationExpression, details) = await CheckIfCanFixAsync(context.Document, context.Span, context.CancellationToken).ConfigureAwait(false);
            if (invocationExpression != null && details != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixTitle,
                        createChangedSolution: cancellationToken => ApplyFixAsync(context.Document, invocationExpression, details, cancellationToken),
                        equivalenceKey: CodeFixTitle),
                    context.Diagnostics);
            }
        }

        internal async Task<(ExpressionSyntax? invocationExpression, FixDetails? details)>
            CheckIfCanFixAsync(Document invocationDoc, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await GetSyntaxRootAsync(invocationDoc, cancellationToken).ConfigureAwait(false);
            if (root?.FindNode(span) is not ExpressionSyntax invocationExpression)
            {
                // Shouldn't happen, we only get called for invocations
                return (null, null);
            }

            var sm = await GetSemanticModelAsync(invocationDoc, cancellationToken).ConfigureAwait(false);
            if (sm == null)
            {
                // Shouldn't happen
                return (null, null);
            }

            var comp = sm.Compilation;

            var loggerExtensions = GetTypeByMetadataName1(comp, "Microsoft.Extensions.Logging.LoggerExtensions");
            if (loggerExtensions == null)
            {
                // Shouldn't happen, we only get called for methods on this type
                return (null, null);
            }

            var invocationOp = GetOperation(sm, invocationExpression, cancellationToken) as IInvocationOperation;
            if (invocationOp == null)
            {
                // Shouldn't happen, we're dealing with an invocation expression
                return (null, null);
            }

            var method = invocationOp.TargetMethod;

            var details = new FixDetails(method, invocationOp, invocationDoc.Project.DefaultNamespace, invocationDoc.Project.Documents);

            if (string.IsNullOrWhiteSpace(details.Message))
            {
                // Can't auto-generate without a valid message string
                return (null, null);
            }

            if (details.EventIdParamIndex >= 0)
            {
                // Can't auto-generate the variants using event id
                return (null, null);
            }

            if (string.IsNullOrWhiteSpace(details.Level))
            {
                // Can't auto-generate without a valid level
                return (null, null);
            }

            return (invocationExpression, details);
        }

        /// <summary>
        /// Get the final name of the target method. If there's an existing method with the right
        /// message, level, and argument types, we just use that. Otherwise, we create a new method.
        /// </summary>
        internal async Task<(string methodName, bool existing)> GetFinalTargetMethodNameAsync(
            Document targetDoc,
            ClassDeclarationSyntax targetClass,
            Document invocationDoc,
            ExpressionSyntax invocationExpression,
            FixDetails details,
            CancellationToken cancellationToken)
        {
            var invocationSM = (await invocationDoc.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false))!;
            var invocationOp = (invocationSM.GetOperation(invocationExpression, cancellationToken) as IInvocationOperation)!;

            var docEditor = await DocumentEditor.CreateAsync(targetDoc, cancellationToken).ConfigureAwait(false);
            var sm = docEditor.SemanticModel;
            var comp = sm.Compilation;

            var logMethodAttribute = GetTypeByMetadataName2(comp, LoggerMessageAttribute);
            if (logMethodAttribute is null)
            {
                // Strange that we can't find the attribute, but supply a potential useful value instead
                return (details.TargetMethodName, false);
            }

            var invocationArgList = MakeArgumentList(details, invocationOp);

            var conflict = false;
            var count = 2;
            string methodName;
            do
            {
                methodName = details.TargetMethodName;
                if (conflict)
                {
                    methodName = $"{methodName}{count}";
                    count++;
                    conflict = false;
                }

                foreach (var method in targetClass.Members.Where(m => m.IsKind(SyntaxKind.MethodDeclaration)).OfType<MethodDeclarationSyntax>())
                {
                    var methodSymbol = GetDeclaredSymbol(sm, method, cancellationToken);
                    if (methodSymbol == null)
                    {
                        // Hmmm, this shouldn't happen should it?
                        continue;
                    }

                    var matchName = method.Identifier.ToString() == methodName;

                    var matchParams = invocationArgList.Count == methodSymbol.Parameters.Length;
                    if (matchParams)
                    {
                        for (int i = 0; i < invocationArgList.Count; i++)
                        {
                            matchParams = invocationArgList[i].Equals(methodSymbol.Parameters[i].Type, SymbolEqualityComparer.Default);
                            if (!matchParams)
                            {
                                break;
                            }
                        }
                    }

                    if (matchName && matchParams)
                    {
                        conflict = true;
                    }

                    foreach (var methodAttr in methodSymbol.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(methodAttr.AttributeClass, logMethodAttribute) &&
                            methodAttr.AttributeConstructor is not null)
                        {
                            var argLevel = GetLogMethodAttributeParameter(methodAttr.AttributeConstructor.Parameters, methodAttr.ConstructorArguments, "level");
                            if (!argLevel.HasValue)
                            {
                                break;
                            }

                            var level = (LogLevel)argLevel.Value.Value!;

                            var argMessage = GetLogMethodAttributeParameter(methodAttr.AttributeConstructor.Parameters, methodAttr.ConstructorArguments, "message");
                            if (!argMessage.HasValue)
                            {
                                break;
                            }

                            var message = argMessage.Value.Value!.ToString();

                            var matchMessage = message == details.Message;
                            var matchLevel = FixDetails.GetLogLevelName(level) == details.Level;

                            if (matchLevel && matchMessage && matchParams)
                            {
                                // Found a match, use this one
                                return (method.Identifier.ToString(), true);
                            }

                            break;
                        }
                    }
                }
            }
            while (conflict);

            return (methodName, false);
        }

        private static TypedConstant? GetLogMethodAttributeParameter(
            ImmutableArray<IParameterSymbol> attributeCtorParams,
            ImmutableArray<TypedConstant> constructorArguments,
            string paramName)
        {
            foreach (var param in attributeCtorParams)
            {
                if (param.Name == paramName)
                {
                    foreach (var ctorArg in constructorArguments)
                    {
                        if (SymbolEqualityComparer.Default.Equals(ctorArg.Type, param.Type))
                        {
                            return ctorArg;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the class into which to create the logging method signature, or creates it if it doesn't exist.
        /// </summary>
        private static async Task<(Solution solution, ClassDeclarationSyntax declarationSyntax, Document document)>
            GetOrMakeTargetClassAsync(Project proj, FixDetails details, CancellationToken cancellationToken)
        {
            while (true)
            {
                var comp = (await proj.GetCompilationAsync(cancellationToken).ConfigureAwait(false))!;
                var allNodes = comp.SyntaxTrees.SelectMany(s => s.GetRoot().DescendantNodes());
                var allClasses = allNodes.Where(d => d.IsKind(SyntaxKind.ClassDeclaration)).OfType<ClassDeclarationSyntax>();
                foreach (var cl in allClasses)
                {
                    var nspace = GetNamespace(cl);
                    if (nspace != details.TargetNamespace)
                    {
                        continue;
                    }

                    if (cl.Identifier.Text == details.TargetClassName)
                    {
                        return (proj.Solution, cl, proj.GetDocument(cl.SyntaxTree)!);
                    }
                }

                var text = $@"
#pragma warning disable CS8019
using Microsoft.Extensions.Logging;
using System;
#pragma warning restore CS8019

static partial class {details.TargetClassName}
{{
}}
";

                if (!string.IsNullOrEmpty(details.TargetNamespace))
                {
                    text = $@"
namespace {details.TargetNamespace}
{{
#pragma warning disable CS8019
    using Microsoft.Extensions.Logging;
    using System;
#pragma warning restore CS8019

    static partial class {details.TargetClassName}
    {{
    }}
}}
";
                }

                proj = proj.AddDocument(details.TargetFilename, text).Project;
            }
        }

        /// <summary>
        /// Remaps an invocation expression to a new doc.
        /// </summary>
        private static async Task<(Document document, ExpressionSyntax expressionSyntax)>
            RemapAsync(Solution sol, DocumentId docId, ExpressionSyntax invocationExpression)
        {
            var doc = sol.GetDocument(docId)!;
            var root = await doc.GetSyntaxRootAsync().ConfigureAwait(false);

            return (doc, (root!.FindNode(invocationExpression.Span) as ExpressionSyntax)!);
        }

        private static string GetNamespace(ClassDeclarationSyntax cl)
        {
            SyntaxNode? parent = cl.Parent;
            var namespaceNames = new List<string>();

            while (parent != null)
            {
                switch (parent)
                {
                    case NamespaceDeclarationSyntax namespaceDecl:
                        namespaceNames.Insert(0, namespaceDecl.Name.ToString());
                        break;
                    case ClassDeclarationSyntax:
                        // Nested type, we don't do those
                        return "<+Invalid Namespace+>";
                }

                // Also check for file-scoped namespace (C# 10+) via syntax kind
                // SyntaxKind.FileScopedNamespaceDeclaration = 8845
                if (parent.RawKind == 8845)
                {
                    // Get the name from the file-scoped namespace via reflection
                    var nameProperty = parent.GetType().GetProperty("Name");
                    if (nameProperty?.GetValue(parent) is NameSyntax name)
                    {
                        namespaceNames.Insert(0, name.ToString());
                    }
                }

                parent = parent.Parent;
            }

            return string.Join(".", namespaceNames);
        }

        /// <summary>
        /// Given a LoggerExtensions method invocation, produce a parameter list for the corresponding generated logging method.
        /// </summary>
        private static List<SyntaxNode> MakeParameterList(
            FixDetails details,
            IInvocationOperation invocationOp,
            SyntaxGenerator gen)
        {
            var t = invocationOp.Arguments[0].Value.Type!;
            var loggerType = gen.TypeExpression(t);
            if (invocationOp.Parent?.Kind == OperationKind.ConditionalAccess)
            {
                loggerType = gen.TypeExpression(t.WithNullableAnnotation(NullableAnnotation.Annotated));
            }

            var loggerParam = gen.ParameterDeclaration("logger", loggerType);
            if (loggerParam is ParameterSyntax parameterSyntax)
            {
                loggerParam = parameterSyntax.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ThisKeyword)));
            }

            var parameters = new List<SyntaxNode>
            {
                loggerParam
            };

            if (details.ExceptionParamIndex >= 0)
            {
                parameters.Add(gen.ParameterDeclaration("exception", gen.TypeExpression(invocationOp.Arguments[details.ExceptionParamIndex].Value.Type!)));
            }

            var index = 0;
            if (details.InterpolationArgs != null)
            {
                foreach (var o in details.InterpolationArgs)
                {
                    parameters.Add(gen.ParameterDeclaration(details.MessageArgs[index++], gen.TypeExpression(o.Type!)));
                }
            }

            var paramsArg = invocationOp.Arguments[details.ArgsParamIndex];
            if (paramsArg != null)
            {
                var arrayCreation = (IArrayCreationOperation)paramsArg.Value;
                foreach (var e in arrayCreation.Initializer!.ElementValues)
                {
                    var type = e.SemanticModel?.GetTypeInfo(e.Syntax).Type!;

                    string name;
                    if (index < details.MessageArgs.Count)
                    {
                        name = details.MessageArgs[index];
                    }
                    else
                    {
                        name = $"arg{index}";
                    }

                    parameters.Add(gen.ParameterDeclaration(name, gen.TypeExpression(type)));
                    index++;
                }
            }

            return parameters;
        }

        /// <summary>
        /// Given a LoggerExtensions method invocation, produce an argument list in the shape of a corresponding generated logging method.
        /// </summary>
        private static List<ITypeSymbol> MakeArgumentList(FixDetails details, IInvocationOperation invocationOp)
        {
            var args = new List<ITypeSymbol>
            {
                invocationOp.Arguments[0].Value.Type!
            };

            if (details.ExceptionParamIndex >= 0)
            {
                args.Add(invocationOp.Arguments[details.ExceptionParamIndex].Value.Type!);
            }

            if (details.InterpolationArgs != null)
            {
                foreach (var a in details.InterpolationArgs)
                {
                    args.Add(a.Type!);
                }
            }

            var paramsArg = invocationOp.Arguments[details.ArgsParamIndex];
            if (paramsArg != null)
            {
                var arrayCreation = (IArrayCreationOperation)paramsArg.Value;
                foreach (var e in arrayCreation.Initializer!.ElementValues)
                {
                    foreach (var d in e.Descendants())
                    {
                        args.Add(d.Type!);
                    }
                }
            }

            return args;
        }

        private static async Task<Solution> RewriteLoggingCallAsync(
            Document doc,
            ExpressionSyntax invocationExpression,
            FixDetails details,
            string methodName,
            CancellationToken cancellationToken)
        {
            var solEditor = new SolutionEditor(doc.Project.Solution);
            var docEditor = await solEditor.GetDocumentEditorAsync(doc.Id, cancellationToken).ConfigureAwait(false);
            var sm = docEditor.SemanticModel;
            var gen = docEditor.Generator;
            var invocation = sm.GetOperation(invocationExpression, cancellationToken) as IInvocationOperation;
            var argList = new List<SyntaxNode>();

            int index = 0;
            SyntaxNode loggerSyntaxNode = null!;
            foreach (var arg in invocation!.Arguments)
            {
                if ((index == details.MessageParamIndex) || (index == details.LogLevelParamIndex))
                {
                    index++;
                    continue;
                }

                index++;

                if (index == 1)
                {
                    loggerSyntaxNode = arg.Syntax;
                }
                else
                {
                    if (arg.ArgumentKind == ArgumentKind.ParamArray)
                    {
                        if (details.InterpolationArgs != null)
                        {
                            foreach (var a in details.InterpolationArgs)
                            {
                                argList.Add(a.Syntax.WithoutTrivia());
                            }
                        }

                        var arrayCreation = (IArrayCreationOperation)arg.Value;
                        foreach (var e in arrayCreation.Initializer!.ElementValues)
                        {
                            argList.Add(e.Syntax.WithoutTrivia());
                        }
                    }
                    else
                    {
                        argList.Add(arg.Syntax.WithoutTrivia());
                    }
                }
            }

            var memberAccessExpression = gen.MemberAccessExpression(loggerSyntaxNode!, methodName);
            var call = gen.InvocationExpression(memberAccessExpression, argList).WithTriviaFrom(invocationExpression);

            if (invocationExpression.Parent!.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                invocationExpression = (ExpressionSyntax)invocationExpression.Parent;
            }

            docEditor.ReplaceNode(invocationExpression, call);

            return solEditor.GetChangedSolution();
        }

        /// <summary>
        /// Orchestrate all the work needed to fix an issue.
        /// </summary>
        private async Task<Solution> ApplyFixAsync(Document invocationDoc, ExpressionSyntax invocationExpression, FixDetails details, CancellationToken cancellationToken)
        {
            ClassDeclarationSyntax targetClass;
            Document targetDoc;
            Solution sol;

            // Stable id surviving across solution generations
            var invocationDocId = invocationDoc.Id;

            // Get a reference to the class where to insert the logging method, creating it if necessary
            (sol, targetClass, targetDoc) = await GetOrMakeTargetClassAsync(invocationDoc.Project, details, cancellationToken).ConfigureAwait(false);

            // Find the doc and invocation in the current solution
            (invocationDoc, invocationExpression) = await RemapAsync(sol, invocationDocId, invocationExpression).ConfigureAwait(false);

            // Determine the final name of the logging method and whether we need to generate it or not
            var (methodName, existing) = await GetFinalTargetMethodNameAsync(targetDoc, targetClass, invocationDoc, invocationExpression, details, cancellationToken).ConfigureAwait(false);

            // If the target method doesn't already exist, go make it
            if (!existing)
            {
                // Generate the logging method signature in the target class
                sol = await InsertLoggingMethodSignatureAsync(targetDoc, targetClass, invocationDoc, invocationExpression, details, cancellationToken).ConfigureAwait(false);

                // Find the doc and invocation in the current solution
                (invocationDoc, invocationExpression) = await RemapAsync(sol, invocationDocId, invocationExpression).ConfigureAwait(false);
            }

            // Rewrite the call site to invoke the generated logging method
            sol = await RewriteLoggingCallAsync(invocationDoc, invocationExpression, details, methodName, cancellationToken).ConfigureAwait(false);

            return sol;
        }

        private async Task<Solution> InsertLoggingMethodSignatureAsync(
            Document targetDoc,
            ClassDeclarationSyntax targetClass,
            Document invocationDoc,
            ExpressionSyntax invocationExpression,
            FixDetails details,
            CancellationToken cancellationToken)
        {
            var invocationSM = (await invocationDoc.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false))!;
            var invocationOp = (invocationSM.GetOperation(invocationExpression, cancellationToken) as IInvocationOperation)!;

            var solEditor = new SolutionEditor(targetDoc.Project.Solution);
            var docEditor = await solEditor.GetDocumentEditorAsync(targetDoc.Id, cancellationToken).ConfigureAwait(false);
            var sm = docEditor.SemanticModel;
            var comp = sm.Compilation;
            var gen = docEditor.Generator;

            var logMethod = gen.MethodDeclaration(
                                details.TargetMethodName,
                                MakeParameterList(details, invocationOp, gen),
                                accessibility: Accessibility.Internal,
                                modifiers: DeclarationModifiers.Partial | DeclarationModifiers.Static);

            var attrArgs = new[]
            {
                gen.LiteralExpression(CalcEventId(comp, targetClass, cancellationToken)),
                gen.MemberAccessExpression(gen.TypeExpression(comp.GetTypeByMetadataName("Microsoft.Extensions.Logging.LogLevel")!), details.Level),
                gen.LiteralExpression(details.Message),
            };

            var attr = gen.Attribute(LoggerMessageAttribute, attrArgs);

            logMethod = gen.AddAttributes(logMethod, attr);

            var line = SyntaxFactory.ParseLeadingTrivia($@"
");
            logMethod = logMethod.WithLeadingTrivia(line);

            docEditor.AddMember(targetClass, logMethod);

            return solEditor.GetChangedSolution();
        }

        /// <summary>
        /// Iterate through the existing methods in the target class
        /// and look at any method annotated with [LoggerMessage],
        /// get their event ids, and then return 1 larger than any event id
        /// found.
        /// </summary>
        private int CalcEventId(Compilation comp, ClassDeclarationSyntax targetClass, CancellationToken cancellationToken)
        {
            var logMethodAttribute = GetTypeByMetadataName3(comp, LoggerMessageAttribute);
            if (logMethodAttribute is null)
            {
                // Strange we can't find the attribute, but supply a potential useful value instead
                return targetClass.Members.Count + 1;
            }

            var max = 0;
            var semanticModel = comp.GetSemanticModel(targetClass.SyntaxTree);
            var targetClassSymbol = semanticModel.GetDeclaredSymbol(targetClass, cancellationToken);
            if (targetClassSymbol is null || targetClassSymbol is IErrorTypeSymbol)
            {
                return max;
            }

            foreach (var methodSymbol in targetClassSymbol.GetMembers().Where(m => m.Kind == SymbolKind.Method).OfType<IMethodSymbol>())
            {
                foreach (var methodAttr in methodSymbol.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(methodAttr.AttributeClass, logMethodAttribute) &&
                        methodAttr.AttributeConstructor is not null)
                    {
                        var arg = GetLogMethodAttributeParameter(methodAttr.AttributeConstructor.Parameters, methodAttr.ConstructorArguments, "eventId");
                        if (!arg.HasValue)
                        {
                            continue;
                        }

                        var eventId = (int)arg.Value.Value!;
                        if (eventId >= max)
                        {
                            max = eventId + 1;
                        }
                    }
                }
            }

            return max;
        }
    }
}
