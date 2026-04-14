// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils.Extensions;

public static class TypeExtensions
{
    ///<summary>
    /// Converts a Type (potentially containing generic parameters) from CLI representation (e.g. <c>System.Collections.Generic.List`1[[System.Int32, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]</c>)
    /// to a more readable string representation (e.g. <c>System.Collections.Generic.List&lt;System.Int32&gt;</c>).
    /// </summary>
    ///<remarks>
    /// This is used when outputting the Type information for the CLI schema JSON.
    ///</remarks>
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
