using System;
using System.Collections.Generic;

namespace N3P.StreamReplacer.Expressions.Cpp
{
    public static class CppStyleEvaluatorDefinition
    {
        public static bool CppStyleEvaluator(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, IReadOnlyDictionary<string, object> args)
        {
            object[] values = new object[args.Count];
            SimpleTrie trie = new SimpleTrie();

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
            trie.AddToken(processor.Encoding.GetBytes("!="), 9);

            //Bitwise
            trie.AddToken(processor.Encoding.GetBytes("&"), 10);
            trie.AddToken(processor.Encoding.GetBytes("|"), 11);
            trie.AddToken(processor.Encoding.GetBytes("<<"), 12);
            trie.AddToken(processor.Encoding.GetBytes(">>"), 13);

            //Braces
            trie.AddToken(processor.Encoding.GetBytes("("), 14);
            trie.AddToken(processor.Encoding.GetBytes(")"), 15);

            //Whitespace
            trie.AddToken(processor.Encoding.GetBytes(" "), 16);
            trie.AddToken(processor.Encoding.GetBytes("\t"), 17);

            //EOLs
            trie.AddToken(processor.Encoding.GetBytes("\r\n"), 18);
            trie.AddToken(processor.Encoding.GetBytes("\n"), 19);
            trie.AddToken(processor.Encoding.GetBytes("\r"), 20);

            //Tokens
            int tokenIndex = 21;
            foreach (KeyValuePair<string, object> pair in args)
            {
                trie.AddToken(processor.Encoding.GetBytes(pair.Key), tokenIndex);
                values[tokenIndex++ - 21] = pair.Value;
            }

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
            else if (token > 20)
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
                    return EvaluateCondition(tokens, values);
                }
            }

            while (bufferLength > trie.Length)
            {
                for (; currentBufferPosition < bufferLength - trie.Length + 1;)
                {
                    if (bufferLength == 0)
                    {
                        currentBufferPosition = 0;
                        return EvaluateCondition(tokens, values);
                    }

                    if (trie.GetOperation(processor.CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                    {
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
                        if (token > 20)
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
                                tokens.Add(new TokenRef
                                {
                                    Family = currentTokenFamily
                                });
                            }
                            else
                            {
                                return EvaluateCondition(tokens, values);
                            }
                        }
                    }
                    else
                    {
                        currentTokenFamily = TokenFamily.Literal;
                        currentTokenBytes.Add(processor.CurrentBuffer[currentBufferPosition++]);
                    }
                }

                processor.AdvanceBuffer(bufferLength - trie.Length + 1);
                currentBufferPosition = processor.CurrentBufferPosition;
                bufferLength = processor.CurrentBufferLength;
            }

            return EvaluateCondition(tokens, values);
        }

        private static bool EvaluateCondition(List<TokenRef> tokens, object[] values)
        {
            //Skip over all leading whitespace
            int i = 0;
            for (; tokens.Count > 0 && (tokens[i].Family == TokenFamily.Whitespace || tokens[i].Family == TokenFamily.Tab); ++i)
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
            bool expectingRightHandSide = false;

            for (i = 0; i < outputTokens.Count; ++i)
            {
                switch (outputTokens[i].Family)
                {
                    case TokenFamily.Not:
                        {
                            Scope nextScope = new Scope();
                            if (expectingRightHandSide)
                            {
                                current.Right = nextScope;
                            }
                            else
                            {
                                current.Left = nextScope;
                            }
                            parents.Push(current);
                            current = nextScope;
                            expectingRightHandSide = false;
                            current.Operator = Operator.Not;
                            break;
                        }
                    case TokenFamily.Literal:
                        if (expectingRightHandSide)
                        {
                            current.Right = InferTypeAndConvertLiteral(outputTokens[i].Literal);
                        }
                        else
                        {
                            current.Left = InferTypeAndConvertLiteral(outputTokens[i].Literal);
                            expectingRightHandSide = true;
                        }

                        if (current.Operator == Operator.Not)
                        {
                            current = parents.Pop();
                        }
                        break;
                    case TokenFamily.And:
                        current.Operator = Operator.And;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.BitwiseAnd:
                        current.Operator = Operator.BitwiseAnd;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.BitwiseOr:
                        current.Operator = Operator.BitwiseOr;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.CloseBrace:
                        current = parents.Pop();
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.EqualTo:
                        current.Operator = Operator.EqualTo;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.GreaterThan:
                        current.Operator = Operator.GreaterThan;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.GreaterThanOrEqualTo:
                        current.Operator = Operator.GreaterThanOrEqualTo;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.LeftShift:
                        current.Operator = Operator.LeftShift;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.LessThan:
                        current.Operator = Operator.LessThan;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.LessThanOrEqualTo:
                        current.Operator = Operator.LessThanOrEqualTo;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.NotEqualTo:
                        current.Operator = Operator.NotEqualTo;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.OpenBrace:
                        {
                            Scope nextScope = new Scope();
                            if (expectingRightHandSide)
                            {
                                current.Right = nextScope;
                            }
                            else
                            {
                                current.Left = nextScope;
                            }
                            parents.Push(current);
                            current = nextScope;
                            expectingRightHandSide = false;
                            break;
                        }
                    case TokenFamily.Or:
                        current.Operator = Operator.Or;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.RightShift:
                        current.Operator = Operator.RightShift;
                        expectingRightHandSide = true;
                        break;
                    case TokenFamily.Xor:
                        current.Operator = Operator.Xor;
                        expectingRightHandSide = true;
                        break;
                    default:
                        if (expectingRightHandSide)
                        {
                            current.Right = ResolveToken(outputTokens[i], values);
                        }
                        else
                        {
                            current.Left = ResolveToken(outputTokens[i], values);
                            expectingRightHandSide = true;
                        }

                        if (current.Operator == Operator.Not)
                        {
                            current = parents.Pop();
                        }
                        break;
                }
            }

            if (parents.Count > 0)
            {
                throw new Exception("Unbalanced condition");
            }

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

                long literalLong;
                if (long.TryParse(literal, out literalLong))
                {
                    return literalLong;
                }

                return null;
            }

            return literal.Substring(1, literal.Length - 2);
        }

        private static object ResolveToken(TokenRef tokenRef, object[] values)
        {
            if (tokenRef.Family == TokenFamily.Literal)
            {
                return tokenRef.Literal;
            }

            if ((tokenRef.Family & TokenFamily.Reference) == TokenFamily.Reference)
            {
                return values[(int)(tokenRef.Family & ~TokenFamily.Reference) - 21];
            }

            throw new Exception($"Token type {tokenRef.Family} does not have a representation as a value");
        }
    }
}
