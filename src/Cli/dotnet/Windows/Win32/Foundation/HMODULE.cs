// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.System.LibraryLoader;
using Windows.Win32.UI.Shell;

namespace Windows.Win32.Foundation;

/// <summary>
///  Module handle.
/// </summary>
internal unsafe partial struct HMODULE
{
    private const string DllGetVersionMethodName = "DllGetVersion";

    private delegate HRESULT DllGetVersionProc(DLLVERSIONINFO* version);

    /// <inheritdoc cref="FromAddress(void*, bool)"/>
    public static HMODULE FromAddress(nint address, bool incrementRefCount = false)
        => FromAddress((void*)address, incrementRefCount);

    /// <summary>
    ///  Gets the module that the specified memory address is in, if any.  Do not release this handle unless
    ///  <paramref name="incrementRefCount"/> is <see langword="true"/>.
    /// </summary>
    /// <returns>The found instance or <see cref="Null"/>.</returns>
    /// <inheritdoc cref="PInvokeMadowaku.GetModuleHandleEx(uint, PCWSTR, HMODULE*)"/>
    public static HMODULE FromAddress(void* address, bool incrementRefCount = false)
    {
        HMODULE hmodule;
        PInvoke.GetModuleHandleEx(
            PInvoke.GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS
                | (incrementRefCount ? 0 : PInvoke.GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT),
            (PCWSTR)address,
            &hmodule);

        return hmodule;
    }

    /// <summary>
    ///  Gets the module for the launching executable. Do not release this handle.
    /// </summary>
    /// <returns>The found instance or <see cref="Null"/>.</returns>
    /// <inheritdoc cref="PInvokeMadowaku.GetModuleHandleEx(uint, PCWSTR, HMODULE*)"/>
    public static HMODULE GetLaunchingExecutable()
    {
        HMODULE hmodule;
        PInvoke.GetModuleHandleEx(
            PInvoke.GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            (PCWSTR)null,
            &hmodule);

        return hmodule;
    }

    /// <summary>
    ///  Gets the module for the launching executable. Do not release this handle unless
    ///  <paramref name="incrementRefCount"/> is <see langword="true"/>.
    /// </summary>
    /// <returns>The found instance or <see cref="Null"/>.</returns>
    /// <inheritdoc cref="PInvokeMadowaku.GetModuleHandleEx(uint, PCWSTR, HMODULE*)"/>
    public static HMODULE FromName(string name, bool incrementRefCount = false)
    {
        fixed (char* n = name)
        {
            HMODULE hmodule;

            PInvoke.GetModuleHandleEx(
                incrementRefCount ? 0 : PInvoke.GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                (PCWSTR)n,
                &hmodule);

            return hmodule;
        }
    }

    /// <summary>
    ///  Loads the module from the specified file path. Throws on failure.
    /// </summary>
    public static HMODULE LoadModule(string filePath, LOAD_LIBRARY_FLAGS flags = default)
    {
        HMODULE module = PInvoke.LoadLibraryEx(filePath, flags);
        if (module.IsNull)
        {
            Error.ThrowLastError();
        }

        return module;
    }

    /// <summary>
    ///  Gets the dll version of the module. Throws if the module doesn't expose `DllGetVersion`.
    /// </summary>
    public Version GetDllVersion()
    {
        FARPROC proc = PInvoke.GetProcAddress(this, DllGetVersionMethodName);

        if (proc.IsNull)
        {
            Error.ThrowLastError();
        }

        DLLVERSIONINFO versionInfo = new()
        {
            cbSize = (uint)sizeof(DLLVERSIONINFO)
        };

        // HRESULT Dllgetversionproc(DLLVERSIONINFO* version)
#if NETFRAMEWORK
        Marshal.GetDelegateForFunctionPointer<DllGetVersionProc>(proc.Value)(&versionInfo).ThrowOnFailure();
#else
        ((delegate* unmanaged<DLLVERSIONINFO*, HRESULT>)proc.Value)(&versionInfo).ThrowOnFailure();
#endif

        return new Version(
            (int)versionInfo.dwMajorVersion,
            (int)versionInfo.dwMinorVersion,
            (int)versionInfo.dwBuildNumber,
            (int)versionInfo.dwPlatformID);
    }

    /// <summary>
    ///  Gets the address of the specified procedure. Throws if the procedure can't be found.
    /// </summary>
    public FARPROC GetProcAddress(string procName)
    {
        FARPROC proc = PInvoke.GetProcAddress(this, procName);
        if (proc.IsNull)
        {
            Error.ThrowLastError();
        }

        return proc;
    }
}
