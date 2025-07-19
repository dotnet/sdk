// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Logging.Tests
{
    public class SuppressionTests
    {
        public static IEnumerable<object[]> GetEqualData()
        {
            yield return new object[] { new Suppression(string.Empty, string.Empty), new Suppression(string.Empty, string.Empty, target:null, left: null, right: null) };
            yield return new object[] { new Suppression(string.Empty, string.Empty), new Suppression(string.Empty, string.Empty, target: string.Empty, left: string.Empty, right: string.Empty) };
            yield return new object[] { new Suppression("PK004", string.Empty), new Suppression("pk004", string.Empty) };
            yield return new object[] { new Suppression("PK004", string.Empty), new Suppression(" pk004 ", string.Empty) };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B"), new Suppression(" pk004 ", string.Empty, "A.b ") };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll"), new Suppression(" pk004 ", string.Empty, "A.B", "ref/net6.0/mylib.dll") };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll"), new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: false) };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: false), new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: false) };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: true), new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: true) };
        }

        public static IEnumerable<object[]> GetDifferentData()
        {
            yield return new object[] { new Suppression(string.Empty, string.Empty), new Suppression("PK005", string.Empty) };
            yield return new object[] { new Suppression("PK004", string.Empty), new Suppression("PK005", string.Empty) };
            yield return new object[] { new Suppression("PK004", string.Empty), new Suppression("PK004", string.Empty, "A.B()") };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B"), new Suppression("PK004", string.Empty, "A.B()") };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.C"), new Suppression("PK004", string.Empty, "A.B()") };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B()", "ref/net6.0/myLib.dll"), new Suppression("PK004", string.Empty, "A.B()") };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B()", "ref/net6.0/myLib.dll"), new Suppression("PK004", string.Empty, "A.B()", "lib/net6.0/myLib.dll") };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B()", "ref/net6.0/myLib.dll"), new Suppression("PK004", string.Empty, "A.B()") };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B()", "ref/net6.0/myLib.dll"), new Suppression("PK004", string.Empty, "A.B()", "lib/net6.0/myLib.dll") };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B()", "ref/net6.0/mylib.dll", "lib/net6.0/myLib.dll"), new Suppression("PK004", string.Empty, "A.B()", "ref/netstandard2.0/mylib.dll", "lib/net6.0/myLib.dll") };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: true), new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: false) };
            yield return new object[] { new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll"), new Suppression("PK004", string.Empty, "A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: true) };
        }

        [Theory]
        [MemberData(nameof(GetEqualData))]
        public void CheckSuppressionsAreEqual(Suppression suppression, Suppression other)
        {
            Assert.True(suppression.Equals(other));
            Assert.True(other.Equals(suppression));
        }

        [Theory]
        [MemberData(nameof(GetDifferentData))]
        public void CheckSuppressionsAreNotEqual(Suppression suppression, Suppression other)
        {
            Assert.False(suppression.Equals(other));
            Assert.False(other.Equals(suppression));
        }

        [Fact]
        public void CheckSuppressionIsNotEqualWithNull()
        {
            Assert.False(new Suppression("PK0004", string.Empty).Equals(null));
        }
    }
}
