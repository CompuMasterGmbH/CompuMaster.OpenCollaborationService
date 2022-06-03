Imports NUnit.Framework
Imports CompuMaster.Ocs

Namespace CompuMaster.Ocs.Test

    Public Class BasicTestsNextCloudAdminUser
        Inherits BasicTests

        Private Settings As New SettingsNextCloudAdminUser

        Protected Overrides ReadOnly Property AuthorizedUserRole As AuthorizedUserRoles
            Get
                Return AuthorizedUserRoles.AdminUser
            End Get
        End Property

        Protected Overrides ReadOnly Property UserIDMustExist As String
            Get
                Return Settings.InputLine("username")
            End Get
        End Property

        Protected Overrides ReadOnly Property IgnoreTestEnvironment As Boolean
            Get
                Return Settings.InputLine("username") = "" OrElse Settings.InputLine("username") = "none" OrElse Settings.InputLine("password") = "" OrElse Settings.InputLine("password") = "none"
            End Get
        End Property

        Protected Overrides Function CreateAuthorizedClientInstance() As Client
            If IgnoreTestEnvironment Then Assert.Ignore("Test environment not available (username or password missing or none)")
            Dim username As String = Settings.InputLine("username")
            Dim serverurl As String = Settings.InputLine("server url")
            Dim password As String = Settings.InputLine("password")
            Return New Client(serverurl, username, password)
        End Function

    End Class

End Namespace