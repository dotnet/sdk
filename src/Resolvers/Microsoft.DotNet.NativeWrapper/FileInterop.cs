// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper
{
    public static partial class FileInterop
    {
        public static readonly bool RunningOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static class Unix
        {
            // Ansi marshaling on Unix is actually UTF8
            private const CharSet UTF8 = CharSet.Ansi;
            private static string? PtrToStringUTF8(IntPtr ptr) => Marshal.PtrToStringAnsi(ptr);

            [DllImport("libc", CharSet = UTF8, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr realpath(string path, IntPtr buffer);

            [DllImport("libc", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
            private static extern void free(IntPtr ptr);

            public static string? realpath(string path)
            {
                var ptr = realpath(path, IntPtr.Zero);
                var result = PtrToStringUTF8(ptr);
                free(ptr);
                return result;
            }
        }
    }
}
