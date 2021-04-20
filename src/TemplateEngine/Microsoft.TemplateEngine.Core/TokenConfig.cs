// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core
{
    public class TokenConfig : ITokenConfig
    {
        public string After { get; }

        public string Before { get; }

        public string Value { get; }

        private TokenConfig(string after, string value, string before)
        {
            After = after;
            Value = value;
            Before = before;
        }

        public static TokenConfig FromValue(string value)
        {
            return new TokenConfig(null, value, null);
        }

        public TokenConfig OnlyIfAfter(string prefix)
        {
            return new TokenConfig(prefix, Value, Before);
        }

        public TokenConfig OnlyIfBefore(string suffix)
        {
            return new TokenConfig(After, Value, suffix);
        }

        public static IToken LiteralToken(byte[] data, int start = 0, int end = -1)
        {
            int realEnd = end != -1 ? end : (data.Length - start - 1);
            return new Token(data, start, realEnd);
        }

        public IToken ToToken(Encoding encoding)
        {
            byte[] pre = string.IsNullOrEmpty(After) ? Empty<byte>.Array.Value : encoding.GetBytes(After);
            byte[] post = string.IsNullOrEmpty(Before) ? Empty<byte>.Array.Value : encoding.GetBytes(Before);
            byte[] core = string.IsNullOrEmpty(Value) ? Empty<byte>.Array.Value : encoding.GetBytes(Value);

            byte[] buffer = new byte[pre.Length + core.Length + post.Length];

            if (pre != Empty<byte>.Array.Value)
            {
                Buffer.BlockCopy(pre, 0, buffer, 0, pre.Length);
            }

            if (core != Empty<byte>.Array.Value)
            {
                Buffer.BlockCopy(core, 0, buffer, pre.Length, core.Length);
            }

            if (post != Empty<byte>.Array.Value)
            {
                Buffer.BlockCopy(post, 0, buffer, pre.Length + core.Length, post.Length);
            }

            return new Token(buffer, pre.Length, buffer.Length - post.Length - 1);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ITokenConfig);
        }

        public override int GetHashCode()
        {
            return (Before?.GetHashCode() ?? 0) ^ (After?.GetHashCode() ?? 0) ^ (Value?.GetHashCode() ?? 0);
        }

        public bool Equals(ITokenConfig other)
        {
            return other != null && string.Equals(other.Before, Before, StringComparison.Ordinal) && string.Equals(other.After, After, StringComparison.Ordinal) && string.Equals(other.Value, Value, StringComparison.Ordinal);
        }

        private class Token : IToken
        {
            public byte[] Value { get; }

            public int Start { get; }

            public int End { get; }

            public int Length { get; }

            public Token(byte[] value, int start, int end)
            {
                Value = value;
                Start = start;
                End = end;
                Length = End - Start + 1;
            }
        }
    }
}
