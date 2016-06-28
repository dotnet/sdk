using System.Text;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class SimpleTrieTests
    {
        [Fact]
        public void VerifySimpleTrieAtBegin()
        {
            byte[] hello = Encoding.UTF8.GetBytes("hello");
            byte[] helloBang = Encoding.UTF8.GetBytes("hello!");
            byte[] hi = Encoding.UTF8.GetBytes("hi");

            SimpleTrie t = new SimpleTrie();
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

        [Fact]
        public void VerifySimpleTrieNotEnoughBufferLeft()
        {
            byte[] hello = Encoding.UTF8.GetBytes("hello");
            byte[] helloBang = Encoding.UTF8.GetBytes("hello!");

            SimpleTrie t = new SimpleTrie();
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

        [Fact]
        public void VerifySimpleTrieCombine()
        {
            byte[] hello = Encoding.UTF8.GetBytes("hello");
            byte[] helloBang = Encoding.UTF8.GetBytes("hello!");
            byte[] hi = Encoding.UTF8.GetBytes("hi");
            byte[] there = Encoding.UTF8.GetBytes("there!");

            SimpleTrie t = new SimpleTrie();
            t.AddToken(hello);
            t.AddToken(helloBang);

            SimpleTrie t2 = new SimpleTrie();
            t.AddToken(hi);
            t.AddToken(there);

            SimpleTrie combined = new SimpleTrie();
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
