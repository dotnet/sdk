namespace Company.TestProject1;

[TestClass]
public class Test1 : PageTest
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
    public async Task HomepageHasPlaywrightInTitleAndGetStartedLinkLinkingToTheIntroPage()
    {
        await Page.GotoAsync("https://playwright.dev");

        // Expect a title "to contain" a substring.
        await Expect(Page).ToHaveTitleAsync(new Regex("Playwright"));

        // create a locator
        var getStarted = Page.Locator("text=Get Started");

        // Expect an attribute "to be strictly equal" to the value.
        await Expect(getStarted).ToHaveAttributeAsync("href", "/docs/intro");

        // Click the get started link.
        await getStarted.ClickAsync();

        // Expects the URL to contain intro.
        await Expect(Page).ToHaveURLAsync(new Regex(".*intro"));
    }
}
