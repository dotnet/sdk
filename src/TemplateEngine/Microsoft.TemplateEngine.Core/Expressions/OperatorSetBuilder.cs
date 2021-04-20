// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.TemplateEngine.Core.Expressions
{
    public class OperatorSetBuilder<TToken> : IOperatorMap<Operators, TToken>
                where TToken : struct
    {
        private readonly Func<string, string> _decoder;
        private readonly Func<string, string> _encoder;
        private readonly Dictionary<Operators, Func<IEvaluable, IEvaluable>> _operatorScopeLookupFactory = new Dictionary<Operators, Func<IEvaluable, IEvaluable>>();
        private readonly Dictionary<TToken, Operators> _tokensToOperatorsMap = new Dictionary<TToken, Operators>();
        private ITypeConverter _converter;

        //TODO: The signatures for encoder and decoder will need to be updated to account
        //  for encoding/decoding errors, the host will need to be inputted here as well
        //  for the purposes of logging evaluation/parse errors
        public OperatorSetBuilder(Func<string, string> encoder, Func<string, string> decoder)
        {
            _encoder = encoder ?? Passthrough;
            _decoder = decoder ?? Passthrough;
            BadSyntaxTokens = new HashSet<TToken>();
            NoOpTokens = new HashSet<TToken>();
            LiteralSequenceBoundsMarkers = new HashSet<TToken>();
            Terminators = new HashSet<TToken>();
            _converter = new CustomTypeConverter<OperatorSetBuilder<TToken>>();
        }

        public ISet<TToken> BadSyntaxTokens { get; }

        public TToken CloseGroupToken { get; private set; }

        public Operators Identity => Operators.Identity;

        public ISet<TToken> LiteralSequenceBoundsMarkers { get; }

        public TToken LiteralToken { get; private set; }

        public ISet<TToken> NoOpTokens { get; }

        public TToken OpenGroupToken { get; private set; }

        public IReadOnlyDictionary<Operators, Func<IEvaluable, IEvaluable>> OperatorScopeLookupFactory => _operatorScopeLookupFactory;

        public ISet<TToken> Terminators { get; }

        public IReadOnlyDictionary<TToken, Operators> TokensToOperatorsMap => _tokensToOperatorsMap;

        public OperatorSetBuilder<TToken> Add(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.Add, token, evaluate ?? Add, precedesOperator);
        }

        public OperatorSetBuilder<TToken> And(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.And, token, evaluate ?? And, precedesOperator);
        }

        public OperatorSetBuilder<TToken> BadSyntax(params TToken[] token)
        {
            BadSyntaxTokens.UnionWith(token);
            return this;
        }

        public OperatorSetBuilder<TToken> BitwiseAnd(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.BitwiseAnd, token, evaluate ?? BitwiseAnd, precedesOperator);
        }

        public OperatorSetBuilder<TToken> BitwiseOr(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.BitwiseOr, token, evaluate ?? BitwiseOr, precedesOperator);
        }

        public OperatorSetBuilder<TToken> CloseGroup(TToken token)
        {
            CloseGroupToken = token;
            return this;
        }

        public string Decode(string value)
        {
            return _decoder(value);
        }

        public OperatorSetBuilder<TToken> Divide(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.Divide, token, evaluate ?? Divide, precedesOperator);
        }

        public string Encode(string value)
        {
            return _encoder(value);
        }

        public OperatorSetBuilder<TToken> EqualTo(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.EqualTo, token, evaluate ?? EqualTo, precedesOperator);
        }

        public OperatorSetBuilder<TToken> Exponentiate(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.Exponentiate, token, evaluate ?? Exponentiate, precedesOperator);
        }

        public OperatorSetBuilder<TToken> GreaterThan(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.GreaterThan, token, evaluate ?? GreaterThan, precedesOperator);
        }

        public OperatorSetBuilder<TToken> GreaterThanOrEqualTo(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.GreaterThanOrEqualTo, token, evaluate ?? GreaterThanOrEqualTo, precedesOperator);
        }

        public OperatorSetBuilder<TToken> Ignore(params TToken[] token)
        {
            NoOpTokens.UnionWith(token);
            return this;
        }

        public OperatorSetBuilder<TToken> LeftShift(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.LeftShift, token, evaluate ?? LeftShift, precedesOperator);
        }

        public OperatorSetBuilder<TToken> LessThan(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.LessThan, token, evaluate ?? LessThan, precedesOperator);
        }

        public OperatorSetBuilder<TToken> LessThanOrEqualTo(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.LessThanOrEqualTo, token, evaluate ?? LessThanOrEqualTo, precedesOperator);
        }

        public OperatorSetBuilder<TToken> Literal(TToken token)
        {
            LiteralToken = token;
            return this;
        }

        public OperatorSetBuilder<TToken> LiteralBoundsMarkers(params TToken[] token)
        {
            LiteralSequenceBoundsMarkers.UnionWith(token);
            return this;
        }

        public OperatorSetBuilder<TToken> Multiply(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.Multiply, token, evaluate ?? Multiply, precedesOperator);
        }

        public OperatorSetBuilder<TToken> Not(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.Not] =
                x => CreateUnaryChild(x, Operators.Not, evaluate ?? Not);
            _tokensToOperatorsMap[token] = Operators.Not;
            return this;
        }

        public OperatorSetBuilder<TToken> NotEqualTo(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.NotEqualTo, token, evaluate ?? NotEqualTo, precedesOperator);
        }

        public OperatorSetBuilder<TToken> OpenGroup(TToken token)
        {
            OpenGroupToken = token;
            return this;
        }

        public OperatorSetBuilder<TToken> Or(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.Or, token, evaluate ?? Or, precedesOperator);
        }

        public OperatorSetBuilder<TToken> Other(Operators @operator, TToken token, Func<IEvaluable, IEvaluable> nodeFactory)
        {
            _operatorScopeLookupFactory[@operator] = nodeFactory;
            _tokensToOperatorsMap[token] = @operator;
            return this;
        }

        public OperatorSetBuilder<TToken> RightShift(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.RightShift, token, evaluate ?? RightShift, precedesOperator);
        }

        public OperatorSetBuilder<TToken> Subtract(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.Subtract, token, evaluate ?? Subtract, precedesOperator);
        }

        public OperatorSetBuilder<TToken> TerminateWith(params TToken[] token)
        {
            Terminators.UnionWith(token);
            return this;
        }

        public bool TryConvert<T>(object sender, out T result)
        {
            return _converter.TryConvert(sender, out result);
        }

        public OperatorSetBuilder<TToken> TypeConverter<TSelf>(Action<ITypeConverter> configureConverter)
        {
            _converter = new CustomTypeConverter<TSelf>();
            configureConverter(_converter);
            return this;
        }

        public OperatorSetBuilder<TToken> Xor(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            return SetupBinary(Operators.Xor, token, evaluate ?? Xor, precedesOperator);
        }

        private static IEvaluable CreateBinaryChild(IEvaluable active, Operators op, Func<Operators, bool> precedesOperator, Func<object, object, object> evaluate)
        {
            BinaryScope<Operators> self;

            //If we could steal an arg...
            if (!active.IsIndivisible)
            {
                if (active is BinaryScope<Operators> left && precedesOperator(left.Operator))
                {
                    self = new BinaryScope<Operators>(active, op, evaluate)
                    {
                        Left = left.Right
                    };
                    left.Right.Parent = self;
                    left.Right = self;
                    return self;
                }
            }

            //We couldn't steal an arg, "active" is now our left, inject ourselves into
            //  active's parent in its place
            self = new BinaryScope<Operators>(active.Parent, op, evaluate);

            if (active.Parent != null)
            {
                switch (active.Parent)
                {
                    case UnaryScope<Operators> unary:
                        unary.Parent = self;
                        break;
                    case BinaryScope<Operators> binary:
                        if (binary.Left == active)
                        {
                            binary.Left = self;
                        }
                        else if (binary.Right == active)
                        {
                            binary.Right = self;
                        }
                        break;
                }
            }

            active.Parent = self;
            self.Left = active;
            return self;
        }

        private static IEvaluable CreateUnaryChild(IEvaluable active, Operators op, Func<object, object> evaluate)
        {
            UnaryScope<Operators> self = new UnaryScope<Operators>(active, op, evaluate);
            active.TryAccept(self);
            return self;
        }

        private static object EqualTo(object left, object right)
        {
            string l = left as string;
            string r = right as string;

            if (l != null && r != null)
            {
                return string.Equals(l, r, StringComparison.OrdinalIgnoreCase);
            }

            return Equals(l, r);
        }

        private static object GreaterThan(object left, object right)
        {
            return ((IComparable)left).CompareTo(right) > 0;
        }

        private static object GreaterThanOrEqualTo(object left, object right)
        {
            return ((IComparable)left).CompareTo(right) >= 0;
        }

        private static object LessThan(object left, object right)
        {
            return ((IComparable)left).CompareTo(right) < 0;
        }

        private static object LessThanOrEqualTo(object left, object right)
        {
            return ((IComparable)left).CompareTo(right) <= 0;
        }

        private static string Passthrough(string arg)
        {
            return arg;
        }

        private static bool Precedes(Operators check, Operators arg)
        {
            return check < arg;
        }

        private object Add(object left, object right)
        {
            if (_converter.TryConvert(left, out long longLeft))
            {
                if (_converter.TryConvert(right, out long longRight))
                {
                    return longLeft + longRight;
                }

                if (_converter.TryConvert(right, out double doubleRight))
                {
                    return longLeft + doubleRight;
                }
            }
            else
            {
                if (_converter.TryConvert(left, out double doubleLeft))
                {
                    if (_converter.TryConvert(right, out long longRight))
                    {
                        return doubleLeft + longRight;
                    }

                    if (_converter.TryConvert(right, out double doubleRight))
                    {
                        return doubleLeft + doubleRight;
                    }
                }
            }

            return string.Concat(left, right);
        }

        private object And(object left, object right)
        {
            if (_converter.TryConvert(left, out bool boolLeft) && _converter.TryConvert(right, out bool boolRight))
            {
                return boolLeft && boolRight;
            }

            throw new Exception($"Unable to logical and {left?.GetType()} and {right?.GetType()}");
        }

        private object BitwiseAnd(object left, object right)
        {
            if (_converter.TryConvert(left, out long longLeft) && _converter.TryConvert(right, out long longRight))
            {
                return longLeft & longRight;
            }

            throw new Exception($"Unable to bitwise and {left?.GetType()} and {right?.GetType()}");
        }

        private object BitwiseOr(object left, object right)
        {
            if (_converter.TryConvert(left, out long longLeft) && _converter.TryConvert(right, out long longRight))
            {
                return longLeft | longRight;
            }

            throw new Exception($"Unable to bitwise or {left?.GetType()} and {right?.GetType()}");
        }

        private object Divide(object left, object right)
        {
            long longRight;
            int doubleRight;

            if (_converter.TryConvert(left, out long longLeft))
            {
                if (_converter.TryConvert(right, out longRight))
                {
                    return longLeft / longRight;
                }

                if (_converter.TryConvert(right, out doubleRight))
                {
                    return longLeft / doubleRight;
                }
            }
            else if (_converter.TryConvert(left, out int doubleLeft))
            {
                if (_converter.TryConvert(right, out longRight))
                {
                    return doubleLeft / longRight;
                }

                if (_converter.TryConvert(right, out doubleRight))
                {
                    return doubleLeft / doubleRight;
                }
            }

            throw new Exception($"Cannot divide {left?.GetType()} and {right?.GetType()}");
        }

        private object Exponentiate(object left, object right)
        {
            long longRight;
            int intRight;

            if (_converter.TryConvert(left, out long longLeft))
            {
                if (_converter.TryConvert(right, out longRight))
                {
                    return Math.Pow(longLeft, longRight);
                }

                if (_converter.TryConvert(right, out intRight))
                {
                    return Math.Pow(longLeft, intRight);
                }
            }
            else if (_converter.TryConvert(left, out int intLeft))
            {
                if (_converter.TryConvert(right, out longRight))
                {
                    return Math.Pow(intLeft, longRight);
                }

                if (_converter.TryConvert(right, out intRight))
                {
                    return Math.Pow(intLeft, intRight);
                }
            }

            throw new Exception($"Cannot exponentiate {left?.GetType()} and {right?.GetType()}");
        }

        private object LeftShift(object left, object right)
        {
            if (_converter.TryConvert(left, out long longLeft) && _converter.TryConvert(right, out int intRight))
            {
                return longLeft << intRight;
            }

            throw new Exception($"Unable to left shift {left?.GetType()} and {right?.GetType()}");
        }

        private object Multiply(object left, object right)
        {
            long longRight;
            int doubleRight;

            if (_converter.TryConvert(left, out long longLeft))
            {
                if (_converter.TryConvert(right, out longRight))
                {
                    return longLeft * longRight;
                }

                if (_converter.TryConvert(right, out doubleRight))
                {
                    return longLeft * doubleRight;
                }
            }
            else if (_converter.TryConvert(left, out int doubleLeft))
            {
                if (_converter.TryConvert(right, out longRight))
                {
                    return doubleLeft * longRight;
                }

                if (_converter.TryConvert(right, out doubleRight))
                {
                    return doubleLeft * doubleRight;
                }
            }

            throw new Exception($"Cannot multiply {left?.GetType()} and {right?.GetType()}");
        }

        private object Not(object operand)
        {
            if (_converter.TryConvert(operand, out bool l))
            {
                return !l;
            }

            throw new Exception($"Unable to logical not {operand?.GetType()}");
        }

        private object NotEqualTo(object left, object right)
        {
            if (left is string l && right is string r)
            {
                return !string.Equals(l, r, StringComparison.OrdinalIgnoreCase);
            }

            return Not(EqualTo(left, right));
        }

        private object Or(object left, object right)
        {
            if (_converter.TryConvert(left, out bool l) && _converter.TryConvert(right, out bool r))
            {
                return l || r;
            }

            throw new Exception($"Unable to logical or {left?.GetType()} and {right?.GetType()}");
        }

        private object RightShift(object left, object right)
        {
            if (_converter.TryConvert(left, out long longLeft) && _converter.TryConvert(right, out int intRight))
            {
                return longLeft >> intRight;
            }

            throw new Exception($"Unable to right shift {left?.GetType()} and {right?.GetType()}");
        }

        private OperatorSetBuilder<TToken> SetupBinary(Operators op, TToken token, Func<object, object, object> evaluate, Func<Operators, bool> precedesOperator = null)
        {
            _operatorScopeLookupFactory[op] =
                x => CreateBinaryChild(x, op, precedesOperator ?? (a => Precedes(op, a)), evaluate ?? Add);
            _tokensToOperatorsMap[token] = op;
            return this;
        }

        private object Subtract(object left, object right)
        {
            long longRight;
            int doubleRight;

            if (_converter.TryConvert(left, out long longLeft))
            {
                if (_converter.TryConvert(right, out longRight))
                {
                    return longLeft - longRight;
                }

                if (_converter.TryConvert(right, out doubleRight))
                {
                    return longLeft - doubleRight;
                }
            }
            else if (_converter.TryConvert(left, out int doubleLeft))
            {
                if (_converter.TryConvert(right, out longRight))
                {
                    return doubleLeft - longRight;
                }

                if (_converter.TryConvert(right, out doubleRight))
                {
                    return doubleLeft - doubleRight;
                }
            }

            throw new Exception($"Cannot subtract {left?.GetType()} and {right?.GetType()}");
        }

        private object Xor(object left, object right)
        {
            if (_converter.TryConvert(left, out bool l) && _converter.TryConvert(right, out bool r))
            {
                return l ^ r;
            }

            long longRight;
            int doubleRight;

            if (_converter.TryConvert(left, out long longLeft))
            {
                if (_converter.TryConvert(right, out longRight))
                {
                    return longLeft ^ longRight;
                }

                if (_converter.TryConvert(right, out doubleRight))
                {
                    return longLeft ^ doubleRight;
                }
            }
            else if (_converter.TryConvert(left, out int doubleLeft))
            {
                if (_converter.TryConvert(right, out longRight))
                {
                    return doubleLeft ^ longRight;
                }

                if (_converter.TryConvert(right, out doubleRight))
                {
                    return doubleLeft ^ doubleRight;
                }
            }

            throw new Exception($"Can't xor {left?.GetType()} and {right?.GetType()}");
        }

        public class CustomTypeConverter<TScope> : ITypeConverter
        {
            public Type ScopeType => typeof(TScope);

            public ITypeConverter Register<T>(TypeConverterDelegate<T> converter)
            {
                TypeConverterLookup<T>.TryConvert = converter;
                return this;
            }

            public bool TryConvert<T>(object source, out T result)
            {
                TypeConverterDelegate<T> converter = TypeConverterLookup<T>.TryConvert;

                return converter != null
                    ? converter(source, out result)
                    : TryCoreConvert(source, out result);
            }

            public bool TryCoreConvert<T>(object source, out T result)
            {
                return Converter.TryConvert(source, out result);
            }

            private static class TypeConverterLookup<T>
            {
                public static TypeConverterDelegate<T> TryConvert;
            }
        }
    }
}
