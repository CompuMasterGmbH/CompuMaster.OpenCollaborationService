Imports System.Text
Imports NUnit.Framework

Namespace CompuMaster.Ocs.Test
    <TestFixture> Public MustInherit Class SettingsBase

        Public MustOverride ReadOnly Property AppTitleInBufferFile As String
        Public MustOverride ReadOnly Property AppTitleInEnvironmentVariable As String

        Protected Function EnvironmentVariable(key As String) As String
            Return "TEST_" & AppTitleInEnvironmentVariable & "_" & key.ToUpperInvariant
        End Function

        Public MustOverride Sub PersistInputValue()

        Public Function InputLine(ByVal fieldName As String) As String
            Dim BufferFile As String = BufferFilePath(fieldName)
            Dim EnvVarName As String = EnvironmentVariable(fieldName.Replace(" ", "").Replace(".", "").ToUpperInvariant())

            If Not String.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(EnvVarName)) Then

                If System.Environment.MachineName.StartsWith("WKS") Then
                    PersistInputValue(fieldName, System.Environment.GetEnvironmentVariable(EnvVarName))
                End If

                Return System.Environment.GetEnvironmentVariable(EnvVarName)
            End If

            Dim DefaultValue As String

            If System.IO.File.Exists(BufferFile) Then
                DefaultValue = System.IO.File.ReadAllText(BufferFile)
            Else
                DefaultValue = ""
            End If

            If Not String.IsNullOrWhiteSpace(DefaultValue) Then Return DefaultValue
            Throw New InvalidOperationException("Missing persisted input for field """ & fieldName & """, use environment variable " & EnvVarName & " or write to disk by code with method PersistInputValue()" & vbCrLf &
                                            "Ex. run following customized batch to create local temp-files-cache for credentials (works on WKSxxxx workstations only):" & vbCrLf &
                                            PersitingScriptForRequiredEnvironmentVariables())
        End Function

        Public MustOverride Function PersitingScriptForRequiredEnvironmentVariables() As String

        Private Function BufferFilePath(ByVal fieldName As String) As String
            Dim HashedFieldName As String

            Using md5 As System.Security.Cryptography.MD5 = System.Security.Cryptography.MD5.Create()
                Dim inputBytes As Byte() = System.Text.Encoding.ASCII.GetBytes(fieldName)
                Dim hashBytes As Byte() = md5.ComputeHash(inputBytes)
                Dim sb As New System.Text.StringBuilder()

                For i As Integer = 0 To hashBytes.Length - 1
                    sb.Append(hashBytes(i).ToString("X2"))
                Next

                HashedFieldName = sb.ToString()
            End Using

            Return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "~Buffer." & AppTitleInBufferFile & "." & HashedFieldName & ".tmp")
        End Function

        Private Sub PersistInputValue(ByVal fieldName As String, ByVal value As String)
            System.IO.File.WriteAllText(BufferFilePath(fieldName), value)
        End Sub

        Private Function InputFromBufferFile(ByVal fieldName As String) As String
            Dim BufferFile As String = BufferFilePath(fieldName)

            If System.IO.File.Exists(BufferFile) Then
                Return System.IO.File.ReadAllText(BufferFile)
            Else
                Return Nothing
            End If
        End Function

        Private Sub InputToBufferFile(ByVal fieldName As String, ByVal value As String)
            System.IO.File.WriteAllText(BufferFilePath(fieldName), value)
        End Sub

    End Class
End Namespace