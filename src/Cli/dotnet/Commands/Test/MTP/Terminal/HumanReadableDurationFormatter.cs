// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal static class HumanReadableDurationFormatter
{
    private static string DaysAbbreviation => CliCommandStrings.DurationDaysAbbreviation;
    private static string HoursAbbreviation => CliCommandStrings.DurationHoursAbbreviation;
    private static string MinutesAbbreviation => CliCommandStrings.DurationMinutesAbbreviation;
    private static string SecondsAbbreviation => CliCommandStrings.DurationSecondsAbbreviation;
    private static string MillisecondsAbbreviation => CliCommandStrings.DurationMillisecondsAbbreviation;

    public static void Append(ITerminal terminal, TimeSpan duration, bool wrapInParentheses = true)
    {
        bool hasParentValue = false;

        if (wrapInParentheses)
        {
            terminal.Append('(');
        }

        if (duration.Days > 0)
        {
            terminal.Append($"{duration.Days}{DaysAbbreviation}");
            hasParentValue = true;
        }

        if (duration.Hours > 0 || hasParentValue)
        {
            terminal.Append(GetFormattedPart(duration.Hours, hasParentValue, HoursAbbreviation));
            hasParentValue = true;
        }

        if (duration.Minutes > 0 || hasParentValue)
        {
            terminal.Append(GetFormattedPart(duration.Minutes, hasParentValue, MinutesAbbreviation));
            hasParentValue = true;
        }

        if (duration.Seconds > 0 || hasParentValue)
        {
            terminal.Append(GetFormattedPart(duration.Seconds, hasParentValue, SecondsAbbreviation));
            hasParentValue = true;
        }

        if (duration.Milliseconds >= 0 || hasParentValue)
        {
            terminal.Append(GetFormattedPart(duration.Milliseconds, hasParentValue, MillisecondsAbbreviation, paddingWitdh: 3));
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
            stringBuilder.Append(CultureInfo.CurrentCulture, $"{duration.Value.Days}{DaysAbbreviation}");
            hasParentValue = true;
        }

        if (duration.Value.Hours > 0 || hasParentValue)
        {
            stringBuilder.Append(GetFormattedPart(duration.Value.Hours, hasParentValue, HoursAbbreviation));
            hasParentValue = true;
        }

        if (duration.Value.Minutes > 0 || hasParentValue)
        {
            stringBuilder.Append(GetFormattedPart(duration.Value.Minutes, hasParentValue, MinutesAbbreviation));
            hasParentValue = true;
        }

        if (duration.Value.Seconds > 0 || hasParentValue || !showMilliseconds)
        {
            stringBuilder.Append(GetFormattedPart(duration.Value.Seconds, hasParentValue, SecondsAbbreviation));
            hasParentValue = true;
        }

        if (showMilliseconds)
        {
            if (duration.Value.Milliseconds >= 0 || hasParentValue)
            {
                stringBuilder.Append(GetFormattedPart(duration.Value.Milliseconds, hasParentValue, MillisecondsAbbreviation, paddingWitdh: 3));
            }
        }

        if (wrapInParentheses)
        {
            stringBuilder.Append(')');
        }

        return stringBuilder.ToString();
    }
}
