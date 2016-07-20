namespace Microsoft.TemplateEngine.Abstractions.Engine
{
    public interface IValueReadEventArgs
    {
        string Key { get; set; }

        object Value { get; set; }
    }
}