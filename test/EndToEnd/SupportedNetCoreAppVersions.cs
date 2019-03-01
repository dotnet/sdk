using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EndToEnd
{
    public class SupportedNetCoreAppVersions : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator() => Versions.Select(version => new object[] { version }).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public static IEnumerable<string> Versions
        {
            get
            {
                return new[]
                {
                    "1.0",
                    "1.1",
                    "2.0",
                    "2.1",
                    // https://github.com/dotnet/core-sdk/issues/780
                    // "2.2",
                    "3.0"
                };
            }
        }

        
    }

    public class SupportedAspNetCoreVersions : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator() => Versions.Select(version => new object[] { version }).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public static IEnumerable<string> Versions
        {
            get
            {
                return SupportedNetCoreAppVersions.Versions.Except(new List<string>() { "1.0", "1.1", "2.0" });
            }
        }
    }

    public class SupportedAspNetCoreAllVersions : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator() => Versions.Select(version => new object[] { version }).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public static IEnumerable<string> Versions
        {
            get
            {
                return SupportedAspNetCoreVersions.Versions.Where(v => new Version(v).Major < 3);
            }
        }
    }
}
