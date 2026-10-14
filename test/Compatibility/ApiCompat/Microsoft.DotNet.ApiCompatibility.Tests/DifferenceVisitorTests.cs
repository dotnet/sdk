// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Mapping;
using Moq;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    [TestClass]
    public class DifferenceVisitorTests
    {
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void DuplicateDifferencesRetainError(bool informationalFirst)
        {
            CompatDifference error = CompatDifference.CreateWithDefaultMetadata(
                DiagnosticIds.TypeMustExist,
                "Duplicate",
                DifferenceType.Removed,
                "T:Foo");
            CompatDifference informational = new(
                error.Left,
                error.Right,
                error.DiagnosticId,
                error.Message,
                error.Type,
                error.ReferenceId,
                DifferenceSeverity.Informational);
            CompatDifference[] differences = informationalFirst ? [informational, error] : [error, informational];
            Mock<IMemberMapper> mapper = new();
            mapper.Setup(m => m.GetDifferences()).Returns(differences);
            DifferenceVisitor visitor = new();

            visitor.Visit(mapper.Object);

            CompatDifference difference = Assert.ContainsSingle(visitor.CompatDifferences);
            Assert.AreEqual(DifferenceSeverity.Error, difference.Severity);
        }
    }
}
