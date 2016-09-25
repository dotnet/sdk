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

        public PhasedOperation(string id, IReadOnlyList<Phase> config)
        {
            _id = id;
            _config = config;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            Dictionary<string, int> tokenMap = new Dictionary<string, int>();
            List<byte[]> tokens = new List<byte[]>();

            Stack<IEnumerator<Phase>> sourceParents = new Stack<IEnumerator<Phase>>();
            Stack<List<SpecializedPhase>> targetParents = new Stack<List<SpecializedPhase>>();
            IEnumerator<Phase> currentSource = _config.GetEnumerator();
            List<SpecializedPhase> currentTarget = new List<SpecializedPhase>();

            while (sourceParents.Count > 0 || currentSource != null)
            {
                while (currentSource?.MoveNext() ?? false)
                {
                    Phase c = currentSource.Current;

                    int existingMatchToken;
                    if (!tokenMap.TryGetValue(c.Match, out existingMatchToken))
                    {
                        byte[] bytes = encoding.GetBytes(c.Match);
                        existingMatchToken = tokenMap[c.Match] = tokens.Count;
                        tokens.Add(bytes);
                    }

                    SpecializedPhase target = new SpecializedPhase
                    {
                        Replacement = c.Replacement != null ? encoding.GetBytes(c.Replacement) : tokens[existingMatchToken],
                        Match = existingMatchToken,
                    };

                    foreach (string reset in c.ResetsWith)
                    {
                        int existingResetToken;
                        if (!tokenMap.TryGetValue(reset, out existingResetToken))
                        {
                            byte[] bytes = encoding.GetBytes(reset);
                            existingResetToken = tokenMap[reset] = tokens.Count;
                            tokens.Add(bytes);
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

            return new Impl(this, new SpecializedPhasedOperationConfig
            {
                Tokens = tokens,
                EntryPoints = currentTarget
            });
        }

        private class Impl : IOperation
        {
            private readonly PhasedOperation _definition;
            private readonly IReadOnlyList<SpecializedPhase> _entryPoints;
            private SpecializedPhase _currentPhase;

            public Impl(PhasedOperation definition, SpecializedPhasedOperationConfig config)
            {
                _definition = definition;
                Tokens = config.Tokens;
                _entryPoints = config.EntryPoints;
            }

            public string Id => _definition._id;

            public IReadOnlyList<byte[]> Tokens { get; }

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

                target.Write(Tokens[token], 0, Tokens[token].Length);
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

            public IReadOnlyList<byte[]> Tokens { get; set; }
        }
    }
}
