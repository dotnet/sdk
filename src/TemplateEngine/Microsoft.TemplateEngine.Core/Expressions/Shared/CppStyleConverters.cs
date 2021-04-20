// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Expressions.Shared
{
    public static class CppStyleConverters
    {
        public static void ConfigureConverters(ITypeConverter obj)
        {
            obj.Register((object o, out long r) => CoreConverters.TryHexConvert("0x", obj, o, out r) || obj.TryCoreConvert(o, out r))
               .Register((object o, out int r) => CoreConverters.TryHexConvert("0x", obj, o, out r) || obj.TryCoreConvert(o, out r));
        }

        public static string Decode(string arg)
        {
            return arg.Replace("\\\"", "\"").Replace("\\'", "'");
        }

        public static string Encode(string arg)
        {
            return arg.Replace("\"", "\\\"").Replace("'", "\\'");
        }
    }
}
