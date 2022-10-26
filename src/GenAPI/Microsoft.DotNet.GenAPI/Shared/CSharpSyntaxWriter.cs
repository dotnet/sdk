// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Writes C# source code into IO.File or Console.
/// </summary>
public class CSharpSyntaxWriter : ISyntaxWriter
{
    private readonly TextWriter _textWriter;
    private readonly string? _exceptionMessage;
    private readonly int _indentationSize;
    private readonly char _indentationChar;

    private int Indentation { get; set; }

    public CSharpSyntaxWriter(
        TextWriter streamWriter,
        string fileHeader,
        string? exceptionMessage,
        int indentationSize,
        char indentationChar)
    {
        _textWriter = streamWriter;
        _exceptionMessage = exceptionMessage;
        _indentationSize = indentationSize;
        _indentationChar = indentationChar;

        _textWriter.WriteLine(fileHeader);
    }

    /// <inheritdoc />
    public IDisposable WriteNamespace(IEnumerable<string> namespacePath)
    {
        WriteIndentation();
        WriteKeyword(SyntaxKind.NamespaceKeyword);

        bool root = true;
        foreach (var ns in namespacePath)
        {
            if (!root)
            {
                WriteKeyword(SyntaxKind.DotToken, writeSpace: false);
            }
            else
            {
                root = false;
            }

            _textWriter.Write(ns);
        }

        OpenBrace();

        return new Block(() =>
        {
            CloseBrace();
        });
    }

    /// <inheritdoc />
    public IDisposable WriteTypeDefinition(
        IEnumerable<SyntaxKind> accessibilityModifiers,
        IEnumerable<SyntaxKind> keywords,
        string typeName,
        IEnumerable<string> baseTypeNames,
        IEnumerable<string> constraints)
    {
        WriteIndentation();

        foreach (var modifier in accessibilityModifiers)
        {
            WriteKeyword(modifier);
        }

        foreach (var keyword in keywords)
        {
            WriteKeyword(keyword);
        }

        _textWriter.Write(typeName);

        bool first = true;

        foreach (var baseSymbol in baseTypeNames)
        {
            if (first)
            {
                WriteSpace();
                WriteKeyword(SyntaxKind.ColonToken);
            }
            else
            {
                WriteKeyword(SyntaxKind.CommaToken);
            }

            first = false;

            _textWriter.Write(baseSymbol);
        }

        foreach (var constraint in constraints)
        {
            WriteSpace();
            _textWriter.Write(constraint);
        }

        OpenBrace();

        return new Block(() =>
        {
            CloseBrace();
        });
    }

    public IDisposable WriteDelegate(
        IEnumerable<SyntaxKind> accessibilityModifiers,
        IEnumerable<SyntaxKind> keywords,
        string typeName)
    {
        WriteIndentation();

        foreach (var modifier in accessibilityModifiers)
        {
            WriteKeyword(modifier);
        }

        foreach (var keyword in keywords)
        {
            WriteKeyword(keyword);
        }

        _textWriter.Write(typeName);

        WriteKeyword(SyntaxKind.SemicolonToken, writeSpace: false);
        WriteNewLine();

        return new Block(() =>
        {
        });
    }

    /// <inheritdoc />
    public void WriteAttribute(string attribute)
    {
        WriteIndentation();

        WriteKeyword(SyntaxKind.OpenBracketToken, writeSpace: false);
        _textWriter.Write(attribute);
        WriteKeyword(SyntaxKind.CloseBracketToken, writeSpace: false);
        WriteNewLine();
    }

    /// <inheritdoc />
    public void WriteProperty(
        IEnumerable<SyntaxKind> accessibilityModifiers,
        string definition,
        bool hasImplementation,
        bool hasGetMethod,
        bool hasSetMethod)
    {
        WriteIndentation();

        foreach (var modifier in accessibilityModifiers)
        {
            WriteKeyword(modifier);
        }

        _textWriter.Write(definition);

        if (hasGetMethod || hasSetMethod)
        {
            var _writeAccessorMethod = (SyntaxKind method) =>
            {
                WriteKeyword(method, writeSpace: false);
                if (hasImplementation)
                {
                    WriteSpace();
                    WriteImplementation();
                    WriteSpace();
                }
                else
                {
                    WriteKeyword(SyntaxKind.SemicolonToken);
                }
            };

            WriteSpace();
            WriteKeyword(SyntaxKind.OpenBraceToken);

            if (hasGetMethod)
            {
                _writeAccessorMethod(SyntaxKind.GetKeyword);
            }

            if (hasSetMethod)
            {
                _writeAccessorMethod(SyntaxKind.SetKeyword);
            }

            WriteKeyword(SyntaxKind.CloseBraceToken, writeSpace: false);
        }
        else
        {
            WriteKeyword(SyntaxKind.SemicolonToken, writeSpace: false);
        }

        WriteNewLine();
    }

