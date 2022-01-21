// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core
{
    internal class ExtendedJavascriptEncoder : JavaScriptEncoder
    {
        public override int MaxOutputCharactersPerInputCharacter => UnsafeRelaxedJsonEscaping.MaxOutputCharactersPerInputCharacter;

        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
        {
            ReadOnlySpan<char> input = new ReadOnlySpan<char>(text, textLength);
            int idx = 0;

            while (Rune.DecodeFromUtf16(input.Slice(idx), out Rune result, out int charsConsumed) == OperationStatus.Done)
            {
                if (WillEncode(result.Value))
                {
                    // This character needs to be escaped. Break out.
                    break;
                }
                idx += charsConsumed;
            }

            if (idx == input.Length)
            {
                // None of the characters in the string needs to be escaped.
                return -1;
            }
            return idx;
        }

        public override bool WillEncode(int unicodeScalar)
        {
            if (unicodeScalar == 0x00A0)
            {
                // Don't escape no-break space.
                return false;
            }
            else
            {
                return UnsafeRelaxedJsonEscaping.WillEncode(unicodeScalar);
            }
        }

        public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
        {
            return UnsafeRelaxedJsonEscaping.TryEncodeUnicodeScalar(unicodeScalar, buffer, bufferLength, out numberOfCharactersWritten);
        }
    }
}
