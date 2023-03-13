﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI
{
    internal static class AttributeDataExtensions
    {
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
    }
}
