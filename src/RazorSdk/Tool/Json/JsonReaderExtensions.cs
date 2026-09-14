// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal static class JsonReaderExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckToken(this JsonReader reader, JsonToken expectedToken)
    {
        if (reader.TokenType != expectedToken)
        {
            ThrowUnexpectedTokenException(expectedToken, reader.TokenType);
        }

        [DoesNotReturn]
        static void ThrowUnexpectedTokenException(JsonToken expectedToken, JsonToken actualToken)
        {
            throw new InvalidOperationException(
                Strings.FormatExpected_JSON_token_0_but_it_was_1(expectedToken, actualToken));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadToken(this JsonReader reader, JsonToken expectedToken)
    {
        reader.CheckToken(expectedToken);
        reader.Read();
    }
}
