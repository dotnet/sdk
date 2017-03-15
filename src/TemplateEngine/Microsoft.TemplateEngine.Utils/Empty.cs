using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Utils
{
    public static class Empty<T>
    {
        public static class List
        {
            // ReSharper disable once StaticMemberInGenericType
            public static readonly IReadOnlyList<T> Value = new List<T>();
        }

        public static class Array
        {
            // ReSharper disable once StaticMemberInGenericType
            public static readonly T[] Value = new T[0];
        }
    }
}
