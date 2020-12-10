using System;
using System.Reflection;
#if !NETFULL
using System.Runtime.Loader;
#endif


namespace Microsoft.TemplateEngine.Edge
{
    public static class TypeEx
    {
        public static Type GetType(this string typeName)
        {
            int commaIndex = typeName.IndexOf(',');
            if (commaIndex < 0)
            {
                return Type.GetType(typeName);
            }

            string asmName = typeName.Substring(commaIndex + 1).Trim();

            if (!ReflectionLoadProbingPath.HasLoaded(asmName))
            {
                AssemblyName name = new AssemblyName(asmName);
#if !NETFULL
                AssemblyLoadContext.Default.LoadFromAssemblyName(name);
#else
                AppDomain.CurrentDomain.Load(name);
#endif
            }

            return Type.GetType(typeName);
        }
    }
}
