// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal static class JsonReaderExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckToken(ref Utf8JsonReader reader, JsonTokenType expectedToken)
    {
        if (reader.TokenType != expectedToken)
        {
            ThrowUnexpectedTokenException(expectedToken, reader.TokenType);
        }

        [DoesNotReturn]
        static void ThrowUnexpectedTokenException(JsonTokenType expectedToken, JsonTokenType actualToken)
        {
            throw new InvalidOperationException(
                Strings.FormatExpected_JSON_token_0_but_it_was_1(expectedToken, actualToken));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadToken(ref Utf8JsonReader reader, JsonTokenType expectedToken)
    {
        CheckToken(ref reader, expectedToken);
        reader.Read();
    }
}
