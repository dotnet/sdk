namespace Company.TestProject1;

[TestClass]
public sealed class Test1
{
#if (Fixture == AssemblyInitialize)
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        // This method is called once for the test assembly, before any tests are run.
    }

#endif
#if (Fixture == AssemblyCleanup)
    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        // This method is called once for the test assembly, after all tests are run.
    }

#endif
#if (Fixture == ClassInitialize)
    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // This method is called once for the test class, before any tests of the class are run.
    }

#endif
#if (Fixture == ClassCleanup)
    [ClassCleanup]
    public static void ClassCleanup()
    {
        // This method is called once for the test class, after all tests of the class are run.
    }

#endif
#if (Fixture == TestInitialize)
    [TestInitialize]
    public void TestInit()
    {
        // This method is called before each test method.
    }

#endif
#if (Fixture == TestCleanup)
    [TestCleanup]
    public void TestCleanup()
    {
        // This method is called after each test method.
    }

#endif
    [TestMethod]
    public void TestMethod1()
    {
    }
}
