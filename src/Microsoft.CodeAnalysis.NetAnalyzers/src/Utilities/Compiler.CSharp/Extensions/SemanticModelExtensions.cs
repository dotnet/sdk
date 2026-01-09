// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Analyzer.Utilities.Lightup
{
    internal static class SemanticModelExtensions
    {
        private static Func<SemanticModel, LocalDeclarationStatementSyntax, AwaitExpressionInfo>? s_GetAwaitExpressionInfoForLocalDeclaration;
        private static Func<SemanticModel, UsingStatementSyntax, AwaitExpressionInfo>? s_GetAwaitExpressionInfoForUsingStatement;

        public static AwaitExpressionInfo GetAwaitExpressionInfo(this SemanticModel semanticModel, LocalDeclarationStatementSyntax awaitUsingDeclaration)
        {
            LazyInitializer.EnsureInitialized(ref s_GetAwaitExpressionInfoForLocalDeclaration, () =>
            {
                // Try to get the method from CSharpExtensions
                var csharpExtensionsType = typeof(Microsoft.CodeAnalysis.CSharp.CSharpExtensions);
                var method = csharpExtensionsType.GetMethod(
                    "GetAwaitExpressionInfo",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(SemanticModel), typeof(LocalDeclarationStatementSyntax) },
                    null);

                if (method != null)
                {
                    return (model, syntax) => (AwaitExpressionInfo)method.Invoke(null, new object?[] { model, syntax })!;
                }

                // Fallback if method doesn't exist
                return (model, syntax) => default;
            });

            return s_GetAwaitExpressionInfoForLocalDeclaration!(semanticModel, awaitUsingDeclaration);
        }

        public static AwaitExpressionInfo GetAwaitExpressionInfo(this SemanticModel semanticModel, UsingStatementSyntax awaitUsingStatement)
        {
            LazyInitializer.EnsureInitialized(ref s_GetAwaitExpressionInfoForUsingStatement, () =>
            {
                // Try to get the method from CSharpExtensions
                var csharpExtensionsType = typeof(Microsoft.CodeAnalysis.CSharp.CSharpExtensions);
                var method = csharpExtensionsType.GetMethod(
                    "GetAwaitExpressionInfo",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(SemanticModel), typeof(UsingStatementSyntax) },
                    null);

                if (method != null)
                {
                    return (model, syntax) => (AwaitExpressionInfo)method.Invoke(null, new object?[] { model, syntax })!;
                }

                // Fallback if method doesn't exist
                return (model, syntax) => default;
            });

            return s_GetAwaitExpressionInfoForUsingStatement!(semanticModel, awaitUsingStatement);
        }
    }
}
