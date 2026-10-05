Imports System

Public NotInheritable Class SignatureStatus
    Public Const MissingManager As String = "Brak podpisu kierownika"
    Public Const MissingEmployee As String = "Brak podpisu pracownika"
    Public Const MissingBoth As String = "Brak podpisów"
    Public Const EmptyTitle As String = "Pusty tytuł"
    Public Const InvalidTitle As String = "Błędny tytuł"
    Public Const Correct As String = "Poprawny"
    Public Const Unreadable As String = "Brak odczytu"
    Public Const MissingAkData As String = "Brak danych w AK"

    Private Sub New()
    End Sub
End Class

Public Class SignerInfo
    Public Property Country As String
    Public Property Pesel As String
    Public Property FirstName As String
    Public Property LastName As String
    Public Property CommonName As String
    Public Property Organization As String
    Public Property Vat As String
    Public Property Email As String
    Public Property SignatureDate As String
    Public Property TimeStampDate As String
    Public Property Reason As String
    Public Property PzId As String
    Public Property CertificateIssuer As String
    Public Property Thumbprint As String
    Public Property StatusPodpisu As String
End Class

Friend Class EmployeeRecord
    Public Property EmployeeNumber As String
    Public Property Email As String
    Public Property Pesel As String
End Class

Friend Class ReportRecord
    Public Property SourcePath As String
    Public Property EmployeeNumber As String
    Public Property PdfCreationDate As Nullable(Of DateTime)
    Public Property Title As String
    Public Property Status As String
    Public Property SignatureDate As Nullable(Of DateTime)
End Class
