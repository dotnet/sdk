#if (!csharpFeature_ImplicitUsings)
using NUnit.Framework;

#endif
#if (csharpFeature_FileScopedNamespaces)
namespace Company.TestProject1;

public class Tests
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
namespace Company.TestProject1
{
    public class Tests
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
