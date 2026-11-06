// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;

namespace Microsoft.NET.TestFramework.Utilities;

public static class FileUtility
{
    /// <summary>
    /// Computes a file's SHA-512 hash.
    /// </summary>
    public static string GetFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha512 = SHA512.Create();
        return Convert.ToBase64String(sha512.ComputeHash(stream));
    }

    /// <summary>
    /// Copies a file to <paramref name="destination"/>, retrying on IOException
    /// (e.g. file lock). If the destination already exists the copy is skipped. If all
    /// retries are exhausted the failure is logged as a warning and the method returns
    /// without throwing.
    /// </summary>
    public static void TryCopyFile(string source, string destination, ITestOutputHelper log, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                File.Copy(source, destination);
                return;
            }
            catch (IOException) when (File.Exists(destination))
            {
                log.WriteLine($"⚠️ Destination already exists, skipping copy: '{destination}' (source: '{source}')");
                return;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                Thread.Sleep(attempt * 500);
            }
            catch (IOException ex)
            {
                log.WriteLine($"⚠️ Failed to copy '{source}' after {maxRetries} attempts: {ex.Message}");
            }
        }
    }
}
