// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.DotNet.Cli.Utils;

#pragma warning disable CA1416
internal class WindowsRegistryEnvironmentPathEditor : IWindowsRegistryEnvironmentPathEditor
{
    private const string Path = "PATH";

    public string? Get(SdkEnvironmentVariableTarget sdkEnvironmentVariableTarget)
    {
        using (RegistryKey? environmentKey = OpenEnvironmentKeyIfExists(writable: false, sdkEnvironmentVariableTarget: sdkEnvironmentVariableTarget))
        {
            return environmentKey?.GetValue(Path, "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
    }

    public void Set(string value, SdkEnvironmentVariableTarget sdkEnvironmentVariableTarget)
    {
        using (RegistryKey? environmentKey = OpenEnvironmentKeyIfExists(writable: true, sdkEnvironmentVariableTarget: sdkEnvironmentVariableTarget))
        {
            environmentKey?.SetValue(Path, value, RegistryValueKind.ExpandString);
        }

        Task.Factory.StartNew(() =>
        {
            unsafe
            {
                // send a WM_SETTINGCHANGE message to all windows
                fixed (char* lParam = "Environment")
                {
                    LRESULT r = PInvoke.SendMessageTimeout(
                        HWND.HWND_BROADCAST,
                        PInvoke.WM_SETTINGCHANGE,
                        default,
                        (LPARAM)(nint)lParam,
                        0,
                        1000,
                        out nuint _);
                }
            }
        });
    }

    private static RegistryKey? OpenEnvironmentKeyIfExists(bool writable, SdkEnvironmentVariableTarget sdkEnvironmentVariableTarget)
    {
        RegistryKey baseKey;
        string keyName;

        if (sdkEnvironmentVariableTarget == SdkEnvironmentVariableTarget.CurrentUser)
        {
            baseKey = Registry.CurrentUser;
            keyName = "Environment";
        }
        else if (sdkEnvironmentVariableTarget == SdkEnvironmentVariableTarget.DotDefault)
        {
            baseKey = Registry.Users;
            keyName = ".DEFAULT\\Environment";
        }
        else
        {
            throw new ArgumentException($"{nameof(sdkEnvironmentVariableTarget)} cannot be matched, the value is: {sdkEnvironmentVariableTarget}");
        }

        return baseKey.OpenSubKey(keyName, writable: writable);
    }
}
#pragma warning restore CA1416
