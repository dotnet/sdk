// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class PhasedOperation : IOperationProvider
    {
        private readonly IReadOnlyList<Phase> _config;
        private readonly string _id;
        private readonly bool _initialState;

        public PhasedOperation(string id, IReadOnlyList<Phase> config, bool initialState)
        {
            _id = id;
            _config = config;
            _initialState = initialState;
        }

        public string Id => _id;

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            Dictionary<ITokenConfig, int> tokenMap = new Dictionary<ITokenConfig, int>();
            List<IToken> tokens = new List<IToken>();

            Stack<IEnumerator<Phase>> sourceParents = new Stack<IEnumerator<Phase>>();
            Stack<List<SpecializedPhase>> targetParents = new Stack<List<SpecializedPhase>>();
            IEnumerator<Phase> currentSource = _config.GetEnumerator();
            List<SpecializedPhase> currentTarget = new List<SpecializedPhase>();

            while (sourceParents.Count > 0 || currentSource != null)
            {
                while (currentSource?.MoveNext() ?? false)
                {
                    Phase c = currentSource.Current;
                    if (!tokenMap.TryGetValue(c.Match, out int existingMatchToken))
                    {
                        IToken bytes = c.Match.ToToken(encoding);
                        existingMatchToken = tokenMap[c.Match] = tokens.Count;
                        tokens.Add(bytes);
                    }

                    SpecializedPhase target = new SpecializedPhase
                    {
                        Replacement = c.Replacement != null ? encoding.GetBytes(c.Replacement) : tokens[existingMatchToken].Value,
                        Match = existingMatchToken,
                    };

                    foreach (ITokenConfig reset in c.ResetsWith)
                    {
                        if (!tokenMap.TryGetValue(reset, out int existingResetToken))
                        {
                            IToken token = reset.ToToken(encoding);
                            existingResetToken = tokenMap[reset] = tokens.Count;
                            tokens.Add(token);
                        }
                        target.ResetsWith.Add(existingResetToken);
                    }

                    currentTarget.Add(target);

                    if (c.Next.Count > 0)
                    {
                        sourceParents.Push(currentSource);
                        currentSource = currentSource.Current.Next.GetEnumerator();

                        targetParents.Push(currentTarget);
                        currentTarget = new List<SpecializedPhase>();
                    }
                }

                currentSource?.Dispose();
                currentSource = null;

                if (sourceParents.Count > 0)
                {
                    currentSource = sourceParents.Pop();
                    List<SpecializedPhase> children = currentTarget;
                    currentTarget = targetParents.Pop();
                    currentTarget[currentTarget.Count - 1].Next.AddRange(children);
                }
            }

            return new Impl(this, tokens, currentTarget, _initialState);
        }

        private class Impl : IOperation
        {
            private readonly PhasedOperation _definition;
            private readonly IReadOnlyList<SpecializedPhase> _entryPoints;
            private SpecializedPhase _currentPhase;

            public Impl(PhasedOperation definition, IReadOnlyList<IToken> config, IReadOnlyList<SpecializedPhase> entryPoints, bool initialState)
            {
                _definition = definition;
                Tokens = config;
                _entryPoints = entryPoints;
                IsInitialStateOn = string.IsNullOrEmpty(_definition._id) || initialState;
            }

            public string Id => _definition._id;

            public IReadOnlyList<IToken> Tokens { get; }

            public bool IsInitialStateOn { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                IReadOnlyList<SpecializedPhase> nextPhases = _currentPhase?.Next ?? _entryPoints;
                SpecializedPhase match = nextPhases.FirstOrDefault(x => x.Match == token);

                if (match != null)
                {
                    _currentPhase = match.Next.Count > 0 ? match : null;
                    target.Write(match.Replacement, 0, match.Replacement.Length);
                    return match.Replacement.Length;
                }

                if (_currentPhase != null && _currentPhase.ResetsWith.Contains(token))
                {
                    _currentPhase = null;
                }

                target.Write(Tokens[token].Value, Tokens[token].Start, Tokens[token].Length);
                return Tokens[token].Length;
            }
        }

        private class SpecializedPhase
        {
            public SpecializedPhase()
            {
                Next = new List<SpecializedPhase>();
                ResetsWith = new List<int>();
            }

            public int Match { get; set; }

            public List<SpecializedPhase> Next { get; }

            public byte[] Replacement { get; set; }

            public List<int> ResetsWith { get; }
        }

        private class SpecializedPhasedOperationConfig
        {
            public IReadOnlyList<SpecializedPhase> EntryPoints { get; set; }

            public IReadOnlyList<ITokenConfig> Tokens { get; set; }
        }
    }
}
