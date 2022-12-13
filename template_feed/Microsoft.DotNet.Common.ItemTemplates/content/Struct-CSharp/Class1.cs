#if (!csharpFeature_ImplicitUsings)
using System;

#endif
#if (csharpFeature_FileScopedNamespaces)
namespace Company.ClassLibrary1;
public struct Class1
{

}
#else
namespace Company.ClassLibrary1
{
    public struct Class1
    {

    }
}
#endif
