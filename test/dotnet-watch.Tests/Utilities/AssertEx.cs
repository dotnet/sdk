// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections;
using System.Text.RegularExpressions;
using Xunit.Sdk;

namespace Microsoft.DotNet.Watch.UnitTests
{
    internal static class AssertEx
    {
        private class AssertEqualityComparer<T> : IEqualityComparer<T>
        {
            public static readonly IEqualityComparer<T> Instance = new AssertEqualityComparer<T>();

            private static bool CanBeNull()
            {
                var type = typeof(T);
                return !type.GetType().IsValueType ||
                    (type.GetType().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
            }

            public static bool IsNull(T @object)
            {
                if (!CanBeNull())
                {
                    return false;
                }

                return object.Equals(@object, default(T));
            }

            public static bool Equals(T left, T right)
            {
                return Instance.Equals(left, right);
            }

            bool IEqualityComparer<T>.Equals(T x, T y)
            {
                if (CanBeNull())
                {
                    if (object.Equals(x, default(T)))
                    {
                        return object.Equals(y, default(T));
                    }

                    if (object.Equals(y, default(T)))
                    {
                        return false;
                    }
                }

                if (x.GetType() != y.GetType())
                {
                    return false;
                }

                if (x is IEquatable<T> equatable)
                {
                    return equatable.Equals(y);
                }

                if (x is IComparable<T> comparableT)
                {
                    return comparableT.CompareTo(y) == 0;
                }

                if (x is IComparable comparable)
                {
                    return comparable.CompareTo(y) == 0;
                }

                var enumerableX = x as IEnumerable;
                var enumerableY = y as IEnumerable;

                if (enumerableX != null && enumerableY != null)
                {
                    var enumeratorX = enumerableX.GetEnumerator();
                    var enumeratorY = enumerableY.GetEnumerator();

                    while (true)
                    {
                        bool hasNextX = enumeratorX.MoveNext();
                        bool hasNextY = enumeratorY.MoveNext();

                        if (!hasNextX || !hasNextY)
                        {
                            return hasNextX == hasNextY;
                        }

                        if (!Equals(enumeratorX.Current, enumeratorY.Current))
                        {
                            return false;
                        }
                    }
                }

                return object.Equals(x, y);
            }

            int IEqualityComparer<T>.GetHashCode(T obj)
            {
                throw new NotImplementedException();
            }
        }

        public static void Equal<T>(T expected, T actual, IEqualityComparer<T> comparer = null, string message = null)
        {
            if (ReferenceEquals(expected, actual))
            {
                return;
            }

            if (expected == null)
            {
                Fail("expected was null, but actual wasn't" + Environment.NewLine + message);
            }
            else if (actual == null)
            {
                Fail("actual was null, but expected wasn't" + Environment.NewLine + message);
            }
            else if (!(comparer ?? AssertEqualityComparer<T>.Instance).Equals(expected, actual))
            {
                var expectedAndActual = $"""
                    Expected:
                    {expected}
                    Actual:
                    {actual}
                    """;

                Fail(message + Environment.NewLine + expectedAndActual);
            }
        }

        public static void Equal<T>(
            IEnumerable<T> expected,
            IEnumerable<T> actual,
            IEqualityComparer<T> comparer = null,
            string message = null,
            string itemSeparator = null,
            Func<T, string> itemInspector = null)
            => SequenceEqual(expected, actual, comparer, message, itemSeparator, itemInspector);

        public static void SequenceEqual<T>(
            IEnumerable<T> expected,
            IEnumerable<T> actual,
            IEqualityComparer<T> comparer = null,
            string message = null,
            string itemSeparator = null,
            Func<T, string> itemInspector = null)
        {
            if (expected == null)
            {
                Assert.Null(actual);
            }
            else
            {
                Assert.NotNull(actual);
            }

            if (!expected.SequenceEqual(actual, comparer))
            {
                Fail(GetAssertMessage(expected, actual, message, itemInspector, itemSeparator));
            }
        }

        private static string GetAssertMessage<T>(
            IEnumerable<T> expected,
            IEnumerable<T> actual,
            string prefix = null,
            Func<T, string> itemInspector = null,
            string itemSeparator = null)
        {
            itemInspector ??= (typeof(T) == typeof(byte)) ? b => $"0x{b:X2}" : new Func<T, string>(obj => (obj != null) ? obj.ToString() : "<null>");
            itemSeparator ??= (typeof(T) == typeof(byte)) ? ", " : "," + Environment.NewLine;

            var expectedString = string.Join(itemSeparator, expected.Take(10).Select(itemInspector));
            var actualString = string.Join(itemSeparator, actual.Select(itemInspector));

            var message = new StringBuilder();

            if (!string.IsNullOrEmpty(prefix))
            {
                message.AppendLine(prefix);
                message.AppendLine();
            }

            message.AppendLine("Expected:");
            message.AppendLine(expectedString);
            if (expected.Count() > 10)
            {
                message.AppendLine("... truncated ...");
            }

            message.AppendLine("Actual:");
            message.AppendLine(actualString);

            return message.ToString();
        }

        public static void Empty(string actual, string message = null)
            => Equal("", actual, message: message);

        public static void Fail(string message)
            => throw new XunitException(message);

        public static void EqualFileList(string root, IEnumerable<string> expectedFiles, IEnumerable<string> actualFiles)
        {
            var expected = expectedFiles.Select(p => Path.Combine(root, p));
            EqualFileList(expected, actualFiles);
        }

        public static void EqualFileList(IEnumerable<string> expectedFiles, IEnumerable<string> actualFiles)
        {
            static string normalize(string p) => p.Replace('\\', '/');
            var expected = new HashSet<string>(expectedFiles.Select(normalize));
            var actual = new HashSet<string>(actualFiles.Where(p => !string.IsNullOrEmpty(p)).Select(normalize));
            if (!expected.SetEquals(actual))
            {
                throw NotEqualException.ForEqualValues(
                    expected: "\n" + string.Join("\n", expected.OrderBy(p => p)),
                    actual: "\n" + string.Join("\n", actual.OrderBy(p => p)),
                    banner: "File sets should be equal");
            }
        }

        public static void Contains(string expected, IEnumerable<string> items)
        {
            if (items.Any(item => item.Contains(expected)))
            {
                return;
            }

            var message = new StringBuilder();
            message.AppendLine($"Expected output not found:");
            message.AppendLine(expected);
            message.AppendLine();
            message.AppendLine("Actual output:");

            foreach (var item in items)
            {
                message.AppendLine($"'{item}'");
            }

            Fail(message.ToString());
        }

        public static void ContainsRegex(string pattern, IEnumerable<string> items)
        {
            var regex = new Regex(pattern, RegexOptions.Compiled);

            if (items.Any(item => regex.IsMatch(item)))
            {
                return;
            }

            var message = new StringBuilder();
            message.AppendLine($"Pattern '{pattern}' not found in:");

            foreach (var item in items)
            {
                message.AppendLine($"'{item}'");
            }

            Fail(message.ToString());
        }

        public static void DoesNotContain(string expected, IEnumerable<string> items)
            => Assert.DoesNotContain(expected, items);
    }
}
