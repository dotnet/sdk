namespace N3P.StreamReplacer
{
    public static class ValueNode
    {
        public static IValueNode IsDefined(string name)
        {
            return new IsDefinedNode(name);
        }

        public static IValueNode Constant(object value)
        {
            return new ConstantNode(value);
        }

        public static IValueNode Reference(string name, object defaultValue = null)
        {
            return new ReferenceNode(name, defaultValue);
        }

        public static IValueNode Add(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.Add);
        }

        public static IValueNode BitwiseAnd(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.BitwiseAnd);
        }

        public static IValueNode BitwiseOr(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.BitwiseOr);
        }

        public static IValueNode BitwiseXor(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.BitwiseXor);
        }

        public static IValueNode Contains(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.Contains);
        }

        public static IValueNode Divide(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.Divide);
        }

        public static IValueNode EndsWith(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.EndsWith);
        }

        public static IValueNode Equals(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.Equals);
        }

        public static IValueNode GreaterThan(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.GreaterThan);
        }

        public static IValueNode GreaterThanOrEqualTo(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.GreaterThanOrEqualTo);
        }

        public static IValueNode IndexOf(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.IndexOf);
        }

        public static IValueNode LessThan(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.LessThan);
        }

        public static IValueNode LessThanOrEqualTo(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.LessThanOrEqualTo);
        }

        public static IValueNode Log(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.Log);
        }

        public static IValueNode LogicalAnd(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.LogicalAnd);
        }

        public static IValueNode LogicalOr(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.LogicalOr);
        }

        public static IValueNode LogicalXor(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.LogicalXor);
        }

        public static IValueNode Modulus(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.Modulus);
        }

        public static IValueNode Multiply(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.Multiply);
        }

        public static IValueNode NotEqualTo(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.NotEqualTo);
        }

        public static IValueNode Power(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.Power);
        }

        public static IValueNode StartsWith(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.StartsWith);
        }

        public static IValueNode Subtract(IValueNode left, IValueNode right)
        {
            return new BinaryNode(left, right, BinaryOperator.Subtract);
        }
    }
}