namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ICacheParameter
    {
        string DataType { get; }

        string DefaultValue { get; }

        string Description { get; }
    }
}
