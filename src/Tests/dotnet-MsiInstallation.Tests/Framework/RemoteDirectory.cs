// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions.Execution;

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
                Execute.Assertion.ForCondition(_directory.Exists)
                    .FailWith("Expected directory {0} does not exist.", _directory.Path);
                return new AndConstraint<Assertions>(this);
            }
            public AndConstraint<Assertions> NotExist()
            {
                Execute.Assertion.ForCondition(!_directory.Exists)
                    .FailWith("Expected directory {0} not to exist.", _directory.Path);
                return new AndConstraint<Assertions>(this);
            }
        }
    }
}
