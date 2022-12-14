#if (csharpFeature_FileScopedNamespaces)
namespace Company.ClassLibrary1;
public enum Class1
{

}
#else
namespace Company.ClassLibrary1
{
    public enum Class1
    {

    }
}
#endif
