// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.Utils
{
    public class Glob
    {
        private readonly IReadOnlyList<IMatcher> _matchers;
        private readonly bool _negate;
        private readonly bool _isNameOnlyMatch;

        private Glob(bool negate, IReadOnlyList<IMatcher> matchers, bool canBeNameOnlyMatch)
        {
            _negate = negate;
            _matchers = matchers;
            _isNameOnlyMatch = canBeNameOnlyMatch && !_matchers.Any(x => x is PathMatcher || x is ExactPathMatcher || (x as LiteralMatcher)?.Char?.FirstOrDefault() == '/');
        }

        private interface IMatcher
        {
            int MinConsume { get; }

            bool ProducesCheckpoint { get; }

            bool CanConsume(string test, int startAt, out int endPosition);
        }

        public static Glob Parse(string pattern, bool canBeNameOnlyMatch = true)
        {
            List<IMatcher> matchers = new List<IMatcher>();

            int start = 0;
            bool negate = false;

            if (pattern.Length > 0 && pattern[0] == '!')
            {
                negate = true;
                start = 1;
            }

            for (int i = start; i < pattern.Length; ++i)
            {
                switch (pattern[i])
                {
                    case '\\':
                        if (i < pattern.Length - 1)
                        {
                            switch (pattern[i + 1])
                            {
                                case '[':
                                case ' ':
                                    matchers.Add(new LiteralMatcher(pattern[i + 1]));
                                    ++i;
                                    break;
                                default:
                                    matchers.Add(new LiteralMatcher('\\'));
                                    break;
                            }
                        }
                        else
                        {
                            matchers.Add(new LiteralMatcher('\\'));
                        }
                        break;
                    case '[':
                        List<char> values = new List<char>();
                        for (; i < pattern.Length; ++i)
                        {
                            if (pattern[i] == '\\')
                            {
                                if (i < pattern.Length - 1)
                                {
                                    switch (pattern[i + 1])
                                    {
                                        case ']':
                                        case '[':
                                        case ' ':
                                            ++i;
                                            values.Add(pattern[i]);
                                            continue;
                                    }
                                }
                            }

                            if (pattern[i] == ']')
                            {
                                break;
                            }

                            values.Add(pattern[i]);
                        }
                        matchers.Add(new LiteralMatcher(values));
                        break;
                    case '*':
                        if (pattern.Length > i + 1 && pattern[i + 1] == '*')
                        {
                            if (pattern.Length > i + 2 && pattern[i + 2] == '/')
                            {
                                matchers.Add(new ExactPathMatcher());
                                i += 2;
                            }
                            else
                            {
                                matchers.Add(new PathMatcher());
                                ++i;
                            }
                        }
                        else
                        {
                            matchers.Add(new NameMatcher());
                        }
                        break;
                    default:
                        matchers.Add(new LiteralMatcher(pattern[i]));
                        break;
                }
            }

            return new Glob(negate, matchers, canBeNameOnlyMatch);
        }

        public bool IsMatch(string test)
        {
            return _negate ^ IsMatchCore(test);
        }

        private bool IsMatchCore(string test)
        {
            Stack<Checkpoint> checkpoints = new Stack<Checkpoint>();

            int currentMatcher = 0;
            int i = 0;

            //See if we can just do a name match
            if (_isNameOnlyMatch)
            {
                i = test.LastIndexOf('/') + 1;
            }

            while (i < test.Length)
            {
                IMatcher matcher = _matchers[currentMatcher];

                //If the matcher has a minimum zero width, isn't the last matcher and produces a checkpoint,
                //  we don't need to actually test for a match at this stage
                //Otherwise, test whether the matcher can consume starting at the current position
                if (currentMatcher < _matchers.Count - 1 && matcher.ProducesCheckpoint && matcher.MinConsume == 0 || matcher.CanConsume(test, i, out i))
                {
                    //If the current matcher isn't that last one and it produces a checkpoint, stash
                    //  the checkpoint info
                    if (currentMatcher < _matchers.Count - 1 && matcher.ProducesCheckpoint)
                    {
                        checkpoints.Push(new Checkpoint(matcher, currentMatcher + 1, i));
                    }

                    ++currentMatcher;

                    if (currentMatcher < _matchers.Count)
                    {
                        continue;
                    }

                    if (matcher.ProducesCheckpoint)
                    {
                        if (i < test.Length)
                        {
                            --currentMatcher;
                        }

                        continue;
                    }
                }

                if (currentMatcher == _matchers.Count && i == test.Length)
                {
                    return true;
                }

                //If the match failed or we ran out of matchers, try to revert to an earlier checkpoint to re-evaluate
                //If we've got checkpoints left, back up one and see if it's usable
                while (checkpoints.Count > 0)
                {
                    Checkpoint checkpoint = checkpoints.Pop();

                    //If the matcher is usable...
                    //  restore the checkpoint
                    //  advance the string position
                    //  reset the current matcher to the one that followed the checkpoint
                    if (checkpoint.Matcher.CanConsume(test, checkpoint.StringPosition, out i))
                    {
                        checkpoint.StringPosition = i;
                        currentMatcher = checkpoint.NextMatcherIndex;
                        checkpoints.Push(checkpoint);
                        break;
                    }
                }

                //If we ran out of checkpoints, the match has failed
                if (checkpoints.Count == 0)
                {
                    return false;
                }
            }

            return i == test.Length && currentMatcher == _matchers.Count;
        }

        private class Checkpoint
        {
            public Checkpoint(IMatcher matcher, int nextMatcherIndex, int stringPosition)
            {
                Matcher = matcher;
                NextMatcherIndex = nextMatcherIndex;
                StringPosition = stringPosition;
            }

            public IMatcher Matcher { get; }

            public int NextMatcherIndex { get; }

            public int StringPosition { get; set; }
        }

        private class ExactPathMatcher : IMatcher
        {
            public int MinConsume => 0;

            public bool ProducesCheckpoint => true;

            public bool CanConsume(string test, int startAt, out int endPosition)
            {
                int nextSlash = test.IndexOf("/", startAt, StringComparison.Ordinal);

                if (nextSlash > -1)
                {
                    endPosition = nextSlash + 1;
                    return true;
                }

                endPosition = startAt;
                return false;
            }

            public override string ToString()
            {
                return "**/";
            }
        }

        private class LiteralMatcher : IMatcher
        {
            public LiteralMatcher(IEnumerable<char> c)
            {
                Char = new HashSet<char>(c);
            }

            public LiteralMatcher(char c)
            {
                Char = new HashSet<char> { c };
            }

            public ISet<char> Char { get; }

            public int MinConsume => 1;

            public bool ProducesCheckpoint => false;

            public bool CanConsume(string test, int startAt, out int endPosition)
            {
                if (Char.Contains(test[startAt]))
                {
                    endPosition = startAt + 1;
                    return true;
                }

                endPosition = startAt;
                return false;
            }

            public override string ToString()
            {
                return Char.ToString();
            }
        }

        private class NameMatcher : IMatcher
        {
            public int MinConsume => 0;

            public bool ProducesCheckpoint => true;

            public bool CanConsume(string test, int startAt, out int endPosition)
            {
                if (test[startAt] != '/')
                {
                    endPosition = startAt + 1;
                    return true;
                }

                endPosition = startAt;
                return false;
            }

            public override string ToString()
            {
                return "*";
            }
        }

        private class PathMatcher : IMatcher
        {
            public int MinConsume => 0;

            public bool ProducesCheckpoint => true;

            public bool CanConsume(string test, int startAt, out int endPosition)
            {
                endPosition = startAt + 1;
                return true;
            }

            public override string ToString()
            {
                return "**";
            }
        }
    }
}
