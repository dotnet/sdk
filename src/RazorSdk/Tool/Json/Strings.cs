// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal static class Strings
{
    public const string Could_not_read_value_JSON_token_was_0 = "Could not read value - JSON token was '{0}'.";
    public const string Encountered_end_of_stream_before_end_of_object = "Encountered end of stream before end of object.";
    public const string Encountered_unexpected_JSON_property_0 = "Encountered unexpected JSON property '{0}'.";
    public const string Encountered_unexpected_JSON_token_0 = "Encountered unexpected JSON token '{0}'.";
    public const string Expected_JSON_property_0_but_it_was_1 = "Expected JSON property '{0}', but it was '{1}'.";
    public const string Expected_JSON_token_0_but_it_was_1 = "Expected JSON token '{0}', but it was '{1}'.";

    public const string Expected_0_to_be_non_null = "Expected '{0}' to be non-null.";
    public const string Expected_condition_to_be_false = "Expected condition to be false.";
    public const string Expected_condition_to_be_true = "Expected condition to be true.";
    public const string File_0_Line_1 = " File='{0}', Line={1}";
    public const string This_program_location_is_thought_to_be_unreachable = "This program location is thought to be unreachable.";

    public static string FormatCould_not_read_value_JSON_token_was_0(JsonToken token)
        => string.Format(Could_not_read_value_JSON_token_was_0, token);

    public static string FormatEncountered_unexpected_JSON_property_0(string propertyName)
        => string.Format(Encountered_unexpected_JSON_property_0, propertyName);

    public static string FormatEncountered_unexpected_JSON_token_0(JsonToken token)
        => string.Format(Encountered_unexpected_JSON_token_0, token);

    public static string FormatExpected_JSON_property_0_but_it_was_1(string expectedPropertyName, string? actualPropertyName)
        => string.Format(Expected_JSON_property_0_but_it_was_1, expectedPropertyName, actualPropertyName);

    public static string FormatExpected_JSON_token_0_but_it_was_1(JsonToken expectedToken, JsonToken actualToken)
        => string.Format(Expected_JSON_token_0_but_it_was_1, expectedToken, actualToken);

    public static string FormatExpected_0_to_be_non_null(string? name)
        => string.Format(Expected_0_to_be_non_null, name);

    public static string FormatFile_0_Line_1(string? path, int line)
        => string.Format(File_0_Line_1, path, line);
}
