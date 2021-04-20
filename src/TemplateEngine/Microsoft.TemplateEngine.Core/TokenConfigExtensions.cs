// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core
{
    public static class TokenConfigExtensions
    {
        public static ITokenConfig TokenConfig(this string s)
        {
            return Core.TokenConfig.FromValue(s);
        }

        public static TokenConfig TokenConfigBuilder(this string s)
        {
            return Core.TokenConfig.FromValue(s);
        }

        public static IToken LiteralToken(this byte[] b, int start = 0, int end = -1)
        {
            return Core.TokenConfig.LiteralToken(b, start, end);
        }

        public static IToken Token(this string s, Encoding e)
        {
            return s.TokenConfig().ToToken(e);
        }

        public static IReadOnlyList<ITokenConfig> TokenConfigs(this IEnumerable<string> s)
        {
            List<ITokenConfig> configs = new List<ITokenConfig>();

            foreach(string x in s)
            {
                configs.Add(x.TokenConfig());
            }

            return configs;
        }
    }
}