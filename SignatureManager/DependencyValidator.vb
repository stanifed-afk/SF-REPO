Imports System
Imports System.Reflection
Imports iText.Signatures

Friend NotInheritable Class DependencyValidator
    Private Sub New()
    End Sub

    Public Shared Sub ValidateIText()
        Dim signaturesAssembly = GetType(PdfPKCS7).Assembly
        Dim signaturesVersion = signaturesAssembly.GetName().Version
        Dim adapterAssembly As Assembly

        Try
            adapterAssembly = Assembly.Load("itext.bouncy-castle-adapter")
        Catch ex As Exception
            Throw New InvalidOperationException(
                "Nie można załadować itext.bouncy-castle-adapter. " &
                "Zainstaluj adapter w dokładnie tej samej wersji co itext7. " &
                "Wersja iText.Signatures: " & signaturesVersion.ToString(), ex)
        End Try

        Dim adapterVersion = adapterAssembly.GetName().Version
        If signaturesVersion.Major <> adapterVersion.Major OrElse
           signaturesVersion.Minor <> adapterVersion.Minor OrElse
           signaturesVersion.Build <> adapterVersion.Build Then

            Throw New InvalidOperationException(
                "Niezgodne wersje iText: iText.Signatures=" & signaturesVersion.ToString() &
                ", itext.bouncy-castle-adapter=" & adapterVersion.ToString() &
                ". Usuń bin/obj i zainstaluj oba pakiety w identycznej wersji.")
        End If
    End Sub
End Class
