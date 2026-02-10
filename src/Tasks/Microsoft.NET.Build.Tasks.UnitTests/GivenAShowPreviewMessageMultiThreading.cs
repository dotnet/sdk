// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAShowPreviewMessageMultiThreading
    {
        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(ShowPreviewMessage).Should()
                .BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }
    }
}
