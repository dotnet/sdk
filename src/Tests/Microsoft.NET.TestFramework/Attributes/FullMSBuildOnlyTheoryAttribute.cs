using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class FullMSBuildOnlyTheoryAttribute : TheoryAttribute
    {
        public FullMSBuildOnlyTheoryAttribute()
        {
            this.Skip = "To debug";
        }
    }
}
