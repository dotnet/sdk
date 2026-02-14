// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateDepsFileMultiThreading
    {
        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            new GenerateDepsFile().Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(GenerateDepsFile).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItHasTaskEnvironmentProperty()
        {
            var prop = typeof(GenerateDepsFile).GetProperty("TaskEnvironment");
            prop.Should().NotBeNull();
            prop.PropertyType.Should().Be(typeof(TaskEnvironment));
        }
    }
}
