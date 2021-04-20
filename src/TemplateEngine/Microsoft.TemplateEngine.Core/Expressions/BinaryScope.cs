// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Core.Expressions
{
    public class BinaryScope<TOperator> : IEvaluable
    {
        private readonly Func<object, object, object> _evaluate;

        public BinaryScope(IEvaluable parent, TOperator @operator, Func<object, object, object> evaluate)
        {
            Parent = parent;
            Operator = @operator;
            _evaluate = evaluate;
        }

        public bool IsFull => Left != null && Right != null;

        public bool IsIndivisible => false;

        public IEvaluable Left { get; set; }

        public TOperator Operator { get; }

        public IEvaluable Parent { get; set; }

        public IEvaluable Right { get; set; }

        public object Evaluate()
        {
            object left = Left.Evaluate();
            object right = Right.Evaluate();
            return _evaluate(left, right);
        }

        public override string ToString()
        {
            return $@"({Left} -{Operator}- {Right})";
        }

        public bool TryAccept(IEvaluable child)
        {
            if (Left == null)
            {
                Left = child;
                return true;
            }

            if (Right == null)
            {
                Right = child;
                return true;
            }

            return false;
        }
    }
}
