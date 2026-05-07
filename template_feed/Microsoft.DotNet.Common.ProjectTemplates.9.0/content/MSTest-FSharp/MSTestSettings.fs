module MSTestSettings

open Microsoft.VisualStudio.TestTools.UnitTesting

[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do()
