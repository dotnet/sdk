﻿using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LibraryWithRids
{
    public class NativeCode
    {
        public static string InvokeNativeCodeAndReturnAString()
        {
            switch(GetRidStoredInAssemblyDescriptionAttribute())
            {
                case "'centos.9-x64'":
                case "'rhel.9-x64'":
                case "'debian.9-x64'":
                case "'fedora.37-x64'":
                case "'opensuse.42.3-x64'":
                case "'linux-musl-x64'":
                case "'ubuntu.14.04-x64'":
                case "'ubuntu.16.04-x64'":
                case "'ubuntu.16.10-x64'":
                case "'ubuntu.18.04-x64'":
                case "'ubuntu.20.04-x64'":
                case "'ubuntu.22.04-x64'":
                    return Marshal.PtrToStringAnsi(LinuxNativeMethods.sqlite3_libversion());
                case "'osx.10.10-x64'": 
                case "'osx.10.11-x64'": 
                case "'osx.10.12-x64'":
                case "'osx.10.14-x64'":
                case "'osx.10.15-x64'":
                case "'osx.11.0-x64'":
                case "'osx.12-x64'":
                case "'osx.13-x64'":
                    return Marshal.PtrToStringAnsi(MacNativeMethods.sqlite3_libversion());
                case "'win10-x64'":
                case "'win10-x86'":
                    return Marshal.PtrToStringAnsi(WindowsNativeMethods.sqlite3_libversion());
                default:
                    return "Unexpected RID. Cannot find sqlite3.";
            }            
        }

        public static string GetRidStoredInAssemblyDescriptionAttribute()
        {
            return typeof(NativeCode)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute<AssemblyDescriptionAttribute>()
                ?.Description;
        }
    }
}
