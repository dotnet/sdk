// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.NetFramework.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.NetFramework.CSharp.Analyzers.Helpers
{
    public sealed class CSharpSyntaxNodeHelper : SyntaxNodeHelper
    {
        public static CSharpSyntaxNodeHelper Default { get; } = new CSharpSyntaxNodeHelper();

        private CSharpSyntaxNodeHelper()
        { }

        public override ITypeSymbol? GetClassDeclarationTypeSymbol(SyntaxNode? node, SemanticModel semanticModel)
        {
            if (node == null)
            {
                return null;
            }

            SyntaxKind kind = node.Kind();
            if (kind == SyntaxKind.ClassDeclaration)
            {
                return semanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)node);
            }

            return null;
        }

        public override SyntaxNode? GetAssignmentLeftNode(SyntaxNode? node)
        {
            if (node == null)
            {
                return null;
            }

            SyntaxKind kind = node.Kind();
            if (kind == SyntaxKind.SimpleAssignmentExpression)
            {
                return ((AssignmentExpressionSyntax)node).Left;
            }

            if (kind == SyntaxKind.VariableDeclarator)
            {
                return (VariableDeclaratorSyntax)node;
            }

            return null;
        }

        public override SyntaxNode? GetAssignmentRightNode(SyntaxNode? node)
        {
            if (node == null)
            {
                return null;
            }

            SyntaxKind kind = node.Kind();
            if (kind == SyntaxKind.SimpleAssignmentExpression)
            {
                return ((AssignmentExpressionSyntax)node).Right;
            }

            if (kind == SyntaxKind.VariableDeclarator)
            {
                EqualsValueClauseSyntax initializer = ((VariableDeclaratorSyntax)node).Initializer;
                if (initializer != null)
                {
                    return initializer.Value;
                }
            }

            return null;
        }

        public override SyntaxNode? GetMemberAccessExpressionNode(SyntaxNode? node)
        {
            if (node == null)
            {
                return null;
            }

            SyntaxKind kind = node.Kind();
            if (kind == SyntaxKind.SimpleMemberAccessExpression)
            {
                return ((MemberAccessExpressionSyntax)node).Expression;
            }

            return null;
        }

        public override SyntaxNode? GetMemberAccessNameNode(SyntaxNode? node)
        {
            if (node == null)
            {
                return null;
            }

            SyntaxKind kind = node.Kind();
            if (kind == SyntaxKind.SimpleMemberAccessExpression)
            {
                return ((MemberAccessExpressionSyntax)node).Name;
            }

            return null;
        }

        public override SyntaxNode? GetInvocationExpressionNode(SyntaxNode? node)
        {
            if (node == null)
            {
                return null;
            }

            SyntaxKind kind = node.Kind();
            if (kind != SyntaxKind.InvocationExpression)
            {
                return null;
            }

            return ((InvocationExpressionSyntax)node).Expression;
        }

        public override SyntaxNode? GetCallTargetNode(SyntaxNode? node)
        {
            if (node != null)
            {
                SyntaxKind kind = node.Kind();
                if (kind == SyntaxKind.InvocationExpression)
                {
                    ExpressionSyntax callExpr = ((InvocationExpressionSyntax)node).Expression;
                    return GetMemberAccessNameNode(callExpr) ?? callExpr;
                }
                else if (kind == SyntaxKind.ObjectCreationExpression)
                {
                    return ((ObjectCreationExpressionSyntax)node).Type;
                }
            }

            return null;
        }

        public override SyntaxNode? GetDefaultValueForAnOptionalParameter(SyntaxNode? declNode, int paramIndex)
        {
            if (declNode is BaseMethodDeclarationSyntax methodDecl)
            {
                ParameterListSyntax paramList = methodDecl.ParameterList;
                if (paramIndex < paramList.Parameters.Count)
                {
                    EqualsValueClauseSyntax equalsValueNode = paramList.Parameters[paramIndex].Default;
                    if (equalsValueNode != null)
                    {
                        return equalsValueNode.Value;
                    }
                }
            }
            return null;
        }

        protected override IEnumerable<SyntaxNode> GetCallArgumentExpressionNodes(SyntaxNode? node, CallKinds callKind)
        {
            if (node != null)
            {
                ArgumentListSyntax? argList = null;
                SyntaxKind kind = node.Kind();
                if ((kind == SyntaxKind.InvocationExpression) && ((callKind & CallKinds.Invocation) != 0))
                {
                    var invocationNode = (InvocationExpressionSyntax)node;
                    argList = invocationNode.ArgumentList;
                }
                else if ((kind == SyntaxKind.ObjectCreationExpression) && ((callKind & CallKinds.ObjectCreation) != 0))
                {
                    var invocationNode = (ObjectCreationExpressionSyntax)node;
                    argList = invocationNode.ArgumentList;
                }
                if (argList != null)
                {
                    return argList.Arguments.Select(arg => arg.Expression);
                }
            }

            return Enumerable.Empty<SyntaxNode>();
        }

        public override IEnumerable<SyntaxNode> GetObjectInitializerExpressionNodes(SyntaxNode? node)
        {
            IEnumerable<SyntaxNode> empty = Enumerable.Empty<SyntaxNode>();
            if (node == null)
            {
                return empty;
            }

            SyntaxKind kind = node.Kind();
            if (kind != SyntaxKind.ObjectCreationExpression)
            {
                return empty;
            }

            var objectCreationNode = (ObjectCreationExpressionSyntax)node;
            if (objectCreationNode.Initializer == null)
            {
                return empty;
            }

            return objectCreationNode.Initializer.Expressions;
        }

        public override bool IsMethodInvocationNode(SyntaxNode? node)
        {
            if (node == null)
            {
                return false;
            }
            SyntaxKind kind = node.Kind();
            return kind is SyntaxKind.InvocationExpression or SyntaxKind.ObjectCreationExpression;
        }

        public override IMethodSymbol? GetCalleeMethodSymbol(SyntaxNode? node, SemanticModel semanticModel)
        {
            ISymbol? symbol = GetReferencedSymbol(node, semanticModel);

            if (symbol != null && symbol.Kind == SymbolKind.Method)
            {
                return (IMethodSymbol)symbol;
            }

            return null;
        }

        public override IMethodSymbol? GetCallerMethodSymbol(SyntaxNode? node, SemanticModel semanticModel)
        {
            if (node == null)
            {
                return null;
            }

            MethodDeclarationSyntax declaration = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (declaration != null)
            {
                return semanticModel.GetDeclaredSymbol(declaration);
            }

            ConstructorDeclarationSyntax contructor = node.AncestorsAndSelf().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
            if (contructor != null)
            {
                return semanticModel.GetDeclaredSymbol(contructor);
            }

            return null;
        }

        public override ITypeSymbol? GetEnclosingTypeSymbol(SyntaxNode? node, SemanticModel semanticModel)
        {
            if (node == null)
            {
                return null;
            }

            ClassDeclarationSyntax declaration = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            if (declaration == null)
            {
                return null;
            }

            return semanticModel.GetDeclaredSymbol(declaration);
        }

        public override IEnumerable<SyntaxNode> GetDescendantAssignmentExpressionNodes(SyntaxNode? node)
        {
            IEnumerable<SyntaxNode> empty = Enumerable.Empty<SyntaxNode>();
            if (node == null)
            {
                return empty;
            }

            return node.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>();
        }

        public override IEnumerable<SyntaxNode> GetDescendantMemberAccessExpressionNodes(SyntaxNode? node)
        {
            IEnumerable<SyntaxNode> empty = Enumerable.Empty<SyntaxNode>();
            if (node == null)
            {
                return empty;
            }

            return node.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>();
        }

        public override bool IsObjectCreationExpressionUnderFieldDeclaration([NotNullWhen(returnValue: true)] SyntaxNode? node)
        {
            return node != null &&
                   node.Kind() == SyntaxKind.ObjectCreationExpression &&
                   node.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().FirstOrDefault() != null;
        }

        public override SyntaxNode? GetVariableDeclaratorOfAFieldDeclarationNode(SyntaxNode? objectCreationExpression)
        {
            if (IsObjectCreationExpressionUnderFieldDeclaration(objectCreationExpression))
            {
                return objectCreationExpression.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
            }
            else
            {
                return null;
            }
        }
    }
}
