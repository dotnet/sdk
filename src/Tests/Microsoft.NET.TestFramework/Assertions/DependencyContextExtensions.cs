using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.NET.TestFramework.Assertions
{
    public static class DependencyContextExtensions
    {
        public static DependencyContextAssertions Should(this DependencyContext dependencyContext)
        {
            return new DependencyContextAssertions(dependencyContext);
        }
    }
}
