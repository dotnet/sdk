Imports System
Imports Xunit

Public Class UnitTest1
    <Fact>
    Sub TestSub()
#If (XUnitVersion == "v3")
        Assert.True(True)
#End If
    End Sub
End Class

