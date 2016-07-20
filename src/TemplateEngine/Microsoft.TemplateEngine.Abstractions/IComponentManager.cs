using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IComponentManager
    {
        bool TryGetComponent<T>(Guid id, out T component)
            where T : class, IIdentifiedComponent;

        IEnumerable<T> OfType<T>()
            where T : class, IIdentifiedComponent;

        void Register(Type type);
    }
}