// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

public class FilterOutDelegateMembers : IncludeAllFilter
{
    /// <inheritdoc />
    public override bool Includes(ISymbol member) => member.ContainingType.TypeKind != TypeKind.Delegate;
}
