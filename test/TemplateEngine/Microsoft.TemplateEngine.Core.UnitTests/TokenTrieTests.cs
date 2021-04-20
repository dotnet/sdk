// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.TemplateEngine.Core.Util;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class TokenTrieTests
    {
        [Theory(DisplayName = nameof(VerifyTokenTrieLookArounds))]
        [InlineData("Hello There!", 0, 5, true, null, "Hello", null)]
        [InlineData("Hello There!", 0, 6, true, "Hello", " ", null)]
        [InlineData("Hello There!", 1, 1, false, "Hello", " ", null)]
        [InlineData("Hello There!", 5, 6, true, null, " ", "There!")]
        [InlineData("Hello There!", 0, 6, true, "Hello", " ", "There!")]
        public void VerifyTokenTrieLookArounds(string original, int checkPosition, int expectedPosition, bool success, string after, string value, string before)
        {
            byte[] data = Encoding.UTF8.GetBytes(original);

            TokenTrie t = new TokenTrie();
            TokenConfig builder = (value ?? "").TokenConfigBuilder();

            if (!string.IsNullOrEmpty(after))
            {
                builder = builder.OnlyIfAfter(after);
            }

            if (!string.IsNullOrEmpty(before))
            {
                builder = builder.OnlyIfBefore(before);
            }

            t.AddToken(builder.ToToken(Encoding.UTF8));

            int pos = checkPosition;
            Assert.Equal(success, t.GetOperation(data, data.Length, ref pos, out int token));
            Assert.Equal(expectedPosition, pos);
        }

        [Fact(DisplayName = nameof(VerifyTokenTrieAtBegin))]
        public void VerifyTokenTrieAtBegin()
        {
            byte[] hello = Encoding.UTF8.GetBytes("hello");
            byte[] helloBang = Encoding.UTF8.GetBytes("hello!");
            byte[] hi = Encoding.UTF8.GetBytes("hi");

            TokenTrie t = new TokenTrie();
            t.AddToken(hello);
            t.AddToken(helloBang);
            t.AddToken(hi);

            byte[] source1 = Encoding.UTF8.GetBytes("hello there");
            byte[] source2 = Encoding.UTF8.GetBytes("hello1 there");
            byte[] source3 = Encoding.UTF8.GetBytes("hello! there");
            byte[] source4 = Encoding.UTF8.GetBytes("hi there");
            byte[] source5 = Encoding.UTF8.GetBytes("hi");
            byte[] source6 = Encoding.UTF8.GetBytes("he");

            int token;
            int pos = 0;
            Assert.True(t.GetOperation(source1, source1.Length, ref pos, out token));
            Assert.Equal(0, token);

            pos = 0;
            Assert.True(t.GetOperation(source2, source2.Length, ref pos, out token));
            Assert.Equal(0, token);

            pos = 0;
            Assert.True(t.GetOperation(source3, source3.Length, ref pos, out token));
            Assert.Equal(1, token);

            pos = 0;
            Assert.True(t.GetOperation(source4, source4.Length, ref pos, out token));
            Assert.Equal(2, token);

            pos = 0;
            Assert.True(t.GetOperation(source5, source5.Length, ref pos, out token));
            Assert.Equal(2, token);

            pos = 0;
            Assert.False(t.GetOperation(source6, source6.Length, ref pos, out token));
            Assert.Equal(-1, token);
        }

        [Fact(DisplayName = nameof(VerifyTokenTrieNotEnoughBufferLeft))]
        public void VerifyTokenTrieNotEnoughBufferLeft()
        {
            byte[] hello = Encoding.UTF8.GetBytes("hello");
            byte[] helloBang = Encoding.UTF8.GetBytes("hello!");

            TokenTrie t = new TokenTrie();
            t.AddToken(hello);
            t.AddToken(helloBang);

            byte[] source1 = Encoding.UTF8.GetBytes("hi");
            byte[] source2 = Encoding.UTF8.GetBytes(" hello");

            int token;
            int pos = 0;
            Assert.False(t.GetOperation(source1, source1.Length, ref pos, out token));
            Assert.Equal(-1, token);

            pos = 1;
            Assert.True(t.GetOperation(source2, source2.Length, ref pos, out token));
            Assert.Equal(0, token);

            pos = 2;
            Assert.False(t.GetOperation(source2, source2.Length, ref pos, out token));
            Assert.Equal(-1, token);
        }

        [Fact(DisplayName = nameof(VerifyTokenTrieCombine))]
        public void VerifyTokenTrieCombine()
        {
            byte[] hello = Encoding.UTF8.GetBytes("hello");
            byte[] helloBang = Encoding.UTF8.GetBytes("hello!");
            byte[] hi = Encoding.UTF8.GetBytes("hi");
            byte[] there = Encoding.UTF8.GetBytes("there!");

            TokenTrie t = new TokenTrie();
            t.AddToken(hello);
            t.AddToken(helloBang);

            TokenTrie t2 = new TokenTrie();
            t.AddToken(hi);
            t.AddToken(there);

            TokenTrie combined = new TokenTrie();
            combined.Append(t);
            combined.Append(t2);

            byte[] source1 = Encoding.UTF8.GetBytes("hello there");
            byte[] source2 = Encoding.UTF8.GetBytes("hello! there");
            byte[] source3 = Encoding.UTF8.GetBytes("hi there");
            byte[] source4 = Encoding.UTF8.GetBytes("there!");

            int token;
            int pos = 0;
            Assert.True(t.GetOperation(source1, source1.Length, ref pos, out token));
            Assert.Equal(0, token);

            pos = 0;
            Assert.True(t.GetOperation(source2, source2.Length, ref pos, out token));
            Assert.Equal(1, token);

            pos = 0;
            Assert.True(t.GetOperation(source3, source3.Length, ref pos, out token));
            Assert.Equal(2, token);

            pos = 0;
            Assert.True(t.GetOperation(source4, source4.Length, ref pos, out token));
            Assert.Equal(3, token);
        }
    }
}
