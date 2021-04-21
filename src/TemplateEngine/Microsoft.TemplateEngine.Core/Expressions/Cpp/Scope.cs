// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core.Expressions.Cpp
{
    internal class Scope
    {
        public enum NextPlacement
        {
            Left,
            Right,
            None
        }

        public object Left { get; set; }

        public Operator Operator { get; set; }

        public object Right { get; set; }

        public object Value
        {
            set
            {
                switch (TargetPlacement)
                {
                    case NextPlacement.Left:
                        Left = value;
                        TargetPlacement = NextPlacement.Right;
                        break;
                    case NextPlacement.Right:
                        Right = value;
                        TargetPlacement = NextPlacement.None;
                        break;
                }
            }
        }

        public NextPlacement TargetPlacement { get; set; }

        public static TResult EvaluateSides<TLeft, TRight, TResult>(object left, object right, Func<object, TLeft> convertLeft, Func<object, TRight> convertRight, Func<TLeft, TRight, TResult> combine)
        {
            TLeft l = EvaluateSide(left, convertLeft);
            TRight r = EvaluateSide(right, convertRight);
            return combine(l, r);
        }

        public static TResult EvaluateSides<T, TResult>(object left, object right, Func<object, T> convert, Func<T, T, TResult> combine)
        {
            return EvaluateSides(left, right, convert, convert, combine);
        }

        public object Evaluate()
        {
            switch (Operator)
            {
                case Operator.Not:
                    return !EvaluateSide(Right, x => Convert.ToBoolean(x ?? "False"));
                case Operator.And:
                    return EvaluateSides(Left, Right, x => (bool)x, (x, y) => x && y);
                case Operator.Or:
                    return EvaluateSides(Left, Right, x => (bool)x, (x, y) => x || y);
                case Operator.Xor:
                    return EvaluateSides(Left, Right, x => (bool)x, (x, y) => x ^ y);
                case Operator.EqualTo:
                    return EvaluateSides(Left, Right, x => x, LenientEquals);
                case Operator.NotEqualTo:
                    return EvaluateSides(Left, Right, x => x, (x, y) => !LenientEquals(x, y));
                case Operator.GreaterThan:
                    return EvaluateSides(Left, Right, ParserExtensions.ConvertToDoubleCurrentOrInvariant, (x, y) => x > y);
                case Operator.GreaterThanOrEqualTo:
                    return EvaluateSides(Left, Right, ParserExtensions.ConvertToDoubleCurrentOrInvariant, (x, y) => x >= y);
                case Operator.LessThan:
                    return EvaluateSides(Left, Right, ParserExtensions.ConvertToDoubleCurrentOrInvariant, (x, y) => x < y);
                case Operator.LessThanOrEqualTo:
                    return EvaluateSides(Left, Right, ParserExtensions.ConvertToDoubleCurrentOrInvariant, (x, y) => x <= y);
                case Operator.LeftShift:
                    return EvaluateSides(Left, Right, Convert.ToInt64, Convert.ToInt32, (x, y) => x << y);
                case Operator.RightShift:
                    return EvaluateSides(Left, Right, Convert.ToInt64, Convert.ToInt32, (x, y) => x >> y);
                case Operator.BitwiseAnd:
                    return EvaluateSides(Left, Right, Convert.ToInt64, (x, y) => x & y);
                case Operator.BitwiseOr:
                    return EvaluateSides(Left, Right, Convert.ToInt64, (x, y) => x | y);
                default:
                    if (Left != null)
                    {
                        return EvaluateSide(Left, x => x);
                    }

                    return false;
            }
        }

        private static T EvaluateSide<T>(object side, Func<object, T> convert)
        {
            if (side is Scope scope)
            {
                return convert(scope.Evaluate());
            }

            return convert(side);
        }

        private static bool LenientEquals(object x, object y)
        {
            if (x is string sx && y is string sy)
            {
                return string.Equals(sx, sy, StringComparison.OrdinalIgnoreCase);
            }

            return Equals(x, y);
        }
    }
}
