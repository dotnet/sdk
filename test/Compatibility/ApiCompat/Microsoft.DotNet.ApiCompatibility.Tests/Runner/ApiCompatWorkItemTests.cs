// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Runner.Tests
{
    [TestClass]
    public class ApiCompatWorkItemTests
    {
        [TestMethod]
        public void Ctor_ValidArguments_PropertiesSet()
        {
            ApiCompatRunnerOptions apiCompatOptions = new(enableStrictMode: true, isBaselineComparison: true);
            MetadataInformation left = new("A.dll", @"ref\netstandard2.0\A.dll");
            MetadataInformation right = new("A.dll", @"lib\netstandard2.0\A.dll");

            ApiCompatRunnerWorkItem workItem = new(left, apiCompatOptions, right);

            Assert.AreSequenceEqual(new MetadataInformation[] { left }, workItem.Left);
            Assert.AreEqual(apiCompatOptions, workItem.Options);
            Assert.ContainsSingle(workItem.Right);
            Assert.AreSequenceEqual(new MetadataInformation[] { right }, workItem.Right[0]);
        }

        [TestMethod]
        public void Equals_SameWorkItems_IsEqual()
        {
            ApiCompatRunnerOptions apiCompatOptions = new(enableStrictMode: true, isBaselineComparison: true);
            MetadataInformation left = new("A.dll", @"ref\netstandard2.0\A.dll");
            MetadataInformation right = new("A.dll", @"lib\netstandard2.0\A.dll");

            ApiCompatRunnerWorkItem workItem1 = new(left, apiCompatOptions, right);
            ApiCompatRunnerWorkItem workItem2 = new(left, apiCompatOptions, right);

            Assert.IsTrue(workItem1.Equals((object)workItem2));
            Assert.IsTrue(workItem1.Equals(workItem2));
            Assert.IsTrue(workItem1 == workItem2);
        }

        [TestMethod]
        public void Equals_DifferentWorkItems_NotEqual()
        {
            ApiCompatRunnerOptions apiCompatOptions1 = new(enableStrictMode: true, isBaselineComparison: true);
            ApiCompatRunnerOptions apiCompatOptions2 = new(enableStrictMode: false, isBaselineComparison: false);
            MetadataInformation left1 = new("A.dll", @"ref\netstandard2.0\A.dll");
            MetadataInformation left2 = new("A.dll", @"ref\net6.0\A.dll");
            MetadataInformation right1 = new("A.dll", @"lib\netstandard2.0\A.dll");
            MetadataInformation right2 = new("A.dll", @"lib\net6.0\A.dll");

            ApiCompatRunnerWorkItem workItem1 = new(left1, apiCompatOptions1, right1);
            ApiCompatRunnerWorkItem workItem2 = new(left2, apiCompatOptions2, right2);

            Assert.IsFalse(workItem1.Equals((object)workItem2));
            Assert.IsFalse(workItem1.Equals(workItem2));
            Assert.IsTrue(workItem1 != workItem2);
        }

        [TestMethod]
        public void GetHashCode_SameWorkItems_Equal()
        {
            ApiCompatRunnerOptions apiCompatOptions = new(enableStrictMode: true, isBaselineComparison: true);
            MetadataInformation left = new("A.dll", @"ref\netstandard2.0\A.dll");
            MetadataInformation right = new("A.dll", @"lib\netstandard2.0\A.dll");

            ApiCompatRunnerWorkItem workItem1 = new(left, apiCompatOptions, right);
            ApiCompatRunnerWorkItem workItem2 = new(left, apiCompatOptions, right);

            Assert.AreEqual(workItem1.GetHashCode(), workItem2.GetHashCode());
        }

        [TestMethod]
        public void GetHashCode_DifferentWorkItems_NotEqual()
        {
            ApiCompatRunnerOptions apiCompatOptions1 = new(enableStrictMode: true, isBaselineComparison: true);
            ApiCompatRunnerOptions apiCompatOptions2 = new(enableStrictMode: false, isBaselineComparison: false);
            MetadataInformation left1 = new("A.dll", @"ref\netstandard2.0\A.dll");
            MetadataInformation left2 = new("A.dll", @"ref\net6.0\A.dll");
            MetadataInformation right1 = new("A.dll", @"lib\netstandard2.0\A.dll");
            MetadataInformation right2 = new("A.dll", @"lib\net6.0\A.dll");

            ApiCompatRunnerWorkItem workItem1 = new(left1, apiCompatOptions1, right1);
            ApiCompatRunnerWorkItem workItem2 = new(left2, apiCompatOptions2, right2);

            Assert.AreNotEqual(workItem1.GetHashCode(), workItem2.GetHashCode());
        }
    }
}
