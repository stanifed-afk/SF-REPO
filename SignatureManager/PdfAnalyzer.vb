Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Security.Cryptography.X509Certificates
Imports System.Text.RegularExpressions
Imports iText.Kernel.Pdf
Imports iText.Signatures

Friend NotInheritable Class PdfAnalyzer
    Private Shared ReadOnly TitlePattern As New Regex("^(?<employee>[0-9]{6})_.*_(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})\.pdf$", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)
    Private Shared ReadOnly PeselPatternTemplate As String = "(?<![0-9]){0}(?![0-9])"

    Private ReadOnly _employees As Dictionary(Of String, List(Of EmployeeRecord))
    Private ReadOnly _logger As Logger

    Public Sub New(employees As Dictionary(Of String, List(Of EmployeeRecord)), logger As Logger)
        _employees = employees
        _logger = logger
    End Sub

    Public Function Analyze(path As String) As ReportRecord
        Dim result = New ReportRecord With {.SourcePath = path, .Status = SignatureStatus.Unreadable}

        Try
            Using reader As New PdfReader(path)
                Using document As New PdfDocument(reader)
                    result.Title = If(document.GetDocumentInfo().GetTitle(), String.Empty)
                    If String.IsNullOrWhiteSpace(result.Title) Then
                        result.Title = System.IO.Path.GetFileName(path)
                        result.Status = SignatureStatus.EmptyTitle
                        Return result
                    End If

                    Dim match = TitlePattern.Match(result.Title)
                    If Not match.Success Then
                        result.Status = SignatureStatus.InvalidTitle
                        Return result
                    End If

                    result.EmployeeNumber = match.Groups("employee").Value
                    Dim parsedDate As DateTime
                    If Not DateTime.TryParseExact(match.Groups("date").Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, parsedDate) Then
                        result.Status = SignatureStatus.InvalidTitle
                        Return result
                    End If
                    result.PdfCreationDate = parsedDate

                    Dim employeeRows As List(Of EmployeeRecord) = Nothing
                    If Not _employees.TryGetValue(result.EmployeeNumber, employeeRows) Then
                        result.Status = SignatureStatus.MissingAkData
                        Return result
                    End If

                    AnalyzeSignatures(document, employeeRows, result)
                    If result.Status = SignatureStatus.Unreadable Then
                        result.Title = System.IO.Path.GetFileName(path)
                    End If
                    Return result
                End Using
            End Using
        Catch ex As Exception
            result.Status = SignatureStatus.Unreadable
            result.Title = System.IO.Path.GetFileName(path)
            _logger.Error("Nie udało się odczytać PDF: " & path, ex)
            Return result
        End Try
    End Function

    Private Sub AnalyzeSignatures(document As PdfDocument, employees As List(Of EmployeeRecord), result As ReportRecord)
        Dim signatures = New SignatureUtil(document)
        Dim employeeCount = 0
        Dim managerCount = 0
        Dim signatureReadError = False
        Dim latestEmployeeDate As Nullable(Of DateTime) = Nothing

        For Each signatureName In signatures.GetSignatureNames()
            Try
                Dim pkcs7 = signatures.ReadSignatureData(signatureName)
                Dim signatureDate = GetBestSignatureDate(signatures, signatureName, pkcs7)
                Dim signingCertificate = pkcs7.GetSigningCertificate()

                If Not signatureDate.HasValue OrElse signingCertificate Is Nothing Then
                    _logger.Warn("Pominięto podpis bez daty lub z certyfikatem nieważnym w chwili podpisania. Plik: " & result.SourcePath)
                    Continue For
                End If

                Using certificate As New X509Certificate2(signingCertificate.GetEncoded())
                    If Not IsCertificateValid(certificate, signatureDate.Value) Then
                        _logger.Warn("Pominięto podpis z certyfikatem nieważnym w chwili podpisania. Plik: " & result.SourcePath)
                        Continue For
                    End If

                    Dim signer = CreateSignerInfo(certificate, signatureDate.Value, pkcs7)
                    If IsEmployeeSigner(signer, employees) Then
                        employeeCount += 1
                        If Not latestEmployeeDate.HasValue OrElse signatureDate.Value > latestEmployeeDate.Value Then
                            latestEmployeeDate = signatureDate.Value
                        End If
                    Else
                        managerCount += 1
                    End If
                End Using
            Catch ex As Exception
                signatureReadError = True
                _logger.Error("Błąd analizy pojedynczego podpisu w pliku: " & result.SourcePath, ex)
            End Try
        Next

        result.SignatureDate = latestEmployeeDate
        If signatureReadError Then
            result.Status = SignatureStatus.Unreadable
        ElseIf employeeCount = 0 AndAlso managerCount = 0 Then
            result.Status = SignatureStatus.MissingBoth
        ElseIf employeeCount = 0 Then
            result.Status = SignatureStatus.MissingEmployee
        ElseIf managerCount = 0 Then
            result.Status = SignatureStatus.MissingManager
        Else
            result.Status = SignatureStatus.Correct
        End If
    End Sub

    Private Shared Function GetBestSignatureDate(util As SignatureUtil, name As String, pkcs7 As PdfPKCS7) As Nullable(Of DateTime)
        Try
            Dim timestamp = pkcs7.GetTimeStampDate()
            If timestamp <> DateTime.MinValue AndAlso pkcs7.VerifyTimestampImprint() Then Return timestamp
        Catch
            ' Brak poprawnego znacznika RFC 3161 - stosujemy kolejne źródło daty.
        End Try

        Dim signingTime = pkcs7.GetSignDate()
        If signingTime <> DateTime.MinValue Then Return signingTime

        Dim dictionary = util.GetSignatureDictionary(name)
        Dim pdfDateString = dictionary.GetAsString(PdfName.M)
        If pdfDateString IsNot Nothing Then
            Try
                Return iText.Kernel.Pdf.PdfDate.Decode(pdfDateString.ToString())
            Catch
                Return Nothing
            End Try
        End If
        Return Nothing
    End Function

    Private Shared Function IsCertificateValid(certificate As X509Certificate2, signingDate As DateTime) As Boolean
        Dim signingTimeUtc = signingDate.ToUniversalTime()
        Return signingTimeUtc >= certificate.NotBefore.ToUniversalTime() AndAlso
               signingTimeUtc <= certificate.NotAfter.ToUniversalTime()
    End Function

    Private Shared Function CreateSignerInfo(certificate As X509Certificate2, signingDate As DateTime, pkcs7 As PdfPKCS7) As SignerInfo
        Dim subject = certificate.Subject
        Dim timestampText = String.Empty
        Try
            Dim timestamp = pkcs7.GetTimeStampDate()
            If timestamp <> DateTime.MinValue Then timestampText = timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        Catch
            ' Pole pozostaje puste, gdy podpis nie zawiera poprawnego znacznika czasu.
        End Try
        Return New SignerInfo With {
            .Country = ReadDnValue(subject, "C"),
            .Pesel = FindElevenDigits(subject),
            .FirstName = ReadDnValue(subject, "GIVENNAME"),
            .LastName = ReadDnValue(subject, "SURNAME"),
            .CommonName = ReadDnValue(subject, "CN"),
            .Organization = ReadDnValue(subject, "O"),
            .Vat = ReadDnValue(subject, "2.5.4.97"),
            .Email = ReadEmail(subject),
            .SignatureDate = signingDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            .TimeStampDate = timestampText,
            .CertificateIssuer = certificate.Issuer,
            .Thumbprint = certificate.Thumbprint,
            .StatusPodpisu = "Certyfikat ważny w chwili podpisania"
        }
    End Function

    Private Shared Function IsEmployeeSigner(signer As SignerInfo, employees As IEnumerable(Of EmployeeRecord)) As Boolean
        Dim searchable = String.Join(" ", {signer.Email, signer.Pesel, signer.CommonName, signer.FirstName, signer.LastName, signer.Organization, signer.CertificateIssuer})
        For Each employee In employees
            If Not String.IsNullOrWhiteSpace(employee.Email) AndAlso searchable.IndexOf(employee.Email, StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
            If employee.Pesel.Length = 11 AndAlso Regex.IsMatch(searchable, String.Format(CultureInfo.InvariantCulture, PeselPatternTemplate, Regex.Escape(employee.Pesel))) Then Return True
        Next
        Return False
    End Function

    Private Shared Function ReadDnValue(subject As String, key As String) As String
        Dim match = Regex.Match(subject, "(?:^|,)\s*" & Regex.Escape(key) & "=([^,]+)", RegexOptions.IgnoreCase)
        Return If(match.Success, match.Groups(1).Value.Trim(), String.Empty)
    End Function

    Private Shared Function ReadEmail(subject As String) As String
        Dim value = ReadDnValue(subject, "E")
        If value.Length = 0 Then value = ReadDnValue(subject, "EMAILADDRESS")
        Return value
    End Function

    Private Shared Function FindElevenDigits(value As String) As String
        Dim match = Regex.Match(value, "(?<![0-9])[0-9]{11}(?![0-9])")
        Return If(match.Success, match.Value, String.Empty)
    End Function

End Class
