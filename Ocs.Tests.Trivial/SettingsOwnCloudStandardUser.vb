Imports System.Text
Imports NUnit.Framework

Namespace CompuMaster.Ocs.Test
    <TestFixture> Public NotInheritable Class SettingsOwnCloudStandardUser
        Inherits SettingsBase

        Public Overrides ReadOnly Property AppTitleInBufferFile As String
            Get
                Return "OwnCloud.StandardUser.Test"
            End Get
        End Property

        Public Overrides ReadOnly Property AppTitleInEnvironmentVariable As String
            Get
                Return "OwnCloud.StandardUser".ToUpperInvariant
            End Get
        End Property

        <Test, Explicit("Run only to persist login credentials on dev workstation")>
        Public Overrides Sub PersistInputValue()
            Dim username As String = InputLine("username")
            Dim serverurl As String = InputLine("server url")
            Dim password As String = InputLine("password")

            System.Console.WriteLine(Me.AppTitleInBufferFile & " Environment " & EnvironmentVariable("USERNAME") & "=" & System.Environment.GetEnvironmentVariable(EnvironmentVariable("USERNAME")))
            System.Console.WriteLine("Environment written to disk for future use at local dev workstation:")
            System.Console.WriteLine("- ServerUrl=" & serverurl)
            System.Console.WriteLine("- Username=" & username)

            If password <> "" Then
                System.Console.WriteLine("- Password=********************")
            Else
                System.Console.WriteLine("- Password=")
            End If

            Assert.NotNull(serverurl, "User credentials not found in environment or buffer files (run Sample app for creating buffer files in temp directory!)")
            Assert.NotNull(username, "User credentials not found in environment or buffer files (run Sample app for creating buffer files in temp directory!)")
            Assert.NotNull(password, "User credentials not found in environment or buffer files (run Sample app for creating buffer files in temp directory!)")
        End Sub

        Public Overrides Function PersitingScriptForRequiredEnvironmentVariables() As String
            Return "@echo off" & vbCrLf &
                "SET " & EnvironmentVariable("SERVERURL") & "=https://cloud.server.url/" & vbCrLf &
                "SET " & EnvironmentVariable("USERNAME") & "=xy@abc.login" & vbCrLf &
                "SET " & EnvironmentVariable("PASSWORD") & "=xxxxxxx(encode with leading ^-char )" & vbCrLf &
                "dotnet test --filter ""FullyQualifiedName=" & Me.GetType.FullName & "." & NameOf(PersistInputValue) & """ --framework net5.0"
        End Function
    End Class
End Namespace