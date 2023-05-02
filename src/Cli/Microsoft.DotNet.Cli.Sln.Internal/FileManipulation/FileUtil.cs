// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.Cli.Sln.Internal.FileManipulation
{
    static internal class FileUtil
    {
        internal static TextFormatInfo GetTextFormatInfo(string file)
        {
            var info = new TextFormatInfo();

            string newLine = null;
            Encoding encoding;

            using (FileStream fs = File.OpenRead(file))
            {
                byte[] buf = new byte[1024];
                int nread, i;

                if ((nread = fs.Read(buf, 0, buf.Length)) <= 0)
                {
                    return info;
                }

                if (TryParse(buf, nread, out encoding))
                {
                    i = encoding.GetPreamble().Length;
                }
                else
                {
                    encoding = null;
                    i = 0;
                }

                do
                {
                    while (i < nread)
                    {
                        if (buf[i] == '\r')
                        {
                            newLine = "\r\n";
                            break;
                        }
                        else if (buf[i] == '\n')
                        {
                            newLine = "\n";
                            break;
                        }

                        i++;
                    }

                    if (newLine == null)
                    {
                        if ((nread = fs.Read(buf, 0, buf.Length)) <= 0)
                        {
                            newLine = "\n";
                            break;
                        }

                        i = 0;
                    }
                } while (newLine == null);

                info.EndsWithEmptyLine = fs.Seek(-1, SeekOrigin.End) > 0 && fs.ReadByte() == (int)'\n';
                info.NewLine = newLine;
                info.Encoding = encoding;
                return info;
            }
        }

        private static bool TryParse(byte[] buffer, int available, out Encoding encoding)
        {
            if (buffer.Length >= 2)
            {
                for (int i = 0; i < table.Length; i++)
                {
                    bool matched = true;

                    if (available < table[i].GetPreamble().Length)
                    {
                        continue;
                    }

                    for (int j = 0; j < table[i].GetPreamble().Length; j++)
                    {
                        if (buffer[j] != table[i].GetPreamble()[j])
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched)
                    {
                        encoding = table[i];
                        return true;
                    }
                }
            }

            encoding = null;

            return false;
        }

        private static readonly Encoding[] table = new[] {
            Encoding.UTF8,
            Encoding.UTF32,
            Encoding.ASCII,
        };
    }

    internal class TextFormatInfo
    {
        public TextFormatInfo()
        {
            NewLine = Environment.NewLine;
            Encoding = null;
            EndsWithEmptyLine = true;
        }

        public string NewLine { get; set; }
        public Encoding Encoding { get; set; }
        public bool EndsWithEmptyLine { get; set; }
    }
}
