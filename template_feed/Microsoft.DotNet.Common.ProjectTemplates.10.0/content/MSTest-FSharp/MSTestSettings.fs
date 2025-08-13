module MSTestSettings

open System.Diagnostics.CodeAnalysis
open Microsoft.VisualStudio.TestTools.UnitTesting

[<assembly: ExcludeFromCodeCoverage>]
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do()
