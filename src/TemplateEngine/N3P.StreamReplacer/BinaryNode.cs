using System;
using System.Collections.Generic;

namespace N3P.StreamReplacer
{
    internal class BinaryNode : IValueNode
    {
        private readonly IValueNode _left;
        private readonly IValueNode _right;
        private readonly BinaryOperator _op;

        public BinaryNode(IValueNode left, IValueNode right, BinaryOperator op)
        {
            _left = left;
            _right = right;
            _op = op;
        }

        public object ProvideValue(IReadOnlyDictionary<string, object> args)
        {
            object left = _left.ProvideValue(args);
            object right = _right.ProvideValue(args);

            switch (_op)
            {
                case BinaryOperator.Equals:
                    return Equals(left, right);
                case BinaryOperator.NotEqualTo:
                    return !Equals(left, right);
                case BinaryOperator.BitwiseAnd:
                case BinaryOperator.BitwiseOr:
                case BinaryOperator.BitwiseXor:
                    return ProcessBitwiseOperator(left, right, _op);
                case BinaryOperator.Contains:
                case BinaryOperator.StartsWith:
                case BinaryOperator.EndsWith:
                case BinaryOperator.IndexOf:
                    return ProcessStringOperator(left, right, _op);
                case BinaryOperator.LogicalAnd:
                case BinaryOperator.LogicalOr:
                case BinaryOperator.LogicalXor:
                    return ProcessLogicalOperator(left, right, _op);
                case BinaryOperator.GreaterThan:
                case BinaryOperator.GreaterThanOrEqualTo:
                case BinaryOperator.LessThan:
                case BinaryOperator.LessThanOrEqualTo:
                    return ProcessScalarComparisonOperator(left, right, _op);
                case BinaryOperator.Add:
                case BinaryOperator.Subtract:
                case BinaryOperator.Multiply:
                case BinaryOperator.Divide:
                case BinaryOperator.Modulus:
                case BinaryOperator.Power:
                case BinaryOperator.Log:
                    return ProcessScalarOperator(left, right, _op);
                default:
                    return null;
            }
        }

        private object ProcessScalarComparisonOperator(object left, object right, BinaryOperator op)
        {
            throw new NotImplementedException();
        }

        private object ProcessScalarOperator(object left, object right, BinaryOperator op)
        {
            if (left == null || right == null)
            {
                return null;
            }

            sbyte leftWidth, rightWidth;
            ulong leftVal = GetIntegralScalar(left, out leftWidth);
            ulong rightVal = GetIntegralScalar(left, out rightWidth);
            sbyte maxWidth = Math.Max(leftWidth, rightWidth);
            bool signedArithmetic = leftWidth < 0 || rightWidth < 0;
            ulong result;

            if (signedArithmetic)
            {
                result = DoSignedScalarOperator(leftVal, rightVal, maxWidth, op);
            }
            else
            {
                result = DoUnsignedScalarOperator(leftVal, rightVal, maxWidth, op);
            }


            switch (maxWidth)
            {
                case 0:
                    return (result & 0x01) == 1;
                case 1:
                    return signedArithmetic ? (object) (sbyte) (result & 0xFF) : (byte) (result & 0xFF);
                case 2:
                    return signedArithmetic ? (object) (short) (result & 0xFFFF) : (ushort) (result & 0xFFFF);
                case 4:
                    return signedArithmetic ? (object) (int) (result & 0xFFFFFFFF) : (uint) (result & 0xFFFFFFFF);
                default:
                    return signedArithmetic ? (object) result : (long) result;
            }
        }

        private ulong DoUnsignedScalarOperator(ulong leftVal, ulong rightVal, sbyte maxWidth, BinaryOperator op)
        {
            switch (op)
            {
                case BinaryOperator.Add:
                    return (ulong) ((long) leftVal + (long) rightVal);
                case BinaryOperator.Subtract:
                    return (ulong)((long)leftVal - (long)rightVal);
                case BinaryOperator.Multiply:
                    return (ulong)((long)leftVal * (long)rightVal);
                case BinaryOperator.Divide:
                    return (ulong)((long)leftVal / (long)rightVal);
                case BinaryOperator.Modulus:
                    return (ulong)((long)leftVal % (long)rightVal);
                case BinaryOperator.Power:
                    return (ulong) Math.Pow((long) leftVal, (long) rightVal);
                case BinaryOperator.Log:
                    return (ulong)Math.Log((long)leftVal, (long)rightVal);
                default:
                    return 0;
            }
        }

