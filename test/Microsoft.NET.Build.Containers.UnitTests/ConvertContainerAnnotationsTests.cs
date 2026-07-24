// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Tasks;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ConvertContainerAnnotationsTests
{
    [TestMethod]
    public void RoundTripsArbitraryAnnotationValues()
    {
        ITaskItem[] annotations =
        [
            Create("first:semicolon", "Manifest,Index", "semi;colon:percent% apostrophe' backtick` dollar$ at@ parentheses()"),
            Create("second", "Index", "multiple;:%%'$@`() values"),
        ];
        var encoder = new ConvertContainerAnnotations
        {
            Annotations = annotations,
            BuildEngine = new Mock<IBuildEngine>().Object,
        };
        Assert.IsTrue(encoder.Execute());

        var decoder = new ConvertContainerAnnotations
        {
            SerializedAnnotations = encoder.EncodedAnnotations,
            BuildEngine = new Mock<IBuildEngine>().Object,
        };
        Assert.IsTrue(decoder.Execute());
        Assert.HasCount(2, decoder.DecodedAnnotations);
        for (int i = 0; i < annotations.Length; i++)
        {
            Assert.AreEqual(annotations[i].ItemSpec, decoder.DecodedAnnotations[i].ItemSpec);
            Assert.AreEqual(annotations[i].GetMetadata("Scope"), decoder.DecodedAnnotations[i].GetMetadata("Scope"));
            Assert.AreEqual(annotations[i].GetMetadata("Value"), decoder.DecodedAnnotations[i].GetMetadata("Value"));
        }
    }

    private static TaskItem Create(string identity, string scope, string value)
    {
        TaskItem item = new(identity);
        item.SetMetadata("Scope", scope);
        item.SetMetadata("Value", value);
        return item;
    }
}
