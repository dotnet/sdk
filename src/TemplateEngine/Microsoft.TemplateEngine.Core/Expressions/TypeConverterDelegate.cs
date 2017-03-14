namespace Microsoft.TemplateEngine.Core.Expressions
{
    public delegate bool TypeConverterDelegate<T>(object source, out T result);
}
