// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal static class DeviceIdGetter
    {
        public static string GetDeviceId()
        {
            string deviceId = GetCachedDeviceId();

            // Check if the device Id is already cached
            if (string.IsNullOrEmpty(deviceId))
            {
                // Generate a new guid
                deviceId = Guid.NewGuid().ToString("D").ToLowerInvariant();

                // Cache the new device Id
                try
                {
                    CacheDeviceId(deviceId);
                }
                catch
                {
                    // If caching fails, return empty string to avoid sending a non-stored id
                    deviceId = "";
                }
            }

            return deviceId;
        }

        private static string GetCachedDeviceId()
        {
            string deviceId = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Get device Id from Windows registry matching the OS architecture
                using (var key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\DeveloperTools"))
                {
                    deviceId = key?.GetValue("deviceid") as string;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Get device Id from Linux cache file
                string cacheFilePath;
                string xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
                if (!string.IsNullOrEmpty(xdgCacheHome))
                {
                    cacheFilePath = Path.Combine(xdgCacheHome, "Microsoft", "DeveloperTools", "deviceid");
                }
                else
                {
                    cacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "deviceid");
                }

                if (File.Exists(cacheFilePath))
                {
                    deviceId = File.ReadAllText(cacheFilePath);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Get device Id from macOS cache file
                string cacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Microsoft", "DeveloperTools", "deviceid");
                if (File.Exists(cacheFilePath))
                {
                    deviceId = File.ReadAllText(cacheFilePath);
                }
            }

            return deviceId;
        }

        private static void CacheDeviceId(string deviceId)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Cache device Id in Windows registry matching the OS architecture
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                {
                    using(var key = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\DeveloperTools"))
                    {
                        if (key != null)
                        {
                          key.SetValue("deviceid", deviceId);
                        }
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Cache device Id in Linux cache file
                string cacheFilePath;
                string xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
                if (!string.IsNullOrEmpty(xdgCacheHome))
                {
                    cacheFilePath = Path.Combine(xdgCacheHome, "Microsoft", "DeveloperTools", "deviceId");
                }
                else
                {
                    cacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "deviceid");
                }

                CreateDirectoryAndWriteToFile(cacheFilePath, deviceId);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Cache device Id in macOS cache file
                string cacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Microsoft", "DeveloperTools", "deviceid");

                CreateDirectoryAndWriteToFile(cacheFilePath, deviceId);
            }
        }

        private static void CreateDirectoryAndWriteToFile(string filePath, string content)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(filePath, content);
        }
    }
}
