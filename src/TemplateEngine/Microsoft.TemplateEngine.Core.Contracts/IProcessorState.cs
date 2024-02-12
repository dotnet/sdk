// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IProcessorState
    {
        IEngineConfig Config { get; }

        /// <summary>
        /// Gets the buffer containing the chunk of source stream that is being processed.
        /// </summary>
        byte[] CurrentBuffer { get; }

        /// <summary>
        /// Gets the length of useful data in <see cref="CurrentBuffer"/>.
        /// </summary>
        int CurrentBufferLength { get; }

        /// <summary>
        /// Gets the current position in <see cref="CurrentBuffer"/>.
        /// </summary>
        int CurrentBufferPosition { get; }

        /// <summary>
        /// Gets the current position in source stream.
        /// </summary>
        int CurrentSequenceNumber { get; }

        IEncodingConfig EncodingConfig { get; }

        Encoding Encoding { get; }

        /// <summary>
        /// Advances source stream to position <paramref name="bufferPosition"/>.
        /// </summary>
        bool AdvanceBuffer(int bufferPosition);

        /// <summary>
        /// Seeks source stream until <paramref name="match"/> is found.
        /// </summary>
        /// <param name="match">The token to find.</param>
        /// <param name="bufferLength">The length of the buffer after the token is found.</param>
        /// <param name="currentBufferPosition">The position in the buffer after the token is found.</param>
        /// <param name="consumeToken">True if token should be sought through.</param>
        void SeekSourceForwardUntil(ITokenTrie match, ref int bufferLength, ref int currentBufferPosition, bool consumeToken = false);

        /// <summary>
        /// Seeks source stream while <paramref name="match"/> is found.
        /// </summary>
        void SeekSourceForwardWhile(ITokenTrie match, ref int bufferLength, ref int currentBufferPosition);

        /// <summary>
        /// Seeks target stream backwards until <paramref name="match"/> is found.
        /// </summary>
        /// <param name="match">The token to find.</param>
        /// <param name="consumeToken">True if token should be sought through.</param>
        void SeekTargetBackUntil(ITokenTrie match, bool consumeToken = false);

        /// <summary>
        /// Seeks target stream backwards while <paramref name="match"/> is found.
        /// </summary>
        void SeekTargetBackWhile(ITokenTrie match);

        /// <summary>
        /// Writes <paramref name="buffer"/> to target stream.
        /// </summary>
        /// <param name="buffer">The buffer to write.</param>
        /// <param name="offset">The start position in the buffer to write.</param>
        /// <param name="count">The count of bytes to write.</param>
        void WriteToTarget(byte[] buffer, int offset, int count);

        void Inject(Stream staged);
    }
}
