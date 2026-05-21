// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

public static class EnvironmentVariableParser
{
    public static bool ParseBool(string? str, bool defaultValue)
    {
        if (str is "1" ||
            string.Equals(str, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(str, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(str, "on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (str is "0" ||
            string.Equals(str, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(str, "no", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(str, "off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return defaultValue;
    }
}
