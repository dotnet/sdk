// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Microsoft.Testing.Platform.OutputDevice;

internal static class TargetFrameworkParser
{
    public static string? GetShortTargetFramework(string? frameworkDescription)
    {
        if (frameworkDescription == null)
        {
            return null;
        }

        // https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.runtimeinformation.frameworkdescription
        string netFramework = ".NET Framework";
        if (frameworkDescription.StartsWith(netFramework, ignoreCase: false, CultureInfo.InvariantCulture))
        {
            // .NET Framework 4.7.2
            if (frameworkDescription.Length < (netFramework.Length + 6))
            {
                return frameworkDescription;
            }

            char major = frameworkDescription[netFramework.Length + 1];
            char minor = frameworkDescription[netFramework.Length + 3];
            char patch = frameworkDescription[netFramework.Length + 5];

            if (major == '4' && minor == '6' && patch == '2')
            {
                return "net462";
            }
            else if (major == '4' && minor == '7' && patch == '1')
            {
                return "net471";
            }
            else if (major == '4' && minor == '7' && patch == '2')
            {
                return "net472";
            }
            else if (major == '4' && minor == '8' && patch == '1')
            {
                return "net481";
            }
            else
            {
                // Just return the first 2 numbers.
                return $"net{major}{minor}";
            }
        }

        string netCore = ".NET Core";
        if (frameworkDescription.StartsWith(netCore, ignoreCase: false, CultureInfo.InvariantCulture))
        {
            // .NET Core 3.1
            return frameworkDescription.Length >= (netCore.Length + 4)
                ? $"netcoreapp{frameworkDescription[netCore.Length + 1]}.{frameworkDescription[netCore.Length + 3]}"
                : frameworkDescription;
        }

        string net = ".NET";
        if (frameworkDescription.StartsWith(net, ignoreCase: false, CultureInfo.InvariantCulture))
        {
            int firstDotInVersion = frameworkDescription.IndexOf('.', net.Length + 1);
            return firstDotInVersion < 1
                ? frameworkDescription
                : $"net{frameworkDescription.Substring(net.Length + 1, firstDotInVersion - net.Length - 1)}.{frameworkDescription[firstDotInVersion + 1]}";
        }

        return frameworkDescription;
    }
}
