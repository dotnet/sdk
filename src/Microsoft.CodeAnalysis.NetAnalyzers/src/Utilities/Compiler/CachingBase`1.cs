// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Diagnostics;

namespace Analyzer.Utilities
{
    internal abstract class CachingBase<TEntry>
    {
        private readonly int _alignedSize;
        private TEntry[]? _entries;

        // cache size is always ^2. 
        // items are placed at [hash ^ mask]
        // new item will displace previous one at the same location.
        protected readonly int mask;

        // See docs for createBackingArray on the constructor for why using the non-threadsafe ??= is ok here.
        protected TEntry[] Entries => _entries ??= new TEntry[_alignedSize];

        /// <param name="createBackingArray">Whether or not the backing array should be created immediately, or should
        /// be deferred until the first time that <see cref="Entries"/> is used.  Note: if <paramref
        /// name="createBackingArray"/> is <see langword="false"/> then the array will be created in a non-threadsafe
        /// fashion (effectively different threads might observe a small window of time when different arrays could be
        /// returned.  Derived types should only pass <see langword="false"/> here if that behavior is acceptable for
        /// their use case.</param>
        internal CachingBase(int size, bool createBackingArray = true)
        {
            _alignedSize = AlignSize(size);
            this.mask = _alignedSize - 1;
            _entries = createBackingArray ? new TEntry[_alignedSize] : null;
        }

        private static int AlignSize(int size)
        {
            Debug.Assert(size > 0);

            size--;
            size |= size >> 1;
            size |= size >> 2;
            size |= size >> 4;
            size |= size >> 8;
            size |= size >> 16;
            return size + 1;
        }
    }
}
