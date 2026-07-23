namespace NonTestLibrary;

// A plain (non-test) class library referenced by a nested traversal project.
// dotnet test must silently skip it: it is neither IsTestProject nor
// IsTestingPlatformApplication, so it produces no test module and no error.
public static class Class1
{
    public static int Add(int a, int b) => a + b;
}
