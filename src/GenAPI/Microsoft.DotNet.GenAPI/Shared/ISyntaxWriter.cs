// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Interface responsible for writing various outputs: C# code, XML etc.
/// </summary>
public interface ISyntaxWriter : IDisposable
{
    /// <summary>
    /// Writes namespace in C#, XML formats.
    /// </summary>
    /// <param name="namespacePath">List of nested namespaces: { parent, child }. Empty list for global namespace. </param>
    /// <returns>Returns disposable object. `obj.Dispose()` is called when current namespace is completely processed.</returns>
    IDisposable WriteNamespace(IEnumerable<string> namespacePath);

    /// <summary>
    /// Writes type definition with corresponding accessibilityModifiers, base types and constraints.
    /// </summary>
    /// <param name="accessibilityModifiers">List of accessibilityModifiers: public, private, protected, internal, etc.</param>
    /// <param name="typeName">Type name without namespaces</param>
    /// <param name="keywords">List of keywords: struct, partial, ref, class, readonly, etc.</param>
    /// <param name="baseTypeNames">List of interfaces, base classes</param>
    /// <param name="constraints">List of constraint  for generic type parameters.</param>
    /// <returns>Returns disposable object. `obj.Dispose()` is called when current type definition is completely processed.</returns>
    IDisposable WriteTypeDefinition(
        IEnumerable<SyntaxKind> accessibilityModifiers,
        IEnumerable<SyntaxKind> keywords,
        string typeName,
        IEnumerable<string> baseTypeNames,
        IEnumerable<string> constraints);

    /// <summary>
    /// Writes type delegate with corresponding accessibilityModifiers.
    /// </summary>
    /// <param name="accessibilityModifiers">List of accessibilityModifiers: public, private, protected, internal, etc.</param>
    /// <param name="typeName">Type name without namespaces</param>
    /// <param name="keywords">List of keywords: struct, partial, ref, class, readonly, etc.</param>
    /// <returns>Returns disposable object. `obj.Dispose()` is called when current type definition is completely processed.</returns>
    IDisposable WriteDelegate(
        IEnumerable<SyntaxKind> accessibilityModifiers,
        IEnumerable<SyntaxKind> keywords,
        string typeName);

    /// <summary>
    /// Writes attribute data.
    /// </summary>
    /// <param name="attribute">String representation of attribute.</param>
    void WriteAttribute(string attribute);

    /// <summary>
    /// Writes property symbol.
    /// </summary>
    /// <param name="accessibilityModifiers">List of accessibilityModifiers: public, private, protected, internal, etc.</param>
    /// <param name="definition">Includes property type and name. `bool Field`</param>
    /// <param name="hasImplementation">Determines if poperty is abstract.
    ///     If not - implementation should be ommited.</param>
    /// <param name="hasGetMethod">If `get` method specified.</param>
    /// <param name="hasSetMethod">If `set` method specified.</param>
    void WriteProperty(IEnumerable<SyntaxKind> accessibilityModifiers, string definition, bool hasImplementation, bool hasGetMethod, bool hasSetMethod);

    /// <summary>
    /// Writes event symbol.
    /// </summary>
    /// <param name="accessibilityModifiers">List of accessibilityModifiers: public, private, protected, internal, etc.</param>
    /// <param name="definition">Includes type and name.</param>
    /// <param name="hasAddMethod">If `has` method is specified.</param>
    /// <param name="hasRemoveMethod">If `remove` method is specified.</param>
    void WriteEvent(IEnumerable<SyntaxKind> accessibilityModifiers, string definition, bool hasAddMethod, bool hasRemoveMethod);

    /// <summary>
    /// Writes method symbol.
    /// </summary>
    /// <param name="accessibilityModifiers">List of accessibilityModifiers: public, private, protected, internal, etc.</param>
    /// <param name="definition">Includes return type, name and parameters with default values.</param>
    /// <param name="hasImplementation">Determines if method is abstract.
    ///     If not - implementation should be ommited.</param>
    void WriteMethod(IEnumerable<SyntaxKind> accessibilityModifiers, string definition, bool hasImplementation);

    /// <summary>
    /// Writes field symbols like enum name = value.
    /// </summary>
    /// <param name="definition">Name and constant value (`GotoStatements = 4,`).</param>
    void WriteEnumField(string definition);

    /// <summary>
    /// Writes members, constants.
    /// </summary>
    /// <param name="accessibilityModifiers">List of accessibilityModifiers: public, private, protected, internal, etc.</param>
    /// <param name="definition">Type, name and default value.</param>
    void WriteField(IEnumerable<SyntaxKind> accessibilityModifiers, string definition);
}
