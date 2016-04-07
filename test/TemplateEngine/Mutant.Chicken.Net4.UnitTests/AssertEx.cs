using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mutant.Chicken.Net4.UnitTests
{
    public static class AssertEx
    {
        public static void AreEqual(string expected, string actual)
        {
            if (expected.Length == actual.Length && string.Equals(expected, actual))
            {
                return;
            }

            int diffPos = 0;

            for (; diffPos < expected.Length && diffPos < actual.Length; ++diffPos)
            {
                if (expected[diffPos] != actual[diffPos])
                {
                    break;
                }
            }

            string leadUp = "^".PadLeft(diffPos + 1, '-');
            Assert.Fail($@"Strings are different:
{expected}

{actual}

{leadUp}
Position: {diffPos}
'{expected[diffPos]}'!='{actual[diffPos]}'");
        }
    }
}
