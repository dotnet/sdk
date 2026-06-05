namespace System.Linq
{
    public static class CustomEnumerableExtensions
    {
#if NET10_0_OR_GREATER
        public static IEnumerable<T> Reverse<T>(T[] array) => Enumerable.Reverse(array);
#else
        public static IEnumerable<T> Reverse<T>(this T[] array) => Enumerable.Reverse(array);
#endif
    }
}
