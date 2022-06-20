Imports NUnit.Framework
Imports CompuMaster.Ocs

Namespace CompuMaster.Ocs.Test

    Public MustInherit Class BasicTests

        <SetUp>
        Public Sub Setup()
        End Sub

        Protected MustOverride Function CreateAuthorizedClientInstance() As OcsClient
        Protected MustOverride ReadOnly Property AuthorizedUserRole As AuthorizedUserRoles
        Protected MustOverride ReadOnly Property UserIDMustExist As String
        ''' <summary>
        ''' Depending on testing environment, higher priviledges might not be available and then require ignoring
        ''' </summary>
        ''' <returns></returns>
        Protected MustOverride ReadOnly Property IgnoreTestEnvironment As Boolean

        Protected Enum AuthorizedUserRoles As Byte
            StandardUser = 0
            SubAdminUser = 10
            AdminUser = 100
        End Enum

        <Test>
        Public Sub Login()
            Dim c As OcsClient = Me.CreateAuthorizedClientInstance()
            System.Console.WriteLine("## Instance")
            System.Console.WriteLine("BaseUrl=" & c.BaseUrl)
            System.Console.WriteLine("WebDavBaseUrl=" & c.WebDavBaseUrl)
            System.Console.WriteLine()
            System.Console.WriteLine("## User")
            System.Console.WriteLine(c.AuthorizedUserID)
            System.Console.WriteLine()
            System.Console.WriteLine("## Config")
            Dim ServerConfig = c.GetConfig
            System.Console.WriteLine("Website=" & ServerConfig.Website)
            System.Console.WriteLine("Host=" & ServerConfig.Host)
            System.Console.WriteLine("Ssl=" & ServerConfig.Ssl)
            System.Console.WriteLine("Contact=" & ServerConfig.Contact)
            System.Console.WriteLine("Version=" & ServerConfig.Version)
            Assert.GreaterOrEqual(New Version(1, 7), Version.Parse(ServerConfig.Version))
        End Sub

        <Test> Public Sub GetUserAttributes()
            Dim c As OcsClient = Me.CreateAuthorizedClientInstance()
            Dim User = c.GetUserAttributes(Me.UserIDMustExist)
            Assert.NotNull(User)
            System.Console.WriteLine("## User " & Me.UserIDMustExist)
            System.Console.WriteLine("EMail=" & User.EMail)
            System.Console.WriteLine("DisplayName=" & User.DisplayName)
            System.Console.WriteLine("Enabled=" & User.Enabled)
            System.Console.WriteLine("Quota.Total=" & User.Quota.Total)
            System.Console.WriteLine("Quota.Used=" & User.Quota.Used)
            System.Console.WriteLine("Quota.Free=" & User.Quota.Free)
            System.Console.WriteLine("Quota.Relative=" & User.Quota.Relative)
            Assert.IsNotEmpty(User.EMail)
            Assert.IsNotEmpty(User.DisplayName)
            If User.Enabled.HasValue Then Assert.IsTrue(User.Enabled)
        End Sub

        <Test> Public Sub GetUsers()
            Dim c As OcsClient = Me.CreateAuthorizedClientInstance()
            Try
                Dim Users As String()
                Users = c.SearchUsers().ToArray
                System.Console.WriteLine("## All Users")
                System.Console.WriteLine(Strings.Join(Users, System.Environment.NewLine))
                Assert.NotZero(Users.Length)

                Dim CheckUser As String
                CheckUser = Me.UserIDMustExist.Substring(0, 6)
                Users = c.SearchUsers(CheckUser).ToArray
                System.Console.WriteLine("## Searched Users: " & CheckUser)
                System.Console.WriteLine(Strings.Join(Users, System.Environment.NewLine))
                Assert.NotZero(Users.Length)

                CheckUser = Me.UserIDMustExist
                Users = c.SearchUsers(CheckUser).ToArray
                System.Console.WriteLine("## Searched Users: " & CheckUser)
                System.Console.WriteLine(Strings.Join(Users, System.Environment.NewLine))
                Assert.NotZero(Users.Length)

                Users = c.SearchUsers("*").ToArray
                System.Console.WriteLine("## Searched Users: *")
                System.Console.WriteLine(Strings.Join(Users, System.Environment.NewLine))
                Assert.Zero(Users.Length)

                Users = c.SearchUsers("").ToArray
                System.Console.WriteLine("## Searched Users: {empty string}")
                System.Console.WriteLine(Strings.Join(Users, System.Environment.NewLine))
                Assert.NotZero(Users.Length)

                Users = c.SearchUsers(" ").ToArray
                System.Console.WriteLine("## Searched Users: {empty string}")
                System.Console.WriteLine(Strings.Join(Users, System.Environment.NewLine))
                Assert.NotZero(Users.Length)
            Catch ex As CompuMaster.Ocs.Exceptions.OcsResponseException
                Select Case Me.AuthorizedUserRole
                    Case AuthorizedUserRoles.AdminUser, AuthorizedUserRoles.SubAdminUser
                        Throw New Exception("Expected action to be authorized for user: " & c.AuthorizedUserID, ex)
                    Case AuthorizedUserRoles.StandardUser
                        'expected exception: not authorized
                    Case Else
                        Throw New NotImplementedException
                End Select
            End Try
        End Sub

        <Test> Public Sub GetApps()
            Dim c As OcsClient = Me.CreateAuthorizedClientInstance()
            Try
                System.Console.WriteLine("## Apps")
                System.Console.WriteLine(Strings.Join(c.GetApps.ToArray, System.Environment.NewLine))
                Assert.NotZero(c.GetApps.Count)
            Catch ex As CompuMaster.Ocs.Exceptions.OcsResponseException
                Select Case Me.AuthorizedUserRole
                    Case AuthorizedUserRoles.AdminUser
                        Throw New Exception("Expected action to be authorized", ex)
                    Case AuthorizedUserRoles.StandardUser
                        'expected exception: not authorized
                    Case Else
                        Throw New NotImplementedException
                End Select
            End Try
        End Sub

        ''' <summary>
        ''' Indent a string
        ''' </summary>
        ''' <param name="value"></param>
        ''' <returns></returns>
        Protected Shared Function Indent(value As String) As String
            If (String.IsNullOrWhiteSpace(value)) Then
                Return String.Empty
            Else
                Dim Result As String = "    " + value.Replace(ControlChars.Lf, ControlChars.Lf & "    ")
                If (Result.EndsWith(ControlChars.Lf & "    ")) Then
                    Result = Result.Substring(0, Result.Length - ((ControlChars.Lf & "    ").Length - 1))
                End If
                Return Result
            End If
        End Function

        Protected Function TestAssembly() As System.Reflection.Assembly
            Return System.Reflection.Assembly.GetExecutingAssembly
        End Function

    End Class

End Namespace