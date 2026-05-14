// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.ApiDiff.SyntaxRewriter;

/// <summary>
/// Replaces the bodies of nodes that have one (methods, properties, events, etc.) with a semicolon.
/// </summary>
internal class RemoveBodyCSharpSyntaxRewriter : CSharpSyntaxRewriter
{
    public static readonly RemoveBodyCSharpSyntaxRewriter Singleton = new();

    private readonly SyntaxToken _semiColonToken = SyntaxFactory.Token(SyntaxKind.SemicolonToken);
    private readonly SyntaxToken _noneToken = SyntaxFactory.Token(SyntaxKind.None);
    private readonly SyntaxTriviaList _endLineTrivia = SyntaxFactory.TriviaList((Environment.NewLine == "\r\n") ? SyntaxFactory.CarriageReturnLineFeed : SyntaxFactory.LineFeed);

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var result = node
                .WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        return base.VisitConstructorDeclaration(result);
    }

    // These bad boys look like: 'public static explicit operator int(MyClass value)'
    public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
    {
        var result = node
                .WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(_semiColonToken)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        return base.VisitConversionOperatorDeclaration(result);
    }

    public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node)
    {
        var result = node
                .WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(_semiColonToken)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        return base.VisitDestructorDeclaration(result);
    }

    public override SyntaxNode? VisitEventDeclaration(EventDeclarationSyntax node)
    {
        var result = node
            .WithIdentifier(node.Identifier.WithoutTrivia())
            .WithAccessorList(GetEmptiedAccessors(node.AccessorList));
        return base.VisitEventDeclaration(result);
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var result = node
                .WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(_semiColonToken)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        return base.VisitMethodDeclaration(result);
    }

    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        if (node.OperatorToken.IsKind(SyntaxKind.GreaterThanToken))
        {
            // Takes care of the missing space before the greater than
            node = node.WithOperatorToken(SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), node.OperatorToken.Kind(), SyntaxTriviaList.Empty));
        }
        var result = node
                .WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(_semiColonToken)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        return base.VisitOperatorDeclaration(result);
    }

    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var result = node.WithAccessorList(GetEmptiedAccessors(node.AccessorList));
        return base.VisitPropertyDeclaration(result);
    }

    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        if (node.Members.Any())
        {
            // Fix the spaces and lack of newline
            var replacedOpenBrace = SyntaxFactory.Token(_endLineTrivia.AddRange(node.GetLeadingTrivia()), SyntaxKind.OpenBraceToken, node.OpenBraceToken.TrailingTrivia);
            node = node.WithOpenBraceToken(replacedOpenBrace);
        }
        else
        {
            // Replace braces with semicolon
            node = node.WithOpenBraceToken(_noneToken)
                       .WithCloseBraceToken(_noneToken)
                       .WithSemicolonToken(_semiColonToken);
        }

        if (node.ParameterList != null && node.ParameterList.Parameters.Any())
        {
            // Fix the extra space
            node = node.WithParameterList(node.ParameterList.WithoutTrailingTrivia());
        }
        else
        {
            // Remove the parentheses if there are no arguments
            node = node.WithParameterList(null);
        }

        return base.VisitRecordDeclaration(node);
    }

    private AccessorListSyntax? GetEmptiedAccessors(AccessorListSyntax? accessorList)
    {
        if (accessorList == null)
        {
            return null;
        }

        List<AccessorDeclarationSyntax> newAccessors = new();


        for (int i = 0; i < accessorList.Accessors.Count; i++)
        {
            AccessorDeclarationSyntax accessorDeclaration = accessorList.Accessors[i]
                               .WithBody(null) // remove the default empty body wrapped by brackets
                               .WithoutTrivia() // Important
                               .WithLeadingTrivia(SyntaxFactory.Space) // Add a space before the accessor
                               .WithSemicolonToken(_semiColonToken); // Append a semicolon at the end

            if (i == accessorList.Accessors.Count - 1) // Second to last
            {
                // Add a space after the semicolon only on the last accessor
                accessorDeclaration = accessorDeclaration.WithTrailingTrivia(SyntaxFactory.Space);
            }

            newAccessors.Add(accessorDeclaration);
        }

        return SyntaxFactory.AccessorList(SyntaxFactory.List(newAccessors)).WithLeadingTrivia(SyntaxFactory.Space);
    }

}
