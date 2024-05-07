﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.TestFramework
{
    public class TestAssetInfo
    {
        // This is needed each release after we upgrade to 9.0 but the templates haven't been upgraded yet

        public static readonly string currentTfm = "net9.0";

        private readonly string [] FilesToExclude = { ".DS_Store", ".noautobuild" };

        public string AssetName { get; private set; }

        public FileInfo DotnetExeFile => _testAssets.DotnetCsprojExe;

        public string ProjectFilePattern => "*.csproj";

        public DirectoryInfo Root { get; private set; }

        private TestAssets _testAssets { get; }

        internal TestAssetInfo(DirectoryInfo root, string assetName, TestAssets testAssets)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (string.IsNullOrWhiteSpace(assetName))
            {
                throw new ArgumentException("Argument cannot be null or whitespace", nameof(assetName));
            }

            if (testAssets == null)
            {
                throw new ArgumentNullException(nameof(testAssets));
            }

            Root = root;

            AssetName = assetName;

            _testAssets = testAssets;
        }

        public TestAssetInstance CreateInstance([CallerMemberName] string callingMethod = "", string identifier = "")
        {
            var instancePath = GetTestDestinationDirectory(callingMethod, identifier);

            var testInstance = new TestAssetInstance(this, instancePath);

            return testInstance;
        }

        internal IEnumerable<FileInfo> GetSourceFiles()
        {
            ThrowIfTestAssetDoesNotExist();

            return Root.GetFiles("*.*", SearchOption.AllDirectories)
                        .Where(f => !FilesToExclude.Contains(f.Name));
        }

        private DirectoryInfo GetTestDestinationDirectory(string callingMethod, string identifier) => _testAssets.CreateTestDirectory(AssetName, callingMethod, identifier);

        private void ThrowIfTestAssetDoesNotExist()
        {
            if (!Root.Exists)
            {
                throw new DirectoryNotFoundException($"Directory not found at '{Root.FullName}'");
            }
        }
    }
}
