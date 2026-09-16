// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Checks update paths before ordinary runtime file operations in a stable installation directory.</summary>
internal static class SelfUpdateFile
{
    public static FileStream Open(string path, FileAccess access = FileAccess.Read)
    {
        path = Path.GetFullPath(path);
        SelfUpdatePaths.ValidateDirectory(Path.GetDirectoryName(path)!);
        if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
        {
            throw new IOException($"Self-update requires a file without links or reparse points: '{path}'.");
        }

        return new FileStream(path, FileMode.Open, access, FileShare.Read | FileShare.Delete);
    }

    public static bool Exists(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public static void RequireAbsent(string path)
    {
        if (Exists(path))
        {
            throw new IOException($"Self-update will not overwrite an occupied recovery path: '{path}'.");
        }
    }

}