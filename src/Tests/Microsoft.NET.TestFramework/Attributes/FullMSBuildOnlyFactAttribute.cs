using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class FullMSBuildOnlyFactAttribute : FactAttribute
    {
        public FullMSBuildOnlyFactAttribute()
        {
            this.Skip = "To debug";
        }
    }
}
