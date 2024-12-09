// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable enable
#pragma warning restore IDE0240 // Remove redundant nullable directive

using System.Diagnostics;
using Microsoft.DotNet.NativeWrapper;

#if NETFRAMEWORK
using Microsoft.VisualStudio.Setup.Configuration;
#endif

namespace Microsoft.DotNet.DotNetSdkResolver
{
    public sealed class VSSettings
    {
        private readonly object _lock = new();
        private readonly string? _settingsFilePath;
        private readonly bool _disallowPrereleaseByDefault;
        private FileInfo? _settingsFile;
        private bool _disallowPrerelease;

        // In the product, this singleton is used. It must be safe to use in parallel on multiple threads.
        // In tests, mock instances can be created with the test constructor below.
        public static readonly VSSettings Ambient = new();

        private VSSettings()
        {
#if NETFRAMEWORK
            if (!Interop.RunningOnWindows)
            {
                return;
            }

            var instance = GetSetupInstanceForCurrentProcess();
            if (instance == null)
            {
                return;
            }

            var instanceId = instance.GetInstanceId();
            var installationVersion = instance.GetInstallationVersion();
            var isPrerelease = ((ISetupInstanceCatalog)instance).IsPrerelease();
            var version = Version.Parse(installationVersion);

            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "VisualStudio",
                version.Major + ".0_" + instanceId,
                "sdk.txt");

            _disallowPrereleaseByDefault = !isPrerelease;
            _disallowPrerelease = _disallowPrereleaseByDefault;
#endif
        }

        // Test constructor
        public VSSettings(string settingsFilePath, bool disallowPrereleaseByDefault)
        {
            _settingsFilePath = settingsFilePath;
            _disallowPrereleaseByDefault = disallowPrereleaseByDefault;
            _disallowPrerelease = _disallowPrereleaseByDefault;
        }

        public bool DisallowPrerelease()
        {
            if (_settingsFilePath != null)
            {
                Refresh();
            }

            return _disallowPrerelease;
        }

        private void Refresh()
        {
            Debug.Assert(_settingsFilePath != null);

            var file = new FileInfo(_settingsFilePath);

            // NB: All calls to Exists and LastWriteTimeUtc below will not hit the disk
            //     They will return data obtained during Refresh() here.
            file.Refresh();

            lock (_lock)
            {
                // File does not exist -> use default.
                if (!file.Exists)
                {
                    _disallowPrerelease = _disallowPrereleaseByDefault;
                    _settingsFile = file;
                    return;
                }

                // File has not changed -> reuse prior read.
                if (_settingsFile?.Exists == true && file.LastWriteTimeUtc <= _settingsFile.LastWriteTimeUtc)
                {
                    return;
                }

                // File has changed -> read from disk
                // If we encounter an I/O exception, assume writer is in the process of updating file,
                // ignore the exception, and use stale settings until the next resolution.
                try
                {
                    ReadFromDisk();
                    _settingsFile = file;
                    return;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private void ReadFromDisk()
        {
            if (_settingsFilePath != null)
            {
                using (var reader = new StreamReader(_settingsFilePath))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        int indexOfEquals = line.IndexOf('=');
                        if (indexOfEquals < 0 || indexOfEquals == (line.Length - 1))
                        {
                            continue;
                        }

                        string key = line.Substring(0, indexOfEquals).Trim();
                        string value = line.Substring(indexOfEquals + 1).Trim();

                        if (key.Equals("UsePreviews", StringComparison.OrdinalIgnoreCase)
                            && bool.TryParse(value, out bool usePreviews))
                        {
                            _disallowPrerelease = !usePreviews;
                            return;
                        }
                    }
                }
            }

            // File does not have UsePreviews entry -> use default
            _disallowPrerelease = _disallowPrereleaseByDefault;
        }

#if NETFRAMEWORK
        // The custom interop here is done to avoid first-chance exceptions in non-exceptional circumstances.
        private const string CLSID_SetupConfiguration = "177F0C4A-1CD3-4DE7-A32C-71DBBB9FA36D";
        private const string IID_ISetupConfiguration = "42843719-DB4C-46C2-8E7C-64F1816EFD5B";
        private const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);
        private const int CLSCTX_INPROC_SERVER = 1;
        private const nint NO_ERROR_INFO = -1;

        private static ISetupInstance? GetSetupInstanceForCurrentProcess()
        {
            var clsid = new Guid(CLSID_SetupConfiguration);
            var iid = new Guid(IID_ISetupConfiguration);
            var hr = CoCreateInstance(clsid, null, CLSCTX_INPROC_SERVER, iid, out var obj);

            if (hr == REGDB_E_CLASSNOTREG)
            {
                // Visual Studio is not installed.
                return null; 
            }

            // Other errors from CoCreateInstance are not expected and would indicate a bug.
            Marshal.ThrowExceptionForHR(hr, NO_ERROR_INFO);

            var configuration = (ISetupConfiguration)obj;
            hr = configuration.GetInstanceForCurrentProcess(out var instance);
            if (hr != 0)
            {
                // Main module of current process is not in a Visual Studio directory.
                return null;
            }

            return instance;
        }

        [ComImport, Guid(IID_ISetupConfiguration), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISetupConfiguration
        {
            void _VtblGap1_1();
            [PreserveSig] int GetInstanceForCurrentProcess([MarshalAs(UnmanagedType.Interface)] out ISetupInstance instance);
        }

        [DllImport("ole32.dll")]
        private static extern int CoCreateInstance(
            in Guid clsid,
            [MarshalAs(UnmanagedType.IUnknown)] object? outer,
            int context,
            in Guid iid,
            [MarshalAs(UnmanagedType.IUnknown)] out object obj);
#endif
    }
}

