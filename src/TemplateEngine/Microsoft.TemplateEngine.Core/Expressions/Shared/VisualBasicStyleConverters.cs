namespace Microsoft.TemplateEngine.Core.Expressions.Shared
{
    public static class VisualBasicStyleConverters
    {
        public static void ConfigureConverters(ITypeConverter obj)
        {
            obj.Register((object o, out long r) => CoreConverters.TryHexConvert("&H", obj, o, out r) || obj.TryCoreConvert(o, out r))
               .Register((object o, out int r) => CoreConverters.TryHexConvert("&H", obj, o, out r) || obj.TryCoreConvert(o, out r));
        }
    }
}
