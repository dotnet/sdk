// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace GenAPITaskTestProject
{
    public class Calculator
    {
        public int Add(int a, int b) => a + b;
        public int Subtract(int a, int b) => a - b;

        internal int InternalMultiply(int a, int b) => a * b;
    }

    public interface IShape
    {
        double Area { get; }
    }
}
