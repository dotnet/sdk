// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.NetworkInformation;

namespace Microsoft.DotNet.Cli.Telemetry;

internal static class MacAddressGetter
{
    private static readonly byte[] AllZeroBytes = new byte[] { 0, 0, 0, 0, 0, 0 };

    public static string? GetMacAddress()
    {
        try
        {
            return GetMacAddressByNetworkInterface();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetMacAddressByNetworkInterface()
    {
        return GetMacAddressesByNetworkInterface().FirstOrDefault();
    }

    private static List<string> GetMacAddressesByNetworkInterface()
    {
        NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
        var macs = new List<string>();

        if (nics == null || nics.Length < 1)
        {
            return macs;
        }

        foreach (NetworkInterface adapter in nics)
        {
            byte[] bytes = adapter.GetPhysicalAddress().GetAddressBytes();
            if (bytes.Length == 0 || bytes.SequenceEqual(AllZeroBytes))
            {
                continue;
            }

            macs.Add(string.Join("-", bytes.Select(x => x.ToString("X2"))));
            if (macs.Count >= 10)
            {
                break;
            }
        }

        return macs;
    }
}
