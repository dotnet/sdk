namespace TestProject;

[TestClass]
public class UnitTest1
{
	[TestMethod]
	public void TestMethod1()
	{
	}

#if NET11_0
	[TestMethod]
	public void TestMethod2()
	{
	}
#endif

	[TestMethod]
	public void TestMethod3()
	{
		Assert.Fail();
	}
}