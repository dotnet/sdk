// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Utils
{
    public class CombinedList<T> : IReadOnlyList<T>
    {
        private readonly IReadOnlyList<T> _first;
        private readonly IReadOnlyList<T> _second;

        public CombinedList(IReadOnlyList<T> first, IReadOnlyList<T> second)
        {
            _first = first;
            _second = second;
        }

        public int Count => _first.Count + _second.Count;

        public T this[int index]
        {
            get
            {
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                if (index >= _first.Count)
                {
                    index -= _first.Count;
                }
                else
                {
                    return _first[index];
                }

                if (index >= _second.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _second[index];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private sealed class Enumerator : IEnumerator<T>
        {
            private readonly int _count;
            private readonly IReadOnlyList<T> _items;
            private int _index;

            public Enumerator(IReadOnlyList<T> items)
            {
                _count = items.Count;
                _index = -1;
                _items = items;
            }

            public T Current => _items[_index];

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if ((_index + 1) < _count)
                {
                    ++_index;
                    return true;
                }

                return false;
            }

            public void Reset()
            {
                _index = -1;
            }
        }
    }
}

