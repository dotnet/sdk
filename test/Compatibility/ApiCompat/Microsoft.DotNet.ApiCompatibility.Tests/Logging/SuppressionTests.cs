// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Logging.Tests
{
    [TestClass]
    public class SuppressionTests
    {
        public static IEnumerable<object[]> GetEqualData()
        {
            yield return new object[] { new Suppression(string.Empty), new Suppression(string.Empty) { Left = null, Right = null, Target = null } };
            yield return new object[] { new Suppression(string.Empty), new Suppression(string.Empty) { Left = string.Empty, Right = string.Empty, Target = string.Empty } };
            yield return new object[] { new Suppression("PK004"), new Suppression("pk004") };
            yield return new object[] { new Suppression("PK004"), new Suppression(" pk004 ") };
            yield return new object[] { new Suppression("PK004") { Target = "A.B" }, new Suppression(" pk004 ") { Target = "A.b " } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll" }, new Suppression(" pk004 ") { Target = "A.B", Left = "ref/net6.0/mylib.dll" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = false } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = false }, new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = false } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = true }, new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = true } };
        }

        public static IEnumerable<object[]> GetDifferentData()
        {
            yield return new object[] { new Suppression(string.Empty), new Suppression("PK005") };
            yield return new object[] { new Suppression("PK004"), new Suppression("PK005") };
            yield return new object[] { new Suppression("PK004"), new Suppression("PK004") { Target = "A.B()" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B" }, new Suppression("PK004") { Target = "A.B()" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.C" }, new Suppression("PK004") { Target = "A.B()" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B()", Left = "ref/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B()" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B()", Left = "ref/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B()", Left = "lib/net6.0/myLib.dll" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B()", Right = "ref/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B()" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B()", Right = "ref/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B()", Right = "lib/net6.0/myLib.dll" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B()", Left = "ref/net6.0/mylib.dll", Right = "lib/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B()", Left = "ref/netstandard2.0/mylib.dll", Right = "lib/net6.0/myLib.dll" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = true }, new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = false } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = true } };
        }

        [TestMethod]
        [DynamicData(nameof(GetEqualData))]
        public void CheckSuppressionsAreEqual(Suppression suppression, Suppression other)
        {
            Assert.IsTrue(suppression.Equals(other));
            Assert.IsTrue(other.Equals(suppression));
        }

        [TestMethod]
        [DynamicData(nameof(GetDifferentData))]
        public void CheckSuppressionsAreNotEqual(Suppression suppression, Suppression other)
        {
            Assert.IsFalse(suppression.Equals(other));
            Assert.IsFalse(other.Equals(suppression));
        }

        [TestMethod]
        public void CheckSuppressionIsNotEqualWithNull()
        {
            Assert.IsFalse(new Suppression("PK0004").Equals(null));
        }
    }
}
