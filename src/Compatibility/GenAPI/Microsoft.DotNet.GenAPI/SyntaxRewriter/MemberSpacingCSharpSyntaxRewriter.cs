// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.GenAPI.SyntaxRewriter;

/// <summary>
/// Normalizes blank lines between generated members.
/// </summary>
public sealed class MemberSpacingCSharpSyntaxRewriter : CSharpSyntaxRewriter
{
    private static readonly SyntaxTrivia s_endOfLine = SyntaxFactory.EndOfLine(Environment.NewLine);

    private MemberSpacingCSharpSyntaxRewriter()
    {
    }

    public static MemberSpacingCSharpSyntaxRewriter Singleton { get; } = new();

    /// <inheritdoc />
    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        CompilationUnitSyntax? visited = (CompilationUnitSyntax?)base.VisitCompilationUnit(node);
        return visited?.WithMembers(AddBlankLineBetweenMembers(visited.Members));
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        NamespaceDeclarationSyntax? visited = (NamespaceDeclarationSyntax?)base.VisitNamespaceDeclaration(node);
        return visited?.WithMembers(AddBlankLineBetweenMembers(visited.Members));
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        FileScopedNamespaceDeclarationSyntax? visited = (FileScopedNamespaceDeclarationSyntax?)base.VisitFileScopedNamespaceDeclaration(node);
        return visited?.WithMembers(AddBlankLineBetweenMembers(visited.Members));
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        ClassDeclarationSyntax? visited = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
        return visited?.WithMembers(AddLineBetweenTypeMembers(visited.Members));
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        StructDeclarationSyntax? visited = (StructDeclarationSyntax?)base.VisitStructDeclaration(node);
        return visited?.WithMembers(AddLineBetweenTypeMembers(visited.Members));
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        InterfaceDeclarationSyntax? visited = (InterfaceDeclarationSyntax?)base.VisitInterfaceDeclaration(node);
        return visited?.WithMembers(AddLineBetweenTypeMembers(visited.Members));
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        RecordDeclarationSyntax? visited = (RecordDeclarationSyntax?)base.VisitRecordDeclaration(node);
        return visited?.WithMembers(AddLineBetweenTypeMembers(visited.Members));
    }

    private static SyntaxList<MemberDeclarationSyntax> AddBlankLineBetweenMembers(SyntaxList<MemberDeclarationSyntax> members)
    {
        MemberDeclarationSyntax[] updatedMembers = members.ToArray();

        for (int i = 1; i < members.Count; i++)
        {
            updatedMembers[i - 1] = RemoveTrailingWhitespace(updatedMembers[i - 1]);
            updatedMembers[i] = updatedMembers[i].WithLeadingTrivia(GetAdjustedLeadingTrivia(updatedMembers[i], includeBlankLine: true));
        }

        return SyntaxFactory.List(updatedMembers);
    }

    private static SyntaxList<MemberDeclarationSyntax> AddLineBetweenTypeMembers(SyntaxList<MemberDeclarationSyntax> members)
    {
        MemberDeclarationSyntax[] updatedMembers = members.ToArray();

        for (int i = 1; i < members.Count; i++)
        {
            updatedMembers[i - 1] = RemoveTrailingWhitespace(updatedMembers[i - 1]);
            bool includeBlankLine = updatedMembers[i - 1] is BaseTypeDeclarationSyntax && updatedMembers[i] is BaseTypeDeclarationSyntax;
            updatedMembers[i] = updatedMembers[i].WithLeadingTrivia(GetAdjustedLeadingTrivia(updatedMembers[i], includeBlankLine));
        }

        return SyntaxFactory.List(updatedMembers);
    }

    private static MemberDeclarationSyntax RemoveTrailingWhitespace(MemberDeclarationSyntax member)
    {
        SyntaxTriviaList trailingTrivia = member.GetTrailingTrivia();
        return trailingTrivia.Any(static trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            ? member
            : member.WithTrailingTrivia();
    }

    private static SyntaxTriviaList GetAdjustedLeadingTrivia(MemberDeclarationSyntax member, bool includeBlankLine)
    {
        SyntaxTriviaList leadingTrivia = member.GetLeadingTrivia();

        if (leadingTrivia.Any(static trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
        {
            return leadingTrivia;
        }

        List<SyntaxTrivia> adjustedTrivia = [s_endOfLine];

        if (includeBlankLine)
        {
            adjustedTrivia.Add(s_endOfLine);
        }

        string indentation = GetIndentation(leadingTrivia);
        if (!string.IsNullOrEmpty(indentation))
        {
            adjustedTrivia.Add(SyntaxFactory.Whitespace(indentation));
        }

        return SyntaxFactory.TriviaList(adjustedTrivia);
    }

    private static string GetIndentation(SyntaxTriviaList leadingTrivia)
    {
        for (int i = leadingTrivia.Count - 1; i >= 0; i--)
        {
            SyntaxTrivia trivia = leadingTrivia[i];
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                return trivia.ToFullString();
            }

            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return string.Empty;
            }
        }

        return string.Empty;
    }
}