    /// <inheritdoc />
    public void WriteEvent(
        IEnumerable<SyntaxKind> accessibilityModifiers,
        string definition,
        bool hasAddMethod,
        bool hasRemoveMethod)
    {
        WriteIndentation();

        foreach (var modifier in accessibilityModifiers)
        {
            WriteKeyword(modifier);
        }

        _textWriter.Write(definition);

        if (hasAddMethod || hasRemoveMethod)
        {
            var _writeAccessorMethod = (SyntaxKind method) =>
            {
                WriteKeyword(method);
                WriteImplementation();
                WriteSpace();
            };

            WriteSpace();
            WriteKeyword(SyntaxKind.OpenBraceToken);

            if (hasAddMethod)
            {
                _writeAccessorMethod(SyntaxKind.AddKeyword);
            }

            if (hasRemoveMethod)
            {
                _writeAccessorMethod(SyntaxKind.RemoveKeyword);
            }

            WriteKeyword(SyntaxKind.CloseBraceToken, writeSpace: false);
        }
        else
        {
            WriteKeyword(SyntaxKind.SemicolonToken, writeSpace: false);
        }

        WriteNewLine();
    }

    /// <inheritdoc />
    public void WriteMethod(
        IEnumerable<SyntaxKind> accessibilityModifiers,
        string definition,
        bool hasImplementation)
    {
        WriteIndentation();

        foreach (var modifier in accessibilityModifiers)
        {
            WriteKeyword(modifier);
        }

        _textWriter.Write(definition);

        if (hasImplementation)
        {
            WriteSpace();
            WriteImplementation();
        }
        else
        {
            WriteKeyword(SyntaxKind.SemicolonToken, writeSpace: false);
        }

        WriteNewLine();
    }

    /// <inheritdoc />
    public void WriteEnumField(string definition)
    {
        WriteIndentation();
        _textWriter.Write(definition);
        WriteKeyword(SyntaxKind.CommaToken, writeSpace: false);
        WriteNewLine();
    }

    public void WriteField(IEnumerable<SyntaxKind> accessibilityModifiers, string definition)
    {
        WriteIndentation();

        foreach (var modifier in accessibilityModifiers)
        {
            WriteKeyword(modifier);
        }

        _textWriter.Write(definition);

        WriteKeyword(SyntaxKind.SemicolonToken, writeSpace: false);
        WriteNewLine();
    }

    public void Dispose() => _textWriter.Dispose();

    #region Private methods

    private void WriteSpace()
    {
        _textWriter.Write(' ');
    }

    private void WriteNewLine()
    {
        _textWriter.WriteLine();
    }

    private void WriteIndentation()
    {
        _textWriter.Write(new string(_indentationChar, Indentation * _indentationSize));
    }

    private void OpenBrace()
    {
        WriteNewLine();

        WriteIndentation();
        ++Indentation;

        WriteKeyword(SyntaxKind.OpenBraceToken, writeSpace: false);
        WriteNewLine();
    }

    private void CloseBrace()
    {
        --Indentation;
        WriteIndentation();

        WriteKeyword(SyntaxKind.CloseBraceToken, writeSpace: false);
        WriteNewLine();
    }

    private void WriteKeyword(SyntaxKind keyword, bool writeSpace = true)
    {
        _textWriter.Write(SyntaxFacts.GetText(keyword));
        if (writeSpace)
        {
            WriteSpace();
        }
    }

    private void WriteImplementation()
    {
        WriteKeyword(SyntaxKind.OpenBraceToken);
        if (_exceptionMessage is string exceptionMessage)
        {
            WriteKeyword(SyntaxKind.ThrowKeyword);
            _textWriter.Write("PlatformNotSupportedException");
            _textWriter.Write(SyntaxFacts.GetText(SyntaxKind.OpenParenToken));
            _textWriter.Write(SyntaxFacts.GetText(SyntaxKind.DoubleQuoteToken));
            _textWriter.Write(exceptionMessage);
            _textWriter.Write(SyntaxFacts.GetText(SyntaxKind.DoubleQuoteToken));
            _textWriter.Write(SyntaxFacts.GetText(SyntaxKind.CloseParenToken));
        }
        else
        {
            WriteKeyword(SyntaxKind.ThrowKeyword);
            WriteKeyword(SyntaxKind.NullKeyword, writeSpace: false);
        }
        WriteKeyword(SyntaxKind.SemicolonToken);
        WriteKeyword(SyntaxKind.CloseBraceToken, writeSpace: false);
    }

    private class Block : IDisposable
    {
        private readonly Action _endBlock;

        public Block(Action endBlock) => _endBlock = endBlock;

        public void Dispose() => _endBlock();
    }

    #endregion
}
