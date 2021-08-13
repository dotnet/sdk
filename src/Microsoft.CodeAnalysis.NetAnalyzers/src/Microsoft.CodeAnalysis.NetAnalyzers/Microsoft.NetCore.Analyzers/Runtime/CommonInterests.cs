// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    internal static class CommonInterests
    {
        internal static readonly IEnumerable<SyncBlockingMethod> SyncBlockingMethods = new[]
        {
            new SyncBlockingMethod(new QualifiedMember(new QualifiedType(Namespaces.SystemThreadingTasks, nameof(Task)), nameof(Task.Wait)), null),
            new SyncBlockingMethod(new QualifiedMember(new QualifiedType(Namespaces.SystemThreadingTasks, nameof(Task)), nameof(Task.WaitAll)), null),
            new SyncBlockingMethod(new QualifiedMember(new QualifiedType(Namespaces.SystemThreadingTasks, nameof(Task)), nameof(Task.WaitAny)), null),
            new SyncBlockingMethod(new QualifiedMember(new QualifiedType(Namespaces.SystemRuntimeCompilerServices, nameof(TaskAwaiter)), nameof(TaskAwaiter.GetResult)), null),
            new SyncBlockingMethod(new QualifiedMember(new QualifiedType(Namespaces.SystemRuntimeCompilerServices, nameof(ValueTaskAwaiter)), nameof(ValueTaskAwaiter.GetResult)), null),
        };

        internal static readonly IReadOnlyList<SyncBlockingMethod> SyncBlockingProperties = new[]
        {
            new SyncBlockingMethod(new QualifiedMember(new QualifiedType(Namespaces.SystemThreadingTasks, nameof(Task)), nameof(Task<int>.Result)), null),
            new SyncBlockingMethod(new QualifiedMember(new QualifiedType(Namespaces.SystemThreadingTasks, nameof(ValueTask)), nameof(ValueTask<int>.Result)), null),
        };

        [DebuggerDisplay("{" + nameof(Method) + "} -> {" + nameof(AsyncAlternativeMethodName) + "}")]
#pragma warning disable CA1815 // Override equals and operator equals on value types
        internal readonly struct SyncBlockingMethod
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public SyncBlockingMethod(QualifiedMember method, string? asyncAlternativeMethodName = null, IReadOnlyList<string>? extensionMethodNamespace = null)
            {
                this.Method = method;
                this.AsyncAlternativeMethodName = asyncAlternativeMethodName;
                this.ExtensionMethodNamespace = extensionMethodNamespace;
            }

            public QualifiedMember Method { get; }

            public string? AsyncAlternativeMethodName { get; }

            public IReadOnlyList<string>? ExtensionMethodNamespace { get; }
        }

#pragma warning disable CA1815 // Override equals and operator equals on value types
        internal readonly struct QualifiedMember
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public QualifiedMember(QualifiedType containingType, string methodName)
            {
                this.ContainingType = containingType;
                this.Name = methodName;
            }

            public QualifiedType ContainingType { get; }

            public string Name { get; }

            public bool IsMatch(ISymbol symbol)
            {
                return symbol?.Name == this.Name
                    && this.ContainingType.IsMatch(symbol.ContainingType);
            }

            public override string ToString() => this.ContainingType.ToString() + "." + this.Name;
        }

        /// <summary>
        /// Tests whether a symbol belongs to a given namespace.
        /// </summary>
        /// <param name="symbol">The symbol whose namespace membership is being tested.</param>
        /// <param name="namespaces">A sequence of namespaces from global to most precise. For example: [System, Threading, Tasks].</param>
        /// <returns><c>true</c> if the symbol belongs to the given namespace; otherwise <c>false</c>.</returns>
        internal static bool BelongsToNamespace(this ISymbol symbol, IReadOnlyList<string> namespaces)
        {
            if (namespaces is null)
            {
                throw new ArgumentNullException(nameof(namespaces));
            }

            if (symbol is null)
            {
                return false;
            }

            INamespaceSymbol currentNamespace = symbol.ContainingNamespace;
            for (int i = namespaces.Count - 1; i >= 0; i--)
            {
                if (currentNamespace?.Name != namespaces[i])
                {
                    return false;
                }

                currentNamespace = currentNamespace.ContainingNamespace;
            }

            return currentNamespace?.IsGlobalNamespace ?? false;
        }

#pragma warning disable CA1815 // Override equals and operator equals on value types
        internal readonly struct QualifiedType
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public QualifiedType(IReadOnlyList<string> containingTypeNamespace, string typeName)
            {
                this.Namespace = containingTypeNamespace;
                this.Name = typeName;
            }

            public IReadOnlyList<string> Namespace { get; }

            public string Name { get; }

            public bool IsMatch(ISymbol symbol)
            {
                return symbol?.Name == this.Name
                    && symbol.BelongsToNamespace(this.Namespace);
            }

            public override string ToString() => string.Join(".", this.Namespace.Concat(new[] { this.Name }));
        }

        internal static class AsyncMethodBuilderAttribute
        {
            internal const string TypeName = nameof(System.Runtime.CompilerServices.AsyncMethodBuilderAttribute);
            internal static readonly IReadOnlyList<string> Namespace = Namespaces.SystemRuntimeCompilerServices;
        }

        internal static bool HasAsyncCompatibleReturnType([NotNullWhen(true)] this IMethodSymbol? methodSymbol) => IsAsyncCompatibleReturnType(methodSymbol?.ReturnType);

        /// <summary>
        /// Determines whether a type could be used with the async modifier as a method return type.
        /// </summary>
        /// <param name="typeSymbol">The type returned from a method.</param>
        /// <returns><c>true</c> if the type can be returned from an async method.</returns>
        /// <remarks>
        /// This is not the same thing as being an *awaitable* type, which is a much lower bar. Any type can be made awaitable by offering a GetAwaiter method
        /// that follows the proper pattern. But being an async-compatible type in this sense is a type that can be returned from a method carrying the async keyword modifier,
        /// in that the type is either the special Task type, or offers an async method builder of its own.
        /// </remarks>
        internal static bool IsAsyncCompatibleReturnType([NotNullWhen(true)] this ITypeSymbol? typeSymbol)
        {
            if (typeSymbol is null)
            {
                return false;
            }

            // ValueTask and ValueTask<T> have the AsyncMethodBuilderAttribute
            return (typeSymbol.Name == nameof(Task) && typeSymbol.BelongsToNamespace(Namespaces.SystemThreadingTasks))
                || IsIAsyncEnumerable(typeSymbol) || typeSymbol.AllInterfaces.Any(IsIAsyncEnumerable)
                || typeSymbol.GetAttributes().Any(ad => ad.AttributeClass?.Name == AsyncMethodBuilderAttribute.TypeName && ad.AttributeClass.BelongsToNamespace(AsyncMethodBuilderAttribute.Namespace));

            static bool IsIAsyncEnumerable(ITypeSymbol symbol)
                => symbol.Name == "IAsyncEnumerable"
                && symbol.BelongsToNamespace(Namespaces.SystemCollectionsGeneric);
        }

        internal static bool IsObsolete(this ISymbol symbol)
        {
            return symbol.GetAttributes().Any(a => a.AttributeClass.Name == nameof(ObsoleteAttribute) && a.AttributeClass.BelongsToNamespace(Namespaces.System));
        }
    }
}
