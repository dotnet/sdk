// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    [TestClass]
    public class ArgumentEscaperTests
    {
        [TestMethod]
        [DataRow(new[] { "one", "two", "three" }, "one two three")]
        [DataRow(new[] { "line1\nline2", "word1\tword2" }, "\"line1\nline2\" \"word1\tword2\"")]
        [DataRow(new[] { "with spaces" }, "\"with spaces\"")]
        [DataRow(new[] { @"with\backslash" }, @"with\backslash")]
        [DataRow(new[] { @"""quotedwith\backslash""" }, @"\""quotedwith\backslash\""")]
        [DataRow(new[] { @"C:\Users\" }, @"C:\Users\")]
        [DataRow(new[] { @"C:\Program Files\dotnet\" }, @"""C:\Program Files\dotnet\\""")]
        [DataRow(new[] { @"backslash\""preceedingquote" }, @"backslash\\\""preceedingquote")]
        [DataRow(new[] { @""" hello """ }, @"""\"" hello \""""")]
        public void EscapesArgumentsForProcessStart(string[] args, string expected)
        {
            Assert.AreEqual(expected, ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args));
        }
    }
}
