﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI
{
    internal static class AttributeDataExtensions
    {
        private static readonly HashSet<string> s_reservedTypes = new(StringComparer.Ordinal)
        {
            "DynamicAttribute",
            "IsReadOnlyAttribute",
            "IsUnmanagedAttribute",
            "IsByRefLikeAttribute",
            "TupleElementNamesAttribute",
            "NullableAttribute",
            "NullableContextAttribute",
            "NullablePublicOnlyAttribute",
            "NativeIntegerAttribute",
            "ExtensionAttribute",
            "RequiredMemberAttribute",
            "ScopedRefAttribute",
            "RefSafetyRulesAttribute"
        };

        public static bool IsObsoleteWithUsageTreatedAsCompilationError(this AttributeData attribute)
        {
            INamedTypeSymbol? attributeClass = attribute.AttributeClass;
            if (attributeClass is null)
                return false;

            if (attributeClass.ToDisplayString() != typeof(ObsoleteAttribute).FullName)
                return false;

            if (attribute.ConstructorArguments.Length != 2)
                return false;

            TypedConstant messageArgument = attribute.ConstructorArguments.ElementAt(0);
            TypedConstant errorArgument = attribute.ConstructorArguments.ElementAt(1);
            if (messageArgument.Value == null || errorArgument.Value == null)
                return false;

            if (messageArgument.Value is not string || errorArgument.Value is not bool)
                return false;

            return (bool)errorArgument.Value;
        }

        public static bool IsDefaultMemberAttribute(this AttributeData attribute) =>
             attribute.AttributeClass?.ToDisplayString() == typeof(DefaultMemberAttribute).FullName;

        // Determines if an attribute is a reserved attribute class -- these are attributes that may
        // only be applied by the compiler and are an error to be applied by the user in source.
        // See https://github.com/dotnet/roslyn/blob/b8f6dd56f1a0860fcd822bc1e70bec56dc1e97ea/src/Compilers/CSharp/Portable/Symbols/Symbol.cs#L1421
        public static bool IsReserved(this AttributeData attribute)
        {
            INamedTypeSymbol? attributeClass = attribute.AttributeClass;

            return attributeClass != null && s_reservedTypes.Contains(attributeClass.Name) &&
                attributeClass.ContainingNamespace.ToDisplayString().Equals("System.Runtime.CompilerServices", StringComparison.Ordinal);
        }
    }
}
