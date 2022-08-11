using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

#nullable enable

namespace Microsoft.DotNet.Cli.Utils;

public static class MSBuildPropertyParser {
    public static IEnumerable<(string key, string value)> ParseProperties(string input) {
        var currentPos = 0;
        string? currentKey = null;
        string? currentValue = null;
        
        (string key, string value) EmitAndReset() {
            var key = currentKey!;
            var value= currentValue!;
            currentKey = null;
            currentValue = null;
            return (key, value);
        }

        char? Peek() => currentPos < input.Length ? input[currentPos] : null;

        bool TryConsume(out char? consumed) {
            if(input.Length > currentPos) {
                consumed = input[currentPos];
                currentPos++;
                return true;
            } else {
                consumed = null;
                return false;
            }
        }
        
        void ParseKey() {
            while (TryConsume(out var c) && c != '=') {
                currentKey += c;
            }
        }

        void ParseQuotedValue() {
            TryConsume(out var leadingQuote); // consume the leading quote, which we know is there
            currentValue += leadingQuote;
            while(TryConsume(out char? c)) {
                if (c == '"') {
                    currentValue += c;
                    // we're done
                    return;
                }
                currentValue += c;
                if (c == '\\' && Peek() == '"')
                {
                    // consume the escaped quote
                    TryConsume(out var c2); 
                    currentValue += c2;
                }
            }
        }

        void ParseUnquotedValue() {
            while(TryConsume(out char? c) && c != ';') {
                currentValue += c;
            }
        }

        void ParseValue() {
            if (Peek() == '"') {
                ParseQuotedValue();
            } else {
                ParseUnquotedValue();
            }
        }

        (string key, string value) ParseKeyValue() {
            ParseKey();
            ParseValue();
            return EmitAndReset();
        }

        bool AtEnd() => currentPos == input.Length;

        while (!(AtEnd())) {
            yield return ParseKeyValue();
            if (Peek() is char c && (c == ';' || c== ',')) {
                TryConsume(out _); // swallow the next semicolon or comma delimiter
            }
        }
    }
}
