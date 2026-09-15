// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    [TestClass]
    public class CompatDifferenceTests
    {
        public static IEnumerable<object[]> CompatDifferencesData =>
            new object[][]
            {
                new object[]
                {
                    MetadataInformation.DefaultLeft, MetadataInformation.DefaultRight, DiagnosticIds.TypeMustExist, "Type Foo exists on left but not on right", "T:Foo", DifferenceType.Added,
                },
                new object[]
                {
                    MetadataInformation.DefaultLeft, MetadataInformation.DefaultRight, DiagnosticIds.MemberMustExist, "Member Foo.Blah exists on right but not on left", "M:Foo.Blah", DifferenceType.Removed,
                },
                new object[]
                {
                    MetadataInformation.DefaultLeft, MetadataInformation.DefaultRight, "CP320", string.Empty, "F:Blah.Blah", DifferenceType.Changed
                }
            };

        [TestMethod]
        [DynamicData(nameof(CompatDifferencesData))]
        public void PropertiesAreCorrect(MetadataInformation left, MetadataInformation right, string diagId, string message, string memberId, DifferenceType type)
        {
            CompatDifference difference = new(left, right, diagId, message, type, memberId);
            Assert.AreEqual(left, difference.Left);
            Assert.AreEqual(right, difference.Right);
            Assert.AreEqual(diagId, difference.DiagnosticId);
            Assert.AreEqual(message, difference.Message);
            Assert.AreEqual(memberId, difference.ReferenceId);
            Assert.AreEqual(type, difference.Type);

            Assert.AreEqual($"{diagId} : {message}", difference.ToString());
        }

        [TestMethod]
        public void IsEquatableWorksAsExpected()
        {
            CompatDifference difference = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:Foo");
            CompatDifference otherEqual = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:Foo");
            CompatDifference differentDiagId = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Removed, "T:Foo");
            CompatDifference differentType = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:Foo");
            CompatDifference differentMemberId = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:FooBar");
            CompatDifference differentMessage = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, "Hello", DifferenceType.Removed, "T:Foo");

            Assert.IsFalse(difference.Equals(null));
            Assert.IsTrue(difference.Equals(otherEqual));
            Assert.IsTrue(difference.Equals((object)otherEqual));
            Assert.IsFalse(difference.Equals(differentDiagId));
            Assert.IsFalse(difference.Equals(differentType));
            Assert.IsFalse(difference.Equals(differentMemberId));
            Assert.IsTrue(difference.Equals(differentMessage));
        }
    }
}
