// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.TemplateEngine.Core.Expressions.Shared
{
    //TODO: When the ability to have more descriptive returns is available, update this with
    //  bounds checking
    public static class XmlStyleConverters
    {
        public static string XmlDecode(string arg)
        {
            List<char> output = new List<char>();

            for (int i = 0; i < arg.Length; ++i)
            {
                //Not entity mode
                if (arg[i] != '&')
                {
                    output.Add(arg[i]);
                    continue;
                }

                ++i;
                //Entity mode, decimal or hex
                if (arg[i] == '#')
                {
                    ++i;

                    //Hex entity mode
                    if (arg[i] == 'x')
                    {
                        string hex = arg.Substring(i + 1, 4);
                        char c = (char)short.Parse(hex.TrimStart('0'), NumberStyles.HexNumber);
                        output.Add(c);
                        i += 5; //x, 4 digits, semicolon (consumed by the loop bound)
                    }
                    else
                    {
                        string dec = arg.Substring(i, 4);
                        char c = (char)short.Parse(dec.TrimStart('0'), NumberStyles.Integer);
                        output.Add(c);
                        i += 4; //4 digits, semicolon (consumed by the loop bound)
                    }
                }
                else
                {
                    switch (arg[i])
                    {
                        case 'q':
                            switch (arg[i + 1])
                            {
                                case 'u':
                                    switch (arg[i + 2])
                                    {
                                        case 'o':
                                            switch (arg[i + 3])
                                            {
                                                case 't':
                                                    switch (arg[i + 4])
                                                    {
                                                        case ';':
                                                            output.Add('"');
                                                            i += 4;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'a':
                            switch (arg[i + 1])
                            {
                                case 'm':
                                    switch (arg[i + 2])
                                    {
                                        case 'p':
                                            switch (arg[i + 3])
                                            {
                                                case ';':
                                                    output.Add('&');
                                                    i += 3;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'p':
                                    switch (arg[i + 2])
                                    {
                                        case 'o':
                                            switch (arg[i + 3])
                                            {
                                                case 's':
                                                    switch (arg[i + 4])
                                                    {
                                                        case ';':
                                                            output.Add('\'');
                                                            i += 4;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'l':
                            switch (arg[i + 1])
                            {
                                case 't':
                                    switch (arg[i + 2])
                                    {
                                        case ';':
                                            output.Add('<');
                                            i += 2;
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'g':
                            switch (arg[i + 1])
                            {
                                case 't':
                                    switch (arg[i + 2])
                                    {
                                        case ';':
                                            output.Add('>');
                                            i += 2;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                }
            }

            string s = new string(output.ToArray());
            return s;
        }

        public static string XmlEncode(string arg)
        {
            return arg.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }
}
