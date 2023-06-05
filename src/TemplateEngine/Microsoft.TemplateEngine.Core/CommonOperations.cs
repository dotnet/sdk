// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core
{
    public static class CommonOperations
    {
        public static void WhitespaceHandler(this IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, bool wholeLine = false, bool trim = false, bool trimForward = false, bool trimBackward = false)
        {
            if (wholeLine)
            {
                processor.ConsumeWholeLine(ref bufferLength, ref currentBufferPosition);
                return;
            }

            if (trim)
            {
                trimForward = true;
                trimBackward = true;
            }

            processor.TrimWhitespace(trimForward, trimBackward, ref bufferLength, ref currentBufferPosition);
        }

        public static void ConsumeWholeLine(this IProcessorState processor, ref int bufferLength, ref int currentBufferPosition)
        {
            processor.SeekTargetBackWhile(processor.EncodingConfig.Whitespace);
            processor.SeekSourceForwardUntil(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition, consumeToken: true);
        }

        public static void TrimWhitespace(this IProcessorState processor, bool forward, bool backward, ref int bufferLength, ref int currentBufferPosition)
        {
            if (backward)
            {
                processor.SeekTargetBackWhile(processor.EncodingConfig.Whitespace);
            }

            if (forward)
            {
                processor.SeekSourceForwardWhile(processor.EncodingConfig.Whitespace, ref bufferLength, ref currentBufferPosition);
                //Consume the trailing line end if possible
                _ = processor.EncodingConfig.LineEndings.GetOperation(processor.CurrentBuffer, bufferLength, ref currentBufferPosition, out _);
            }
        }
    }
}
