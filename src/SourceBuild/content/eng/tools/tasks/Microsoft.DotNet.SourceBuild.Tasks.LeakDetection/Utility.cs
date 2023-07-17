using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.SourceBuild.Tasks.LeakDetection
{
    internal static class Utility
    {
        internal static string ToHexString(this byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        internal static byte[] ToBytes(this string hex)
        {
            var bytes = new List<byte>();
            for (var i = 0; i < hex.Length; i += 2)
            {
                bytes.Add(Convert.ToByte(hex.Substring(i, 2), 16));
            }
            return bytes.ToArray();
        }

        internal static string MakeRelativePath(string filePath, string relativeTo)
        {
            // Uri.MakeRelativeUri requires the last slash
            if (!relativeTo.EndsWith("/") && !relativeTo.EndsWith("\\"))
            {
                relativeTo += Path.DirectorySeparatorChar;
            }

            var uri = new Uri(filePath);
            var relativeToUri = new Uri(relativeTo);
            return relativeToUri.MakeRelativeUri(uri).ToString();
        }
    }
}
