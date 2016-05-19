using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IDisposable<out T> : IDisposable
    {
        T Value { get; }
    }
}
