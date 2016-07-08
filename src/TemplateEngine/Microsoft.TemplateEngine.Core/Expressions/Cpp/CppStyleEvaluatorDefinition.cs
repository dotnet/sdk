using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core.Expressions.Cpp
{
    public static class CppStyleEvaluatorDefinition
    {
        private const int ReservedTokenCount = 22;
        private const int ReservedTokenMaxIndex = ReservedTokenCount - 1;
        private static readonly IOperationProvider[] NoOperationProviders = new IOperationProvider[0];

        public static bool EvaluateFromString(string text, IVariableCollection variables)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            using (MemoryStream res = new MemoryStream())
            {
                EngineConfig cfg = new EngineConfig(variables);
                IProcessorState state = new ProcessorState(ms, res, (int)ms.Length, (int)ms.Length, cfg, NoOperationProviders);
                int len = (int)ms.Length;
                int pos = 0;
                return CppStyleEvaluator(state, ref len, ref pos);
            }
        }

        public static bool CppStyleEvaluator(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition)
        {
            TokenTrie trie = new TokenTrie();

            //Logic
            trie.AddToken(processor.Encoding.GetBytes("&&"), 0);
            trie.AddToken(processor.Encoding.GetBytes("||"), 1);
            trie.AddToken(processor.Encoding.GetBytes("^"), 2);
            trie.AddToken(processor.Encoding.GetBytes("!"), 3);
            trie.AddToken(processor.Encoding.GetBytes(">"), 4);
            trie.AddToken(processor.Encoding.GetBytes(">="), 5);
            trie.AddToken(processor.Encoding.GetBytes("<"), 6);
            trie.AddToken(processor.Encoding.GetBytes("<="), 7);
            trie.AddToken(processor.Encoding.GetBytes("=="), 8);
            trie.AddToken(processor.Encoding.GetBytes("="), 9);
            trie.AddToken(processor.Encoding.GetBytes("!="), 10);

            //Bitwise
            trie.AddToken(processor.Encoding.GetBytes("&"), 11);
            trie.AddToken(processor.Encoding.GetBytes("|"), 12);
            trie.AddToken(processor.Encoding.GetBytes("<<"), 13);
            trie.AddToken(processor.Encoding.GetBytes(">>"), 14);

            //Braces
            trie.AddToken(processor.Encoding.GetBytes("("), 15);
            trie.AddToken(processor.Encoding.GetBytes(")"), 16);

            //Whitespace
            trie.AddToken(processor.Encoding.GetBytes(" "), 17);
            trie.AddToken(processor.Encoding.GetBytes("\t"), 18);

            //EOLs
            trie.AddToken(processor.Encoding.GetBytes("\r\n"), 19);
            trie.AddToken(processor.Encoding.GetBytes("\n"), 20);
            trie.AddToken(processor.Encoding.GetBytes("\r"), 21);

            //Tokens
            trie.Append(processor.EncodingConfig.Variables);

            //Run forward to EOL and collect args
            TokenFamily currentTokenFamily;
            List<byte> currentTokenBytes = new List<byte>();
            List<TokenRef> tokens = new List<TokenRef>();
            int token;
            if (!trie.GetOperation(processor.CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
            {
                currentTokenFamily = TokenFamily.Literal;
                currentTokenBytes.Add(processor.CurrentBuffer[currentBufferPosition++]);
            }
            else if (token > ReservedTokenMaxIndex)
            {
                currentTokenFamily = TokenFamily.Reference | (TokenFamily)token;
                tokens.Add(new TokenRef
                {
                    Family = currentTokenFamily
                });
            }
            else
            {
                currentTokenFamily = (TokenFamily)token;

                if (currentTokenFamily != TokenFamily.WindowsEOL && currentTokenFamily != TokenFamily.LegacyMacEOL && currentTokenFamily != TokenFamily.UnixEOL)
                {
                    tokens.Add(new TokenRef
                    {
                        Family = currentTokenFamily
                    });
                }
                else
                {
                    return EvaluateCondition(tokens, processor.EncodingConfig.VariableValues);
                }
            }

            int braceDepth = 0;
            if (tokens[0].Family == TokenFamily.OpenBrace)
            {
                ++braceDepth;
            }

            bool first = true;
            while ((first || braceDepth > 0) && bufferLength > 0)
            {
                int targetLen = Math.Min(bufferLength, trie.MaxLength);
                for (; currentBufferPosition < bufferLength - targetLen + 1;)
                {
                    int oldBufferPos = currentBufferPosition;
                    if (trie.GetOperation(processor.CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                    {
                        if (braceDepth == 0)
                        {
                            switch (tokens[tokens.Count - 1].Family)
                            {
                                case TokenFamily.Whitespace:
                                case TokenFamily.Tab:
                                case TokenFamily.CloseBrace:
                                case TokenFamily.WindowsEOL:
                                case TokenFamily.UnixEOL:
                                case TokenFamily.LegacyMacEOL:
                                    TokenFamily thisFamily = (TokenFamily)token;
                                    if (thisFamily == TokenFamily.WindowsEOL || thisFamily == TokenFamily.UnixEOL || thisFamily == TokenFamily.LegacyMacEOL)
                                    {
                                        currentBufferPosition = oldBufferPos;
                                    }

                                    break;
                                default:
                                    currentBufferPosition = oldBufferPos;
                                    first = false;
                                    break;
                            }

                            if (!first)
                            {
                                break;
                            }
                        }

                        //We matched an item, so whatever this is, it's not a literal, end the current literal if that's
                        //  what we currently have
                        if (currentTokenFamily == TokenFamily.Literal)
                        {
                            string literal = processor.Encoding.GetString(currentTokenBytes.ToArray());
                            tokens.Add(new TokenRef
                            {
                                Family = TokenFamily.Literal,
                                Literal = literal
                            });
                            currentTokenBytes.Clear();
                        }

                        //If we have a token from the args...
                        if (token > ReservedTokenMaxIndex)
                        {
                            currentTokenFamily = TokenFamily.Reference | (TokenFamily)token;
                            tokens.Add(new TokenRef
                            {
                                Family = currentTokenFamily
                            });
                        }
                        //If we have a normal token...
                        else
                        {
                            currentTokenFamily = (TokenFamily)token;
                            if (currentTokenFamily != TokenFamily.WindowsEOL && currentTokenFamily != TokenFamily.LegacyMacEOL && currentTokenFamily != TokenFamily.UnixEOL)
                            {
                                if (currentTokenFamily == TokenFamily.OpenBrace)
                                {
                                    ++braceDepth;
                                }
                                else if (currentTokenFamily == TokenFamily.CloseBrace)
                                {
                                    --braceDepth;
                                }

                                tokens.Add(new TokenRef
                                {
                                    Family = currentTokenFamily
                                });
                            }
                            else
                            {
                                return EvaluateCondition(tokens, processor.EncodingConfig.VariableValues);
                            }
                        }
                    }
                    else if (braceDepth > 0)
                    {
                        currentTokenFamily = TokenFamily.Literal;
                        currentTokenBytes.Add(processor.CurrentBuffer[currentBufferPosition++]);
                    }
                    else
                    {
                        first = false;
                        break;
                    }
                }

                processor.AdvanceBuffer(currentBufferPosition);
                currentBufferPosition = processor.CurrentBufferPosition;
                bufferLength = processor.CurrentBufferLength;
            }

            return EvaluateCondition(tokens, processor.EncodingConfig.VariableValues);
        }

        private static bool IsLogicalOperator(Operator op)
        {
            return op == Operator.And || op == Operator.Or || op == Operator.Xor || op == Operator.Not;
        }

        private static void CombineExpressionOperator(ref Scope current, Stack<Scope> parents)
        {
            if (current.Operator == Operator.None)
            {
                Scope leftScope = current.Left as Scope;
                if (current.TargetPlacement != Scope.NextPlacement.Right
                    || leftScope == null
                    || !IsLogicalOperator(leftScope.Operator))
                {
                    return;
                }

                Scope tmp2 = new Scope
                {
                    Value = leftScope.Right
                };

                leftScope.Right = tmp2;
                parents.Push(leftScope);
                current = tmp2;
                return;
            }

            Scope tmp = new Scope();

            if (!IsLogicalOperator(current.Operator))
            {
                tmp.Value = current;
            }
            else
            {
                tmp.Value = current.Right;
                current.Right = tmp;
                parents.Push(current);
            }

            current = tmp;
        }

        private static bool EvaluateCondition(List<TokenRef> tokens, IReadOnlyList<Func<object>> values)
        {
            //Skip over all leading whitespace
            int i = 0;
            for (; i < tokens.Count && (tokens[i].Family == TokenFamily.Whitespace || tokens[i].Family == TokenFamily.Tab); ++i)
            {
            }

            //Scan through all remaining tokens and put them in a form the standard evaluator can understand
            List<TokenRef> outputTokens = new List<TokenRef>();
            for (; i < tokens.Count; ++i)
            {
                if (tokens[i].Family == TokenFamily.Whitespace || tokens[i].Family == TokenFamily.Tab)
                {
                    //Ignore whitespace
                }
                else
                {
                    if (tokens[i].Family.HasFlag(TokenFamily.Reference))
                    {
                        outputTokens.Add(tokens[i]);
                    }
                    else if (tokens[i].Family == TokenFamily.Literal)
                    {
                        //Combine literals
                        string literalValue = tokens[i].Literal;
                        string followingWhitespace = "";
                        int reach = i;

                        for (int j = i + 1; j < tokens.Count; ++j)
                        {
                            switch (tokens[j].Family)
                            {
                                case TokenFamily.Literal:
                                    literalValue += followingWhitespace + tokens[j].Literal;
                                    followingWhitespace = string.Empty;
                                    reach = j;
                                    break;
                                case TokenFamily.Tab:
                                    followingWhitespace += '\t';
                                    break;
                                case TokenFamily.Whitespace:
                                    followingWhitespace += ' ';
                                    break;
                                default:
                                    j = tokens.Count;
                                    break;
                            }
                        }

                        i = reach;
                        outputTokens.Add(new TokenRef
                        {
                            Family = TokenFamily.Literal,
                            Literal = literalValue
                        });
                    }
                    else
                    {
                        outputTokens.Add(tokens[i]);
                    }
                }
            }

            Scope root = new Scope();
            Scope current = root;
            Stack<Scope> parents = new Stack<Scope>();

            for (i = 0; i < outputTokens.Count; ++i)
            {
                switch (outputTokens[i].Family)
                {
                    case TokenFamily.Not:
                        {
                            Scope nextScope = new Scope
                            {
                                TargetPlacement = Scope.NextPlacement.Right
                            };

                            current.Value = nextScope;
                            parents.Push(current);
                            current = nextScope;
                            current.Operator = Operator.Not;
                            break;
                        }
                    case TokenFamily.Literal:
                        current.Value = InferTypeAndConvertLiteral(outputTokens[i].Literal);

                        while (parents.Count > 0 && current.TargetPlacement == Scope.NextPlacement.None)
                        {
                            current = parents.Pop();
                        }
                        break;
                    case TokenFamily.And:
                        if (current.Operator != Operator.None)
                        {
                            Scope s = new Scope
                            {
                                Value = current
                            };

                            current = s;
                        }

                        current.Operator = Operator.And;
                        current.TargetPlacement = Scope.NextPlacement.Right;
                        break;
                    case TokenFamily.BitwiseAnd:
                        CombineExpressionOperator(ref current, parents);
                        current.Operator = Operator.BitwiseAnd;
                        break;
                    case TokenFamily.BitwiseOr:
                        CombineExpressionOperator(ref current, parents);
                        current.Operator = Operator.BitwiseOr;
                        break;
                    case TokenFamily.CloseBrace:
                        //If the grouping is valid, the grouping has already been closed
                        //  due to argument fulfillment, do nothing.
                        // -- OR --
                        //This is a grouping around a unary operator
                        if (parents.Count > 0 && current.TargetPlacement == Scope.NextPlacement.Right)
                        {
                            current = parents.Pop();
                        }
                        break;
                    case TokenFamily.EqualTo:
                    case TokenFamily.EqualToShort:
                        CombineExpressionOperator(ref current, parents);
                        current.Operator = Operator.EqualTo;
                        break;
                    case TokenFamily.GreaterThan:
                        CombineExpressionOperator(ref current, parents);
                        current.Operator = Operator.GreaterThan;
                        break;
                    case TokenFamily.GreaterThanOrEqualTo:
                        CombineExpressionOperator(ref current, parents);
                        current.Operator = Operator.GreaterThanOrEqualTo;
                        break;
                    case TokenFamily.LeftShift:
                        CombineExpressionOperator(ref current, parents);
                        current.Operator = Operator.LeftShift;
                        break;
                    case TokenFamily.LessThan:
                        CombineExpressionOperator(ref current, parents);
                        current.Operator = Operator.LessThan;
                        break;
                    case TokenFamily.LessThanOrEqualTo:
                        CombineExpressionOperator(ref current, parents);
                        current.Operator = Operator.LessThanOrEqualTo;
                        break;
                    case TokenFamily.NotEqualTo:
                        CombineExpressionOperator(ref current, parents);
                        current.Operator = Operator.NotEqualTo;
                        break;
                    case TokenFamily.OpenBrace:
                        {
                            Scope nextScope = new Scope();
                            current.Value = nextScope;
                            parents.Push(current);
                            current = nextScope;
                            break;
                        }
                    case TokenFamily.Or:
                        if (current.Operator != Operator.None)
                        {
                            Scope s = new Scope
                            {
                                Value = current
                            };

                            current = s;
                        }

                        current.Operator = Operator.Or;
                        break;
                    case TokenFamily.RightShift:
                        CombineExpressionOperator(ref current, parents);
                        current.Operator = Operator.RightShift;
                        break;
                    case TokenFamily.Xor:
                        if (current.Operator != Operator.None)
                        {
                            Scope s = new Scope
                            {
                                Value = current
                            };

                            current = s;
                        }

                        current.Operator = Operator.Xor;
                        break;
                    default:
                        current.Value = ResolveToken(outputTokens[i], values);

                        while (parents.Count > 0 && current.TargetPlacement == Scope.NextPlacement.None)
                        {
                            current = parents.Pop();
                        }

                        break;
                }
            }

            Debug.Assert(parents.Count == 0, "Unbalanced condition");
            return (bool)current.Evaluate();
        }

        private static object InferTypeAndConvertLiteral(string literal)
        {
            if (!literal.Contains("\""))
            {
                if (string.Equals(literal, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(literal, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.Equals(literal, "null", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                double literalDouble;
                if (literal.Contains(".") && double.TryParse(literal, out literalDouble))
                {
                    return literalDouble;
                }

                long literalLong;
                if (long.TryParse(literal, out literalLong))
                {
                    return literalLong;
                }

                if (literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(literal.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out literalLong))
                {
                    return literalLong;
                }

                return null;
            }

            return literal.Substring(1, literal.Length - 2);
        }

        private static object ResolveToken(TokenRef tokenRef, IReadOnlyList<Func<object>> values)
        {
            return values[(int)(tokenRef.Family & ~TokenFamily.Reference) - ReservedTokenCount]();
        }
    }
}
