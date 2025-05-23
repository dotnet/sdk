// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils.Extensions;

public static class TypeExtensions
{
    // This is used when outputting the Type information for the CLI schema JSON.
    public static string ToCliTypeString(this Type type)
    {
        var typeName = type.FullName ?? string.Empty;
        if (!type.IsGenericType)
        {
            return typeName;
        }

        var genericTypeName = typeName.Substring(0, typeName.IndexOf('`'));
        var genericTypes = string.Join(", ", type.GenericTypeArguments.Select(generic => generic.ToCliTypeString()));
        return $"{genericTypeName}<{genericTypes}>";
    }
}
