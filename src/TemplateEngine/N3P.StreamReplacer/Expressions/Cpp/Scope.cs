using System;

namespace N3P.StreamReplacer.Expressions.Cpp
{
    internal class Scope
    {
        public object Left { get; set; }

        public Operator Operator { get; set; }

        public object Right { get; set; }

        public object Evaluate()
        {
            switch (Operator)
            {
                case Operator.None:
                    return (Left as Scope)?.Evaluate() ?? (bool?)Left ?? false;
                case Operator.Not:
                    return !(bool)((Left as Scope)?.Evaluate() ?? (bool?)Left ?? false);
                case Operator.And:
                    return (bool)((Left as Scope)?.Evaluate() ?? (bool?)Left ?? false) && (bool)((Right as Scope)?.Evaluate() ?? (bool?)Right ?? false);
                case Operator.Or:
                    return (bool)((Left as Scope)?.Evaluate() ?? (bool?)Left ?? false) || (bool)((Right as Scope)?.Evaluate() ?? (bool?)Right ?? false);
                case Operator.Xor:
                    return (bool)((Left as Scope)?.Evaluate() ?? (bool?)Left ?? false) ^ (bool)((Right as Scope)?.Evaluate() ?? (bool?)Right ?? false);
                case Operator.EqualTo:
                    return Equals((Left as Scope)?.Evaluate() ?? Left, (Right as Scope)?.Evaluate() ?? Right);
                case Operator.NotEqualTo:
                    return !Equals((Left as Scope)?.Evaluate() ?? Left, (Right as Scope)?.Evaluate() ?? Right);
                case Operator.GreaterThan:
                    return Convert.ToInt64((Left as Scope)?.Evaluate() ?? Left) > Convert.ToInt64((Right as Scope)?.Evaluate() ?? Right);
                case Operator.GreaterThanOrEqualTo:
                    return Convert.ToInt64((Left as Scope)?.Evaluate() ?? Left) >= Convert.ToInt64((Right as Scope)?.Evaluate() ?? Right);
                case Operator.LessThan:
                    return Convert.ToInt64((Left as Scope)?.Evaluate() ?? Left) < Convert.ToInt64((Right as Scope)?.Evaluate() ?? Right);
                case Operator.LessThanOrEqualTo:
                    return Convert.ToInt64((Left as Scope)?.Evaluate() ?? Left) <= Convert.ToInt64((Right as Scope)?.Evaluate() ?? Right);
                case Operator.LeftShift:
                    return Convert.ToInt64((Left as Scope)?.Evaluate() ?? Left) << Convert.ToInt32((Right as Scope)?.Evaluate() ?? Right);
                case Operator.RightShift:
                    return Convert.ToInt64((Left as Scope)?.Evaluate() ?? Left) >> Convert.ToInt32((Right as Scope)?.Evaluate() ?? Right);
                default:
                    throw new NotImplementedException();
            }
        }
    }
}