        private ulong DoSignedScalarOperator(ulong leftVal, ulong rightVal, sbyte width, BinaryOperator op)
        {
            throw new NotImplementedException();
        }

        private static object ProcessLogicalOperator(object left, object right, BinaryOperator op)
        {
            if (!(left is bool) || !(right is bool))
            {
                return null;
            }

            bool leftBool = (bool)left;
            bool rightBool = (bool)right;

            switch (op)
            {
                case BinaryOperator.LogicalAnd:
                    return leftBool && rightBool;
                case BinaryOperator.LogicalOr:
                    return leftBool || rightBool;
                case BinaryOperator.LogicalXor:
                    return leftBool ^ rightBool;
                default:
                    return null;
            }
        }

        private static object ProcessStringOperator(object left, object right, BinaryOperator op)
        {
            string leftString = left as string;
            string rightString = right as string;

            if (leftString == null || rightString == null)
            {
                return null;
            }

            switch (op)
            {
                case BinaryOperator.StartsWith:
                    return leftString.StartsWith(rightString);
                case BinaryOperator.EndsWith:
                    return leftString.EndsWith(rightString);
                case BinaryOperator.Contains:
                    return leftString.Contains(rightString);
                case BinaryOperator.IndexOf:
                    return leftString.IndexOf(rightString, StringComparison.Ordinal);
                default:
                    return null;
            }
        }

        private object ProcessBitwiseOperator(object left, object right, BinaryOperator op)
        {
            if (left == null || right == null)
            {
                return null;
            }

            sbyte leftWidth, rightWidth;
            ulong leftVal = GetIntegralScalar(left, out leftWidth);
            ulong rightVal = GetIntegralScalar(left, out rightWidth);
            ulong result;

            leftWidth = Math.Abs(leftWidth);
            rightWidth = Math.Abs(rightWidth);

            switch (op)
            {
                case BinaryOperator.BitwiseAnd:
                    result = leftVal & rightVal;
                    break;
                case BinaryOperator.BitwiseOr:
                    result = leftVal | rightVal;
                    break;
                case BinaryOperator.BitwiseXor:
                    result = leftVal ^ rightVal;
                    break;
                default:
                    return null;
            }

            switch (Math.Max(leftWidth, rightWidth))
            {
                case 0:
                    return (result & 0x01) == 1;
                case 1:
                    return (byte) (result & 0xFF);
                case 2:
                    return (ushort)(result & 0xFFFF);
                case 4:
                    return (uint)(result & 0xFFFFFFFF);
                default:
                    return result;
            }
        }

        private ulong GetIntegralScalar(object o, out sbyte width)
        {
            Type leftType = o.GetType();

            if (leftType == typeof(bool))
            {
                width = 0;
                return (bool)o ? 1UL : 0UL;
            }

            if (leftType == typeof(byte))
            {
                width = 1;
                return (byte)o;
            }

            if (leftType == typeof(sbyte))
            {
                width = -1;
                return (ulong)(sbyte)o;
            }

            if (leftType == typeof(ushort))
            {
                width = 2;
                return (ushort)o;
            }

            if (leftType == typeof(short))
            {
                width = -2;
                return (ulong)(short)o;
            }

            if (leftType == typeof(uint))
            {
                width = 4;
                return (uint)o;
            }

            if (leftType == typeof(int))
            {
                width = -4;
                return (ulong)(int)o;
            }

            if (leftType == typeof(ulong))
            {
                width = 8;
                return (ulong)o;
            }

            if (leftType == typeof(long))
            {
                width = -8;
                return (ulong)(long)o;
            }

            width = 0;
            return 0;
        }
    }
}