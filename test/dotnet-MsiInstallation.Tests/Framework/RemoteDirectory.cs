// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    abstract class RemoteDirectory
    {
        public string Path { get; }

        protected RemoteDirectory(string path)
        {
            Path = path;
        }

        public abstract bool Exists { get; }

        public abstract List<string> Directories { get; }

        public abstract List<string> Files { get; }

        public Assertions Should()
        {
            return new Assertions(this);
        }

        public class Assertions
        {
            RemoteDirectory _directory;

            public Assertions(RemoteDirectory directory)
            {
                _directory = directory;
            }

            public AndConstraint<Assertions> Exist()
            {
                _directory.Exists.Should().BeTrue($"Expected directory {_directory.Path} to exist, but it does not.");
                return new AndConstraint<Assertions>(this);
            }
            public AndConstraint<Assertions> NotExist()
            {
                _directory.Exists.Should().BeFalse($"Expected directory {_directory.Path} to not exist, but it does.");
                return new AndConstraint<Assertions>(this);
            }
        }
    }
}
