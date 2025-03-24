// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal static class HumanReadableDurationFormatter
{
    public static void Append(ITerminal terminal, TimeSpan duration, bool wrapInParentheses = true)
    {
        bool hasParentValue = false;

        if (wrapInParentheses)
        {
            terminal.Append('(');
        }

        if (duration.Days > 0)
        {
            terminal.Append($"{duration.Days}d");
            hasParentValue = true;
        }

        if (duration.Hours > 0 || hasParentValue)
        {
            terminal.Append(GetFormattedPart(duration.Hours, hasParentValue, "h"));
            hasParentValue = true;
        }

        if (duration.Minutes > 0 || hasParentValue)
        {
            terminal.Append(GetFormattedPart(duration.Minutes, hasParentValue, "m"));
            hasParentValue = true;
        }

        if (duration.Seconds > 0 || hasParentValue)
        {
            terminal.Append(GetFormattedPart(duration.Seconds, hasParentValue, "s"));
            hasParentValue = true;
        }

        if (duration.Milliseconds >= 0 || hasParentValue)
        {
            terminal.Append(GetFormattedPart(duration.Milliseconds, hasParentValue, "ms", paddingWitdh: 3));
        }

        if (wrapInParentheses)
        {
            terminal.Append(')');
        }
    }

    private static string GetFormattedPart(int value, bool hasParentValue, string suffix, int paddingWitdh = 2)
        => $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? value.ToString(CultureInfo.InvariantCulture).PadLeft(paddingWitdh, '0') : value.ToString(CultureInfo.InvariantCulture))}{suffix}";

    public static string Render(TimeSpan? duration, bool wrapInParentheses = true, bool showMilliseconds = false)
    {
        if (duration is null)
        {
            return string.Empty;
        }

        bool hasParentValue = false;

        var stringBuilder = new StringBuilder();

        if (wrapInParentheses)
        {
            stringBuilder.Append('(');
        }

        if (duration.Value.Days > 0)
        {
            stringBuilder.Append(CultureInfo.CurrentCulture, $"{duration.Value.Days}d");
            hasParentValue = true;
        }

        if (duration.Value.Hours > 0 || hasParentValue)
        {
            stringBuilder.Append(GetFormattedPart(duration.Value.Hours, hasParentValue, "h"));
            hasParentValue = true;
        }

        if (duration.Value.Minutes > 0 || hasParentValue)
        {
            stringBuilder.Append(GetFormattedPart(duration.Value.Minutes, hasParentValue, "m"));
            hasParentValue = true;
        }

        if (duration.Value.Seconds > 0 || hasParentValue || !showMilliseconds)
        {
            stringBuilder.Append(GetFormattedPart(duration.Value.Seconds, hasParentValue, "s"));
            hasParentValue = true;
        }

        if (showMilliseconds)
        {
            if (duration.Value.Milliseconds >= 0 || hasParentValue)
            {
                stringBuilder.Append(GetFormattedPart(duration.Value.Milliseconds, hasParentValue, "ms", paddingWitdh: 3));
            }
        }

        if (wrapInParentheses)
        {
            stringBuilder.Append(')');
        }

        return stringBuilder.ToString();
    }
}
