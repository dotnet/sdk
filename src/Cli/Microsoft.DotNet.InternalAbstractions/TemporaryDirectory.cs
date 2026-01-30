// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.InternalAbstractions
{
    internal class TemporaryDirectory : ITemporaryDirectory
    {
        public string DirectoryPath { get; }

        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(CreateSubdirectory());
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(DirectoryPath, true);
            }
            catch
            {
                // Ignore failures here.
            }
        }

        public static string CreateSubdirectory()
        {
#if NETFRAMEWORK
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
#else
            return Directory.CreateTempSubdirectory().FullName;
#endif
        }
    }
}
