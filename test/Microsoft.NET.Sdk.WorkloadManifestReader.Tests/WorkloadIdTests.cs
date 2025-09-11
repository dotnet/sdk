// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader.Tests
{
    public class WorkloadIdTests
    {
        [Theory]
        [InlineData("wasm-tools", "wasm.tools")]
        [InlineData("something_something", "something.something")]
        public void ItCanCreateSafeIds(string workloadId, string expectedSafeId)
        {
            var id = new WorkloadId(workloadId);
            Assert.Equal(expectedSafeId, id.ToSafeId());
        }

        [Theory]
        [InlineData("wasm-tools", "Microsoft.NET.Component.wasm.tools")]
        [InlineData("microsoft-android-runtime", "Microsoft.NET.Component.android.runtime")]
        public void ItCanCreateSafeIdsWithVisualStudioStudioPrefix(string workloadId, string expectedSafeId)
        {
            var id = new WorkloadId(workloadId);
            Assert.Equal(expectedSafeId, id.ToSafeId(includeVisualStudioPrefix: true));
        }
    }
}
