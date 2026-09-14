Imports Microsoft.VisualStudio.TestTools.UnitTesting

Namespace Company.TestProject1
    <TestClass>
    Public Class Test1
#If (Fixture == AssemblyInitialize)
        <AssemblyInitialize>
        Public Shared Sub AssemblyInit(ByVal testContext As TestContext)
            ' This method is called once for the test assembly, before any tests are run.
        End Sub

#End If
#If (Fixture == AssemblyCleanup)
        <AssemblyCleanup>
        Public Shared Sub AssemblyCleanup()
            ' This method is called once for the test assembly, after all tests are run.
        End Sub

#End If
#If (Fixture == ClassInitialize)
        <ClassInitialize>
        Public Shared Sub ClassInit(ByVal testContext As TestContext)
            ' This method is called once for the test class, before any tests of the class are run.
        End Sub

#End If
#If (Fixture == ClassCleanup)
        <ClassCleanup>
        Public Shared Sub ClassCleanup()
            ' This method is called once for the test class, after all tests of the class are run.
        End Sub

#End If
#If (Fixture == TestInitialize)
        <TestInitialize>
        Public Sub TestInit()
            ' This method is called before each test method.
        End Sub

#End If
#If (Fixture == TestCleanup)
        <TestCleanup>
        Public Sub TestCleanup()
            ' This method is called after each test method.
        End Sub

#End If
        <TestMethod>
        Sub TestSub()

        End Sub
    End Class
End Namespace

