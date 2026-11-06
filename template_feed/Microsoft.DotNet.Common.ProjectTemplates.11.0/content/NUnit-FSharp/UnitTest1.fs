#if (TestRunner == "Microsoft.Testing.Platform")
module Company.TestProject1.Tests
#else
module Company.TestProject1
#endif

open NUnit.Framework

[<SetUp>]
let Setup () =
    ()

[<Test>]
let Test1 () =
    Assert.Pass()
