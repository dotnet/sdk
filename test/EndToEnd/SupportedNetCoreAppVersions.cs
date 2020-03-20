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
                    "2.2",
                    "3.0",
                    "3.1",
                    "5.0"
                };
            }
        }

        public static IEnumerable<string> TargetFrameworkShortFolderVersion
        {
            get
            {
                var targetFrameworkShortFolderVersion = new List<string>();
                foreach (var v in Versions)
                {
                    if (Version.Parse(v).Major >= 5)
                    {
                        targetFrameworkShortFolderVersion.Add($"net{v}");
                    }
                    else
                    {
                        targetFrameworkShortFolderVersion.Add($"netcoreapp{v}");
                    }
                }

                return targetFrameworkShortFolderVersion;
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
