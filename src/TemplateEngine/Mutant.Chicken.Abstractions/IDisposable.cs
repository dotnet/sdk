using System;

namespace Mutant.Chicken.Abstractions
{
    public interface IDisposable<T> : IDisposable
    {
        T Value { get; }
    }
}
