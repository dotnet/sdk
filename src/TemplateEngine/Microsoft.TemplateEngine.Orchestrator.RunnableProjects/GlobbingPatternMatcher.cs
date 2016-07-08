using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions.Runner;
using Microsoft.TemplateEngine.Core;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class GlobbingPatternMatcher : IPathMatcher
    {
        private readonly Regex _regex;
        private static readonly TokenTrie Trie;

        private enum GlobbingPatternToken
        {
            Literal = -1,
            AnyNumberOfPathParts = 0,
            OnePathPart = 1,
            Wildcard = 2,
            OpenCharSet = 3,
            CloseCharSet = 4,
            SeparatorChar = 5,
            SeparatorChar2 = 6
        }

        static GlobbingPatternMatcher()
        {
            Trie = new TokenTrie();
            byte[] anyNumberOfPathParts = Encoding.UTF8.GetBytes("**/");
            byte[] onePathPart = Encoding.UTF8.GetBytes("*");
            byte[] wildcard = Encoding.UTF8.GetBytes("?");
            byte[] openCharSet = Encoding.UTF8.GetBytes("[");
            byte[] closeCharSet = Encoding.UTF8.GetBytes("]");
            byte[] separatorChar = Encoding.UTF8.GetBytes("/");
            byte[] separatorChar2 = Encoding.UTF8.GetBytes("\\");
            Trie.AddToken(anyNumberOfPathParts);
            Trie.AddToken(onePathPart);
            Trie.AddToken(wildcard);
            Trie.AddToken(openCharSet);
            Trie.AddToken(closeCharSet);
            Trie.AddToken(separatorChar);
            Trie.AddToken(separatorChar2);
        }

        public string Pattern { get; }

        public GlobbingPatternMatcher(string pattern)
        {
            Pattern = pattern;
            List<Tuple<int, GlobbingPatternToken>> tokens = new List<Tuple<int, GlobbingPatternToken>>();
            byte[] patternBytes = Encoding.UTF8.GetBytes(pattern);
            int currentBufferPosition = 0;

            while (currentBufferPosition != patternBytes.Length)
            {
                int token;
                int originalBufferPosition = currentBufferPosition;
                tokens.Add(!Trie.GetOperation(patternBytes, patternBytes.Length, ref currentBufferPosition, out token)
                    ? Tuple.Create(currentBufferPosition++, GlobbingPatternToken.Literal)
                    : Tuple.Create(originalBufferPosition, (GlobbingPatternToken) token));
            }

            StringBuilder rx = new StringBuilder("^");
            int literalBegin = 0;
            GlobbingPatternToken lastToken = GlobbingPatternToken.AnyNumberOfPathParts;

            for(int i = 0; i < tokens.Count; ++i)
            {
                if (lastToken == GlobbingPatternToken.Literal && tokens[i].Item2 != GlobbingPatternToken.Literal)
                {
                    rx.Append(Regex.Escape(Encoding.UTF8.GetString(patternBytes, literalBegin, tokens[i].Item1 - literalBegin)));
                }

                switch (tokens[i].Item2)
                {
                    case GlobbingPatternToken.Literal:
                        if (lastToken != GlobbingPatternToken.Literal)
                        {
                            literalBegin = tokens[i].Item1;
                        }
                        break;
                    case GlobbingPatternToken.AnyNumberOfPathParts:
                        rx.Append(@"(?:[^\\/]*[\\/])*");
                        break;
                    case GlobbingPatternToken.CloseCharSet:
                        rx.Append("]");
                        break;
                    case GlobbingPatternToken.OpenCharSet:
                        rx.Append("[");
                        break;
                    case GlobbingPatternToken.OnePathPart:
                        rx.Append(@"[^\\/]*");
                        break;
                    case GlobbingPatternToken.Wildcard:
                        rx.Append(@"[^\\/]?");
                        break;
                    case GlobbingPatternToken.SeparatorChar:
                    case GlobbingPatternToken.SeparatorChar2:
                        rx.Append(@"[\\/]");
                        break;
                }

                lastToken = tokens[i].Item2;
            }

            if(lastToken == GlobbingPatternToken.Literal)
            {
                rx.Append(Regex.Escape(Encoding.UTF8.GetString(patternBytes, literalBegin, patternBytes.Length - literalBegin)));
            }

            _regex = new Regex(rx.ToString(), RegexOptions.Compiled);
        }

        public bool IsMatch(string path)
        {
            return _regex.IsMatch(path);
        }
    }
}
