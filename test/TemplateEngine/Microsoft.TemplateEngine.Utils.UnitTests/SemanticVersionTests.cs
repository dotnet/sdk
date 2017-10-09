using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class SemanticVersionTests
    {
        [Theory(DisplayName = nameof(SemanticVersionParse))]
        [InlineData("1", true, 1, 0, 0)]
        [InlineData("1-preview1", true, 1, 0, 0, "preview1")]
        [InlineData("1+build1", true, 1, 0, 0, null, "build1")]
        [InlineData("1-preview1+build1", true, 1, 0, 0, "preview1", "build1")]
        [InlineData("1.2", true, 1, 2, 0)]
        [InlineData("1.2-preview1", true, 1, 2, 0, "preview1")]
        [InlineData("1.2+build1", true, 1, 2, 0, null, "build1")]
        [InlineData("1.2-preview1+build1", true, 1, 2, 0, "preview1", "build1")]
        [InlineData("1.2.3", true, 1, 2, 3)]
        [InlineData("1.2.3-preview1", true, 1, 2, 3, "preview1")]
        [InlineData("1.2.3+build1", true, 1, 2, 3, null, "build1")]
        [InlineData("1.2.3-preview1+build1", true, 1, 2, 3, "preview1", "build1")]
        [InlineData("0.0.0", true)]
        [InlineData("01.0.0", false)]
        [InlineData("0.01.0", false)]
        [InlineData("0.0.01", false)]
        [InlineData("1.0.0-", false)]
        [InlineData("1.0.0+", false)]
        [InlineData("1.0.0-+", false)]
        [InlineData("1.0.0-1.1", true, 1, 0, 0, "1.1")]
        [InlineData("1.0.0-1!1", false)]
        [InlineData("1.0.0-00", false)]
        [InlineData("1.0.0-a.00.b", false)]
        [InlineData("1.0.0-0", true, 1, 0, 0, "0")]
        [InlineData("1.0.0+1.1", true, 1, 0, 0, null, "1.1")]
        [InlineData("1.0.0+1!1", false)]
        [InlineData("1.0.0+00", true, 1, 0, 0, null, "00")]
        [InlineData("1.0.0+0", true, 1, 0, 0, null, "0")]
        public void SemanticVersionParse(string source, bool expectValid, int major = 0, int minor = 0, int patch = 0, string prerelease = null, string metadata = null)
        {
            SemanticVersion ver;

            if (expectValid)
            {
                Assert.True(SemanticVersion.TryParse(source, out ver));
            }
            else
            {
                Assert.False(SemanticVersion.TryParse(source, out ver));
                return;
            }

            Assert.Equal(major, ver.Major);
            Assert.Equal(minor, ver.Minor);
            Assert.Equal(patch, ver.Patch);
            Assert.Equal(prerelease, ver.PrereleaseInfo);
            Assert.Equal(metadata, ver.BuildMetadata);
        }

        [Theory(DisplayName = nameof(SemanticVersionCompareTo))]
        [InlineData("1.0.0", "1.0.0-beta1", true, false)]
        [InlineData("1.0.0", "1.0.0-beta1", true, true)]
        [InlineData("1.0.0", "1.0.0-beta1", null, false)]

        [InlineData("1.0.0-alpha.100", "1.0.0-alpha.99", true, true)]
        [InlineData("1.0.0-alpha.100", "1.0.0-alpha.99", true, false)]
        [InlineData("1.0.0-alpha.100", "1.0.0-alpha.99", null, false)]

        [InlineData("1.0.0-alpha.99.test", "1.0.0-alpha.99", true, true)]
        [InlineData("1.0.0-alpha.99.test", "1.0.0-alpha.99", true, false)]
        [InlineData("1.0.0-alpha.99.test", "1.0.0-alpha.99", null, false)]

        [InlineData("1.0.0-alpha.99", "1.0.0-alpha.test", false, false)]
        [InlineData("1.0.0-alpha.99", "1.0.0-alpha.test", false, true)]
        [InlineData("1.0.0-alpha.99", "1.0.0-alpha.test", null, false)]

        [InlineData("1.0.0-alpha1", "1.0.0-beta1", false, false)]
        [InlineData("1.0.0-alpha1", "1.0.0-beta1", false, true)]
        [InlineData("1.0.0-alpha1", "1.0.0-beta1", null, false)]

        [InlineData("1.0.0-beta1", "1.0.0-alpha1", true, false)]
        [InlineData("1.0.0-beta1", "1.0.0-alpha1", true, true)]
        [InlineData("1.0.0-beta1", "1.0.0-alpha1", null, false)]

        [InlineData("1.0.0-alpha1", "1.0.0-alpha1", true, true)]
        [InlineData("1.0.0-alpha1", "1.0.0-alpha1", false, true)]
        [InlineData("1.0.0-alpha1", "1.0.0-alpha1", null, true)]

        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build42", true, true)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build42", false, true)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build42", null, true)]

        [InlineData("1.0.0-alpha1+build42", "1.0.0-beta1+build42", false, false)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-beta1+build42", false, true)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-beta1+build42", null, false)]

        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build43", false, false)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build43", false, true)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build43", null, false)] // This is expected to differ from the Operators test - as build metada has to be accounted for when performing a comparison sort
        public void SemanticVersionCompareTo(string ver1, string ver2, bool? greater, bool equal)
        {
            Assert.True(SemanticVersion.TryParse(ver1, out SemanticVersion v1));
            Assert.True(SemanticVersion.TryParse(ver2, out SemanticVersion v2));

            if (equal)
            {
                if (!greater.HasValue)
                {
                    Assert.True(v1.CompareTo(v2) == 0, $"Expected {ver1} CompareTo {ver2} to be equal to 0");
                }
                else if (greater.Value)
                {
                    Assert.True(v1.CompareTo(v2) >= 0, $"Expected {ver1} CompareTo {ver2} to be greater than or equal to 0");
                }
                else
                {
                    Assert.True(v1.CompareTo(v2) <= 0, $"Expected {ver1} CompareTo {ver2} to be less than or equal to 0");
                }
            }
            else if (!greater.HasValue)
            {
                Assert.True(v1.CompareTo(v2) != 0, $"Expected {ver1} CompareTo {ver2} to not be equal to 0");
            }
            else if (greater.Value)
            {
                Assert.True(v1.CompareTo(v2) > 0, $"Expected {ver1} CompareTo {ver2} to be greater than 0");
            }
            else
            {
                Assert.True(v1.CompareTo(v2) < 0, $"Expected {ver1} CompareTo {ver2} to be less than 0");
            }
        }

        [Theory(DisplayName = nameof(SemanticVersionOperators))]
        [InlineData("1.0.0", "1.0.0-beta1", true, false)]
        [InlineData("1.0.0", "1.0.0-beta1", true, true)]
        [InlineData("1.0.0", "1.0.0-beta1", null, false)]

        [InlineData("1.0.0-alpha1", "1.0.0-beta1", false, false)]
        [InlineData("1.0.0-alpha1", "1.0.0-beta1", false, true)]
        [InlineData("1.0.0-alpha1", "1.0.0-beta1", null, false)]

        [InlineData("1.0.0-alpha.100", "1.0.0-alpha.99", true, true)]
        [InlineData("1.0.0-alpha.100", "1.0.0-alpha.99", true, false)]
        [InlineData("1.0.0-alpha.100", "1.0.0-alpha.99", null, false)]

        [InlineData("1.0.0-alpha.99.test", "1.0.0-alpha.99", true, true)]
        [InlineData("1.0.0-alpha.99.test", "1.0.0-alpha.99", true, false)]
        [InlineData("1.0.0-alpha.99.test", "1.0.0-alpha.99", null, false)]

        [InlineData("1.0.0-alpha.99", "1.0.0-alpha.test", false, false)]
        [InlineData("1.0.0-alpha.99", "1.0.0-alpha.test", false, true)]
        [InlineData("1.0.0-alpha.99", "1.0.0-alpha.test", null, false)]

        [InlineData("1.0.0-beta1", "1.0.0-alpha1", true, false)]
        [InlineData("1.0.0-beta1", "1.0.0-alpha1", true, true)]
        [InlineData("1.0.0-beta1", "1.0.0-alpha1", null, false)]

        [InlineData("1.0.0-alpha1", "1.0.0-alpha1", true, true)]
        [InlineData("1.0.0-alpha1", "1.0.0-alpha1", false, true)]
        [InlineData("1.0.0-alpha1", "1.0.0-alpha1", null, true)]

        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build42", true, true)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build42", false, true)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build42", null, true)]

        [InlineData("1.0.0-alpha1+build42", "1.0.0-beta1+build42", false, false)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-beta1+build42", false, true)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-beta1+build42", null, false)]

        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build43", true, true)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build43", false, true)]
        [InlineData("1.0.0-alpha1+build42", "1.0.0-alpha1+build43", null, true)] // This is expected to differ from the CompareTo test - as build metada should be ignored
        public void SemanticVersionOperators(string ver1, string ver2, bool? greater, bool equal)
        {
            Assert.True(SemanticVersion.TryParse(ver1, out SemanticVersion v1));
            Assert.True(SemanticVersion.TryParse(ver2, out SemanticVersion v2));

            if (equal)
            {
                if (!greater.HasValue)
                {
                    Assert.True(v1 == v2, $"Expected {ver1} == {ver2}");
                }
                else if (greater.Value)
                {
                    Assert.True(v1 >= v2, $"Expected {ver1} >= {ver2}");
                }
                else
                {
                    Assert.True(v1 <= v2, $"Expected {ver1} <= {ver2}");
                }
            }
            else if (!greater.HasValue)
            {
                Assert.True(v1 != v2, $"Expected {ver1} != {ver2}");
            }
            else if (greater.Value)
            {
                Assert.True(v1 > v2, $"Expected {ver1} > {ver2}");
            }
            else
            {
                Assert.True(v1 < v2, $"Expected {ver1} < {ver2}");
            }
        }

        [Fact(DisplayName = nameof(SemanticVersionObjectEquals))]
        public void SemanticVersionObjectEquals()
        {
            SemanticVersion.TryParse("1.0.0-beta1", out SemanticVersion a);
            SemanticVersion.TryParse("1.0.0-beta1", out SemanticVersion b);
            SemanticVersion.TryParse("1.0.0-beta1+build2", out SemanticVersion c);
            SemanticVersion.TryParse("1.0.0-beta2", out SemanticVersion d);

            Assert.True(a.Equals((object)b));
            Assert.True(a.Equals((object)c));
            Assert.False(a.Equals((object)d));
        }

        [Fact(DisplayName = nameof(SemanticVersionObjectEquals))]
        public void SemanticVersionObjectCompareTo()
        {
            SemanticVersion.TryParse("1.0.0-beta1", out SemanticVersion a);
            SemanticVersion.TryParse("1.0.0-beta1", out SemanticVersion b);
            SemanticVersion.TryParse("1.0.0-beta1+build2", out SemanticVersion c);
            SemanticVersion.TryParse("1.0.0-beta2", out SemanticVersion d);

            Assert.True(a.CompareTo((object)b) == 0);
            Assert.True(a.CompareTo((object)c) < 0);
            Assert.True(a.CompareTo((object)d) < 0);
        }
    }
}
