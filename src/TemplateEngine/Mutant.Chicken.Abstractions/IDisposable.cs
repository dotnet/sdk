using System;

namespace Mutant.Chicken.Abstractions
{
    public interface IDisposable<out T> : IDisposable
    {
        T Value { get; }
    }
}
