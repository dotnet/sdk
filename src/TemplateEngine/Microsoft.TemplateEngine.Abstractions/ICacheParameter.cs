namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ICacheParameter
    {
        string DataType { get; set; }

        string DefaultValue { get; set; }

        string Description { get; set; }
    }
}
