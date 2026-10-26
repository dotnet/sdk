namespace TestProject;

[TestClass]
public class UnitTest1
{
	[TestMethod]
	public void TestMethod1()
	{
		Assert.AreEqual(1, 1);
	}

#if NET9_0
	[TestMethod]
	public void TestMethod2()
	{
		Assert.AreEqual(1, 1);
	}
#endif

	[TestMethod]
	public void TestMethod3()
	{
		Assert.AreEqual(1, 0);
	}
}