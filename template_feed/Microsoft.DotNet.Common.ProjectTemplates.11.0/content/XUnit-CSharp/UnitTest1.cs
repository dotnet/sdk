#if (!csharpFeature_ImplicitUsings)
using Xunit;

#endif
#if (csharpFeature_FileScopedNamespaces)
namespace Company.TestProject1;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {

    }
}
#else
namespace Company.TestProject1
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {

        }
    }
}
#endif
