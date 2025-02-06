#if (ImplicitUsings != "enable")
using NUnit.Framework;

#endif
#if (csharpFeature_FileScopedNamespaces)
namespace Tests;

public class UnitTest1
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        Assert.Pass();
    }
}
#else
namespace Tests
{
    public class UnitTest1
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            Assert.Pass();
        }
    }
}
#endif
