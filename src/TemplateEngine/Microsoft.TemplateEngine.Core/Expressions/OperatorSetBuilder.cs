using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Expressions
{
    public class OperatorSetBuilder<TToken> : IOperatorMap<Operators, TToken>
        where TToken : struct
    {
        private readonly Func<string, string> _decoder;
        private readonly Func<string, string> _encoder;
        private readonly Dictionary<Operators, Func<IEvaluable, IEvaluable>> _operatorScopeLookupFactory = new Dictionary<Operators, Func<IEvaluable, IEvaluable>>();
        private readonly Dictionary<TToken, Operators> _tokensToOperatorsMap = new Dictionary<TToken, Operators>();

        public OperatorSetBuilder(Func<string, string> encoder, Func<string, string> decoder)
        {
            _encoder = encoder ?? Passthrough;
            _decoder = decoder ?? Passthrough;
            BadSyntaxTokens = new HashSet<TToken>();
            NoOpTokens = new HashSet<TToken>();
            LiteralSequenceBoundsMarkers = new HashSet<TToken>();
            Terminators = new HashSet<TToken>();
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

        public static bool ComparisonPrecedence(Operators arg)
        {
            return
                //Take precedence over logic
                (arg == Operators.And || arg == Operators.Or || arg == Operators.Xor || arg == Operators.Not)
                   //but still process comparisons left to right
                   && (arg != Operators.EqualTo && arg != Operators.NotEqualTo && arg != Operators.GreaterThan && arg != Operators.LessThan && arg != Operators.LessThanOrEqualTo && arg != Operators.LessThanOrEqualTo);
        }

        public OperatorSetBuilder<TToken> And(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.And] =
                x => CreateBinaryChild(x, Operators.And, precedesOperator ?? AndPrecedence, evaluate ?? And);
            _tokensToOperatorsMap[token] = Operators.And;
            return this;
        }

        public OperatorSetBuilder<TToken> BadSyntax(params TToken[] token)
        {
            BadSyntaxTokens.UnionWith(token);
            return this;
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

        public string Encode(string value)
        {
            return _encoder(value);
        }

        public OperatorSetBuilder<TToken> EqualTo(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.EqualTo] =
                x => CreateBinaryChild(x, Operators.EqualTo, precedesOperator ?? ComparisonPrecedence, evaluate ?? Equals);
            _tokensToOperatorsMap[token] = Operators.EqualTo;
            return this;
        }

        public OperatorSetBuilder<TToken> GreaterThan(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.GreaterThan] =
                x => CreateBinaryChild(x, Operators.GreaterThan, precedesOperator ?? ComparisonPrecedence, evaluate ?? GreaterThan);
            _tokensToOperatorsMap[token] = Operators.GreaterThan;
            return this;
        }

        public OperatorSetBuilder<TToken> GreaterThanOrEqualTo(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.GreaterThanOrEqualTo] =
                x => CreateBinaryChild(x, Operators.GreaterThanOrEqualTo, precedesOperator ?? ComparisonPrecedence, evaluate ?? GreaterThanOrEqualTo);
            _tokensToOperatorsMap[token] = Operators.GreaterThanOrEqualTo;
            return this;
        }

        public OperatorSetBuilder<TToken> Ignore(params TToken[] token)
        {
            NoOpTokens.UnionWith(token);
            return this;
        }

        public OperatorSetBuilder<TToken> LessThan(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.LessThan] =
                x => CreateBinaryChild(x, Operators.LessThan, precedesOperator ?? ComparisonPrecedence, evaluate ?? LessThan);
            _tokensToOperatorsMap[token] = Operators.LessThan;
            return this;
        }

        public OperatorSetBuilder<TToken> LessThanOrEqualTo(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.LessThanOrEqualTo] =
                x => CreateBinaryChild(x, Operators.LessThanOrEqualTo, precedesOperator ?? ComparisonPrecedence, evaluate ?? LessThanOrEqualTo);
            _tokensToOperatorsMap[token] = Operators.LessThanOrEqualTo;
            return this;
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

        public OperatorSetBuilder<TToken> Not(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.Not] =
                x => CreateUnaryChild(x, Operators.Not, evaluate ?? Not);
            _tokensToOperatorsMap[token] = Operators.Not;
            return this;
        }

        public OperatorSetBuilder<TToken> NotEqualTo(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.NotEqualTo] =
                x => CreateBinaryChild(x, Operators.NotEqualTo, precedesOperator ?? ComparisonPrecedence, evaluate ?? NotEquals);
            _tokensToOperatorsMap[token] = Operators.NotEqualTo;
            return this;
        }

        public OperatorSetBuilder<TToken> OpenGroup(TToken token)
        {
            OpenGroupToken = token;
            return this;
        }

        public OperatorSetBuilder<TToken> Or(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.Or] =
                x => CreateBinaryChild(x, Operators.Or, precedesOperator ?? OrPrecedence, evaluate ?? Or);
            _tokensToOperatorsMap[token] = Operators.Or;
            return this;
        }

        public OperatorSetBuilder<TToken> Other(Operators @operator, TToken token, Func<IEvaluable, IEvaluable> nodeFactory)
        {
            _operatorScopeLookupFactory[@operator] = nodeFactory;
            _tokensToOperatorsMap[token] = @operator;
            return this;
        }

        public OperatorSetBuilder<TToken> TerminateWith(params TToken[] token)
        {
            Terminators.UnionWith(token);
            return this;
        }

        public OperatorSetBuilder<TToken> Xor(TToken token, Func<Operators, bool> precedesOperator = null, Func<object, object, object> evaluate = null)
        {
            _operatorScopeLookupFactory[Operators.Xor] =
                x => CreateBinaryChild(x, Operators.Xor, precedesOperator ?? XorPrecedence, evaluate ?? Xor);
            _tokensToOperatorsMap[token] = Operators.Xor;
            return this;
        }

        private static object And(object left, object right)
        {
            bool l = (bool)Convert.ChangeType(left, typeof(bool));
            bool r = (bool)Convert.ChangeType(right, typeof(bool));

            return l && r;
        }

        private static bool AndPrecedence(Operators arg)
        {
            return arg != Operators.EqualTo && arg != Operators.NotEqualTo && arg != Operators.GreaterThan && arg != Operators.LessThan && arg != Operators.LessThanOrEqualTo && arg != Operators.LessThanOrEqualTo;
        }

        private static IEvaluable CreateBinaryChild(IEvaluable active, Operators op, Func<Operators, bool> precedesOperator, Func<object, object, object> evaluate)
        {
            BinaryScope<Operators> self;

            //If we could steal an arg...
            if (!active.IsIndivisible)
            {
                BinaryScope<Operators> left = active as BinaryScope<Operators>;
                if (left != null && precedesOperator(left.Operator))
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
                UnaryScope<Operators> unary = active.Parent as UnaryScope<Operators>;

                if (unary != null)
                {
                    unary.Parent = self;
                }
                else
                {
                    BinaryScope<Operators> binary = active.Parent as BinaryScope<Operators>;

                    if (binary != null)
                    {
                        if (binary.Left == active)
                        {
                            binary.Left = self;
                        }
                        else if (binary.Right == active)
                        {
                            binary.Right = self;
                        }
                    }
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

        private new static object Equals(object left, object right)
        {
            string l = left as string;
            string r = right as string;

            if (l != null && r != null)
            {
                return string.Equals(l, r, StringComparison.OrdinalIgnoreCase);
            }

            return object.Equals(l, r);
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

        private static object Not(object operand)
        {
            bool l = (bool)Convert.ChangeType(operand, typeof(bool));

            return !l;
        }

        private static object NotEquals(object left, object right)
        {
            string l = left as string;
            string r = right as string;

            if (l != null && r != null)
            {
                return !string.Equals(l, r, StringComparison.OrdinalIgnoreCase);
            }

            return !object.Equals(l, r);
        }

        private static object Or(object left, object right)
        {
            bool l = (bool)Convert.ChangeType(left, typeof(bool));
            bool r = (bool)Convert.ChangeType(right, typeof(bool));

            return l || r;
        }

        private static bool OrPrecedence(Operators arg)
        {
            return arg != Operators.EqualTo && arg != Operators.NotEqualTo && arg != Operators.GreaterThan && arg != Operators.LessThan && arg != Operators.LessThanOrEqualTo && arg != Operators.LessThanOrEqualTo && arg != Operators.And;
        }

        private static string Passthrough(string arg)
        {
            return arg;
        }

        private static object Xor(object left, object right)
        {
            bool l = (bool)Convert.ChangeType(left, typeof(bool));
            bool r = (bool)Convert.ChangeType(right, typeof(bool));

            return l ^ r;
        }

        private static bool XorPrecedence(Operators arg)
        {
            return arg != Operators.EqualTo && arg != Operators.NotEqualTo && arg != Operators.GreaterThan && arg != Operators.LessThan && arg != Operators.LessThanOrEqualTo && arg != Operators.LessThanOrEqualTo && arg != Operators.And && arg != Operators.Or;
        }
    }
}
