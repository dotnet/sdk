// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Assertions
{
    public class DirectoryInfoAssertions
    {
        private DirectoryInfo _dirInfo;

        public DirectoryInfoAssertions(DirectoryInfo dir)
        {
            _dirInfo = dir;
        }

        public DirectoryInfo DirectoryInfo => _dirInfo;

        public AndConstraint<DirectoryInfoAssertions> Exist()
        {
            _dirInfo.Exists.Should().BeTrue($"Expected directory {_dirInfo.FullName} to exist, but it does not.");
            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotExist()
        {
            _dirInfo.Exists.Should().BeFalse($"Expected directory {_dirInfo.FullName} to not exist, but it does.");
            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> HaveFile(string expectedFile)
        {
            var file = _dirInfo.EnumerateFiles(expectedFile, SearchOption.TopDirectoryOnly).SingleOrDefault() ?? new FileInfo(Path.Combine(_dirInfo.FullName, expectedFile));
            file.Should().Exist($"Expected File {expectedFile} cannot be found in directory {_dirInfo.FullName}.");
            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotHaveFile(string expectedFile)
        {
            var file = _dirInfo.EnumerateFiles(expectedFile, SearchOption.TopDirectoryOnly).SingleOrDefault() ?? new FileInfo(Path.Combine(_dirInfo.FullName, expectedFile));
            file.Should().NotExist($"File {expectedFile} should not be found in directory {_dirInfo.FullName}.");
            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> HaveFiles(IEnumerable<string> expectedFiles)
        {
            foreach (var expectedFile in expectedFiles)
            {
                HaveFile(expectedFile);
            }

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> HaveFilesMatching(
            string expectedFilesSearchPattern,
            SearchOption searchOption,
            string because = "",
            params object[] reasonArgs)
        {
            var matchingFileExists = _dirInfo.EnumerateFiles(expectedFilesSearchPattern, searchOption).Any();

            matchingFileExists.Should().BeTrue($"Expected directory {_dirInfo.FullName} to contain files matching {expectedFilesSearchPattern}, but no matching file exists.");

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotHaveFiles(IEnumerable<string> expectedFiles)
        {
            foreach (var expectedFile in expectedFiles)
            {
                NotHaveFile(expectedFile);
            }

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotHaveFilesMatching(string expectedFilesSearchPattern, SearchOption searchOption)
        {
            var matchingFileCount = _dirInfo.EnumerateFiles(expectedFilesSearchPattern, searchOption).Count();
            matchingFileCount.Should().Be(0, $"Found {matchingFileCount} files that should not exist in directory {_dirInfo.FullName}. No file matching {expectedFilesSearchPattern} should exist.");
            return new AndConstraint<DirectoryInfoAssertions>(this);
        }


        public AndConstraint<DirectoryInfoAssertions> HaveDirectory(string expectedDir)
        {
            var dir = _dirInfo.EnumerateDirectories(expectedDir, SearchOption.TopDirectoryOnly).SingleOrDefault() ?? new DirectoryInfo(expectedDir);
            dir.Exists.Should().BeTrue($"Expected directory {expectedDir} cannot be found inside directory {_dirInfo.FullName}.");

            return new AndConstraint<DirectoryInfoAssertions>(new DirectoryInfoAssertions(dir ?? new DirectoryInfo(expectedDir)));
        }

        public AndConstraint<DirectoryInfoAssertions> OnlyHaveFiles(IEnumerable<string> expectedFiles, SearchOption searchOption = SearchOption.AllDirectories)
        {
            var actualFiles = _dirInfo.EnumerateFiles("*", searchOption)
                              .Select(f => f.FullName.Substring(_dirInfo.FullName.Length + 1) // make relative to _dirInfo
                              .Replace("\\", "/")); // normalize separator

            var missingFiles = expectedFiles.Except(actualFiles);
            var extraFiles = actualFiles.Except(expectedFiles);
            var nl = Environment.NewLine;

            missingFiles.Should().BeEmpty($"Following files cannot be found inside directory {_dirInfo.FullName} {nl} {string.Join(nl, missingFiles)}");
            extraFiles.Should().BeEmpty($"Following extra files are found inside directory {_dirInfo.FullName} {nl} {string.Join(nl, extraFiles)}");

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> BeEmpty()
        {
            _dirInfo.EnumerateFileSystemInfos().Any().Should().BeFalse($"The directory {_dirInfo.FullName} is not empty.");

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotBeEmpty()
        {
            _dirInfo.EnumerateFileSystemInfos().Any().Should().BeTrue($"The directory {_dirInfo.FullName} is empty.");

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotExist(string because = "", params object[] reasonArgs)
        {
            _dirInfo.Exists.Should().BeFalse($"Expected directory {_dirInfo.FullName} to not exist, but it does.");

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }

        public AndConstraint<DirectoryInfoAssertions> NotHaveSubDirectories(params string[] notExpectedSubdirectories)
        {
            notExpectedSubdirectories = notExpectedSubdirectories ?? Array.Empty<string>();

            var subDirectories = _dirInfo.EnumerateDirectories();


            if (!notExpectedSubdirectories.Any())
            {
                subDirectories.Any().Should().BeFalse($"Directory {_dirInfo.FullName} should not have any sub directories.");
            }
            else
            {
                var actualSubDirectories = subDirectories
                                            .Select(f => f.FullName.Substring(_dirInfo.FullName.Length + 1) // make relative to _dirInfo
                                            .Replace("\\", "/")); // normalize separator

                var errorSubDirectories = notExpectedSubdirectories.Intersect(actualSubDirectories);

                var nl = Environment.NewLine;
                errorSubDirectories.Should().BeEmpty($"The following subdirectories should not be found inside directory {_dirInfo.FullName} {nl} {string.Join(nl, errorSubDirectories)}");
            }

            return new AndConstraint<DirectoryInfoAssertions>(this);
        }
    }
}
