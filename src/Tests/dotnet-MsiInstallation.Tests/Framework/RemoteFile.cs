// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions.Execution;

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    abstract class RemoteFile
    {
        public RemoteFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public abstract bool Exists { get; }

        public abstract string ReadAllText();

        public Assertions Should()
        {
            return new Assertions(this);
        }

        public class Assertions
        {
            RemoteFile _file;

            public Assertions(RemoteFile file)
            {
                _file = file;
            }

            public AndConstraint<Assertions> Exist(string because = "", params object[] reasonArgs)
            {
                Execute.Assertion
                    .ForCondition(_file.Exists)
                    .BecauseOf(because, reasonArgs)
                    .FailWith($"Expected File {_file.Path} to exist, but it does not.");
                return new AndConstraint<Assertions>(this);
            }

            public AndConstraint<Assertions> NotExist(string because = "", params object[] reasonArgs)
            {
                Execute.Assertion
                    .ForCondition(!_file.Exists)
                    .BecauseOf(because, reasonArgs)
                    .FailWith($"Expected File {_file.Path} to not exist, but it does.");
                return new AndConstraint<Assertions>(this);
            }
        }
    }
}
