namespace Company.TestProject1

open System
open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass>]
type Test1 () =

#if (Fixture == AssemblyInitialize)
    [<AssemblyInitialize>]
    static member AssemblyInit (context: TestContext) =
        // This method is called once for the test assembly, before any tests are run.
        ()

#endif
#if (Fixture == AssemblyCleanup)
    [<AssemblyCleanup>]
    static member AssemblyCleanup () =
        // This method is called once for the test assembly, after all tests are run.
        ()

#endif
#if (Fixture == ClassInitialize)
    [<ClassInitialize>]
    member this.ClassInit (context: TestContext) =
        // This method is called once for the test class, before any tests of the class are run.
        ()

#endif
#if (Fixture == ClassCleanup)
    [<ClassCleanup>]
    member this.ClassCleanup () =
        // This method is called once for the test class, after all tests of the class are run.
        ()

#endif
#if (Fixture == TestInitialize)
    [<TestInitialize>]
    member this.TestInit () =
        // This method is called before each test method.
        ()

#endif
#if (Fixture == TestCleanup)
    [<TestCleanup>]
    member this.TestCleanup () =
        // This method is called after each test method.
        ()

#endif
    [<TestMethod>]
    member this.TestMethodPassing () =
        Assert.IsTrue(true);
