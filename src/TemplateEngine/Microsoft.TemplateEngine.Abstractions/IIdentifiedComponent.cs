using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IIdentifiedComponent
    {
        Guid Id { get; }
    }
}