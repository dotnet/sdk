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

        if (nics == null || nics.Length < 1)
        {
            return new List<string>();
        }

        return nics
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                          nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Select(nic => nic.GetPhysicalAddress().GetAddressBytes())
            .Where(bytes => bytes.Length == 6 && !bytes.SequenceEqual(AllZeroBytes))
            .Select(bytes => string.Join("-", bytes.Select(b => b.ToString("X2"))))
            .ToList();
    }
}
