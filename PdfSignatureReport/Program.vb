Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports ClosedXML.Excel
Imports iTextSharp.text.pdf
Imports iTextSharp.text.pdf.security
Imports Org.BouncyCastle.Asn1.X509
Imports Org.BouncyCastle.X509

Module Program
    Private Const Separator As String = " | "

    Sub Main(args As String())
        If args.Length <> 2 Then
            ShowUsage()
            Environment.ExitCode = 1
            Return
        End If

        Dim pdfFolder = Path.GetFullPath(args(0))
        Dim outputPath = Path.GetFullPath(args(1))

        If Not outputPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) Then
            Console.Error.WriteLine("Drugi parametr musi wskazywać plik wynikowy z rozszerzeniem .xlsx.")
            Environment.ExitCode = 1
            Return
        End If

        EnsureOutputDirectoryExists(outputPath)

        Dim logPath = BuildLogPath(outputPath)
        Dim logger As New ErrorLogger(logPath)

        Try
            logger.LogInfo("START", String.Empty, String.Empty, "Rozpoczęto przetwarzanie katalogu PDF: " & pdfFolder)

            Dim rows = ReadPdfFiles(pdfFolder, logger).ToList()
            SaveReport(rows, outputPath)

            logger.LogInfo("KONIEC", String.Empty, String.Empty, "Zakończono przetwarzanie. Liczba wierszy raportu: " & rows.Count.ToString())

            Console.WriteLine("Katalog PDF: {0}", pdfFolder)
            Console.WriteLine("Przetworzono plików PDF: {0}", rows.Count)
            Console.WriteLine("Zapisano raport: {0}", outputPath)
            Console.WriteLine("Zapisano log: {0}", logPath)
        Catch ex As Exception
            logger.LogError("Inne błędy", String.Empty, String.Empty, "Nieobsłużony błąd programu.", ex)
            Console.Error.WriteLine("Wystąpił błąd programu: {0}", ex.Message)
            Console.Error.WriteLine("Szczegóły zapisano w logu: {0}", logPath)
            Environment.ExitCode = 1
        End Try
    End Sub

    Private Sub ShowUsage()
        Console.Error.WriteLine("Użycie:")
        Console.Error.WriteLine("  PdfSignatureReport.exe <ścieżka_do_katalogu_PDF> <ścieżka_do_pliku_wynikowego_xlsx>")
        Console.Error.WriteLine()
        Console.Error.WriteLine("Przykład:")
        Console.Error.WriteLine("  PdfSignatureReport.exe ""C:\plikiPDF"" ""C:\raporty\RaportPodpisowPDF.xlsx""")
    End Sub

    Private Sub EnsureOutputDirectoryExists(outputPath As String)
        Dim outputDirectory = Path.GetDirectoryName(outputPath)

        If Not String.IsNullOrWhiteSpace(outputDirectory) AndAlso Not Directory.Exists(outputDirectory) Then
            Directory.CreateDirectory(outputDirectory)
        End If
    End Sub

    Private Function BuildLogPath(outputPath As String) As String
        Dim outputDirectory = Path.GetDirectoryName(outputPath)

        If String.IsNullOrWhiteSpace(outputDirectory) Then
            outputDirectory = Environment.CurrentDirectory
        End If

        Return Path.Combine(outputDirectory, "log.txt")
    End Function

    Private Function ReadPdfFiles(folderPath As String, logger As ErrorLogger) As IEnumerable(Of PdfReportRow)
        Dim rows As New List(Of PdfReportRow)()

        If Not Directory.Exists(folderPath) Then
            Dim message = "Nie znaleziono katalogu: " & folderPath
            rows.Add(New PdfReportRow With {
                .FileName = "Brak katalogu",
                .ReadStatus = message
            })
            logger.LogError("Błędy odczytu plików PDF", folderPath, String.Empty, message)
            Return rows
        End If

        Dim pdfPaths As List(Of String)
        Try
            pdfPaths = Directory.EnumerateFiles(folderPath, "*.pdf", SearchOption.TopDirectoryOnly).OrderBy(Function(path) path).ToList()
        Catch ex As Exception
            Dim message = "Nie można odczytać listy plików PDF z katalogu: " & folderPath
            rows.Add(New PdfReportRow With {
                .FileName = "Błąd katalogu",
                .ReadStatus = message & ": " & ex.Message
            })
            logger.LogError("Błędy odczytu plików PDF", folderPath, String.Empty, message, ex)
            Return rows
        End Try

        For Each pdfPath In pdfPaths
            rows.Add(ReadSinglePdf(pdfPath, logger))
        Next

        Return rows
    End Function

    Private Function ReadSinglePdf(pdfPath As String, logger As ErrorLogger) As PdfReportRow
        Dim reportRow As New PdfReportRow With {
            .FileName = Path.GetFileName(pdfPath),
            .ReadStatus = "OK"
        }

        Try
            Using reader As New PdfReader(pdfPath)
                Dim fields = reader.AcroFields
                Dim signatureNames = fields.GetSignatureNames().Cast(Of String)().ToList()
                reportRow.SignatureCount = signatureNames.Count

                For Each signatureName In signatureNames
                    Dim signatureInfo = ReadSignature(fields, signatureName, reportRow.FileName, logger)
                    reportRow.Signatures.Add(signatureInfo)
                Next

                reportRow.DuplicateSignatures = FindDuplicateSigners(reportRow.Signatures)
                If Not String.IsNullOrWhiteSpace(reportRow.DuplicateSignatures) Then
                    logger.LogWarning("Zdublowane podpisy", reportRow.FileName, String.Empty, reportRow.DuplicateSignatures)
                End If
            End Using
        Catch ex As Exception
            reportRow.ReadStatus = "Błąd odczytu: " & ex.Message
            logger.LogError("Błędy odczytu plików PDF", reportRow.FileName, String.Empty, "Nie można odczytać pliku PDF.", ex)
        End Try

        Return reportRow
    End Function

    Private Function ReadSignature(fields As AcroFields, signatureName As String, fileName As String, logger As ErrorLogger) As SignatureInfo
        Dim info As New SignatureInfo With {
            .FieldName = signatureName
        }

        Try
            Dim pkcs7 As PdfPKCS7 = fields.VerifySignature(signatureName)
            info.SignDate = pkcs7.SignDate
            info.CoversWholeDocument = fields.SignatureCoversWholeDocument(signatureName)
            info.SignatureType = ResolveSignatureType(fields, signatureName)
            info.SignatureValid = pkcs7.Verify()

            If Not info.SignatureValid Then
                Dim message = "Podpis nie przeszedł weryfikacji kryptograficznej."
                info.ErrorMessage = AppendMessage(info.ErrorMessage, message)
                logger.LogWarning("Błędne podpisy", fileName, signatureName, message)
            End If

            Dim certificate As X509Certificate = pkcs7.SigningCertificate
            If certificate IsNot Nothing Then
                Try
                    FillSignerData(info, certificate)
                    ValidateCertificate(info, certificate, fileName, signatureName, logger)
                Catch ex As Exception
                    Dim message = "Nie można odczytać danych certyfikatu podpisu."
                    info.CertificateStatus = "Błędny certyfikat"
                    info.ErrorMessage = AppendMessage(info.ErrorMessage, message & " " & ex.Message)
                    logger.LogError("Błędne certyfikaty", fileName, signatureName, message, ex)
                End Try
            Else
                Dim message = "Brak certyfikatu podpisującego w podpisie."
                info.CertificateStatus = "Brak certyfikatu"
                info.ErrorMessage = AppendMessage(info.ErrorMessage, message)
                logger.LogWarning("Błędne certyfikaty", fileName, signatureName, message)
            End If
        Catch ex As Exception
            info.SignatureType = "Nie można zweryfikować/odczytać podpisu"
            info.ErrorMessage = AppendMessage(info.ErrorMessage, ex.Message)
            logger.LogError("Błędne podpisy", fileName, signatureName, "Nie można zweryfikować lub odczytać podpisu.", ex)
        End Try

        Return info
    End Function

    Private Sub ValidateCertificate(info As SignatureInfo, certificate As X509Certificate, fileName As String, signatureName As String, logger As ErrorLogger)
        Dim validationDate = If(info.SignDate.HasValue, info.SignDate.Value, DateTime.Now)

        If validationDate < certificate.NotBefore OrElse validationDate > certificate.NotAfter Then
            Dim message = String.Format("Certyfikat nieważny dla daty {0:yyyy-MM-dd HH:mm:ss}. Ważny od {1:yyyy-MM-dd HH:mm:ss} do {2:yyyy-MM-dd HH:mm:ss}.", validationDate, certificate.NotBefore, certificate.NotAfter)
            info.CertificateStatus = "Nieważny certyfikat"
            info.ErrorMessage = AppendMessage(info.ErrorMessage, message)
            logger.LogWarning("Nieważne certyfikaty", fileName, signatureName, message)
        Else
            info.CertificateStatus = "Ważny dla daty podpisu"
        End If
    End Sub

    Private Function AppendMessage(currentMessage As String, newMessage As String) As String
        If String.IsNullOrWhiteSpace(currentMessage) Then
            Return newMessage
        End If

        If String.IsNullOrWhiteSpace(newMessage) Then
            Return currentMessage
        End If

        Return currentMessage & Separator & newMessage
    End Function

    Private Sub FillSignerData(info As SignatureInfo, certificate As X509Certificate)
        Dim subject = certificate.SubjectDN
        Dim subjectValues = ReadDistinguishedName(subject)
        Dim allSubjectText = String.Join(" ", subjectValues.SelectMany(Function(pair) pair.Value))

        info.Subject = subject.ToString()
        Dim serialNumber = FindByOid(subjectValues, "2.5.4.5")
        Dim organizationIdentifier = FindByOid(subjectValues, "2.5.4.97")

        info.Pesel = FirstNonEmpty(FindPesel(serialNumber), FindPesel(allSubjectText), serialNumber)
        info.Vat = FirstNonEmpty(FindVatOrNip(organizationIdentifier), organizationIdentifier, FindVatOrNip(allSubjectText))
        info.Surname = FirstNonEmpty(FindByOid(subjectValues, "2.5.4.4"), ExtractSurnameFromCommonName(FindByOid(subjectValues, "2.5.4.3")))
        info.GivenName = FirstNonEmpty(FindByOid(subjectValues, "2.5.4.42"), ExtractGivenNameFromCommonName(FindByOid(subjectValues, "2.5.4.3")))
        info.CommonName = FindByOid(subjectValues, "2.5.4.3")
    End Sub

    Private Function ReadDistinguishedName(name As X509Name) As Dictionary(Of String, List(Of String))
        Dim values As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
        Dim oidList = name.GetOidList()
        Dim valueList = name.GetValueList()

        For index = 0 To oidList.Count - 1
            Dim oid = oidList(index).ToString()
            Dim value = valueList(index).ToString()

            If Not values.ContainsKey(oid) Then
                values(oid) = New List(Of String)()
            End If

            values(oid).Add(value)
        Next

        Return values
    End Function

    Private Function FindByOid(values As Dictionary(Of String, List(Of String)), oid As String) As String
        If values.ContainsKey(oid) Then
            Return String.Join(", ", values(oid).Where(Function(value) Not String.IsNullOrWhiteSpace(value)))
        End If

        Return String.Empty
    End Function

    Private Function FindPesel(text As String) As String
        Dim matches = Regex.Matches(text, "(?<!\d)\d{11}(?!\d)")
        Return String.Join(", ", matches.Cast(Of Match)().Select(Function(match) match.Value).Distinct())
    End Function

    Private Function FindVatOrNip(text As String) As String
        Dim values As New List(Of String)()

        For Each match As Match In Regex.Matches(text, "VAT[A-Z]{2}[- ]?[A-Z0-9]+", RegexOptions.IgnoreCase)
            values.Add(match.Value)
        Next

        For Each match As Match In Regex.Matches(text, "(?:NIP|TIN|VATIN|VAT|PL)[- :]*([0-9]{10})", RegexOptions.IgnoreCase)
            values.Add(match.Value)
        Next

        Return String.Join(", ", values.Distinct(StringComparer.OrdinalIgnoreCase))
    End Function

    Private Function ExtractSurnameFromCommonName(commonName As String) As String
        If String.IsNullOrWhiteSpace(commonName) Then
            Return String.Empty
        End If

        Dim parts = commonName.Split(" "c).Where(Function(part) Not String.IsNullOrWhiteSpace(part)).ToList()
        If parts.Count >= 2 Then
            Return parts.Last()
        End If

        Return String.Empty
    End Function

    Private Function ExtractGivenNameFromCommonName(commonName As String) As String
        If String.IsNullOrWhiteSpace(commonName) Then
            Return String.Empty
        End If

        Dim parts = commonName.Split(" "c).Where(Function(part) Not String.IsNullOrWhiteSpace(part)).ToList()
        If parts.Count >= 2 Then
            Return String.Join(" ", parts.Take(parts.Count - 1))
        End If

        Return commonName
    End Function

    Private Function ResolveSignatureType(fields As AcroFields, signatureName As String) As String
        Dim subFilter = "nieznany"
        Dim isCertification = False
        Dim item = fields.GetFieldItem(signatureName)

        If item IsNot Nothing Then
            Dim signatureDictionary = item.GetMerged(0).GetAsDict(PdfName.V)
            If signatureDictionary IsNot Nothing Then
                Dim subFilterName = signatureDictionary.GetAsName(PdfName.SUBFILTER)
                If subFilterName IsNot Nothing Then
                    subFilter = subFilterName.ToString()
                End If

                Dim references = signatureDictionary.GetAsArray(PdfName.REFERENCE)
                If references IsNot Nothing Then
                    For index = 0 To references.Size - 1
                        Dim referenceDictionary = references.GetAsDict(index)
                        If referenceDictionary IsNot Nothing AndAlso PdfName.DOCMDP.Equals(referenceDictionary.GetAsName(PdfName.TRANSFORMMETHOD)) Then
                            isCertification = True
                        End If
                    Next
                End If
            End If
        End If

        If isCertification Then
            Return "Podpis certyfikujący (SubFilter: " & subFilter & ")"
        End If

        Return "Podpis zatwierdzający/akceptacyjny (SubFilter: " & subFilter & ")"
    End Function

    Private Function FindDuplicateSigners(signatures As IEnumerable(Of SignatureInfo)) As String
        Dim duplicates = signatures _
            .Where(Function(signature) Not String.IsNullOrWhiteSpace(signature.SignerKey)) _
            .GroupBy(Function(signature) signature.SignerKey, StringComparer.OrdinalIgnoreCase) _
            .Where(Function(group) group.Count() > 1) _
            .Select(Function(group) group.First().SignerDisplayName & " x" & group.Count().ToString())

        Return String.Join(Separator, duplicates)
    End Function

    Private Function FirstNonEmpty(ParamArray values As String()) As String
        For Each value In values
            If Not String.IsNullOrWhiteSpace(value) Then
                Return value.Trim()
            End If
        Next

        Return String.Empty
    End Function

    Private Sub SaveReport(rows As IEnumerable(Of PdfReportRow), outputPath As String)
        Using workbook As New XLWorkbook()
            Dim worksheet = workbook.Worksheets.Add("Podpisy PDF")
            WriteHeaders(worksheet)

            Dim rowIndex = 2
            For Each reportRow In rows
                WriteRow(worksheet, rowIndex, reportRow)
                rowIndex += 1
            Next

            worksheet.Columns().AdjustToContents()
            worksheet.SheetView.FreezeRows(1)
            workbook.SaveAs(outputPath)
        End Using
    End Sub

    Private Sub WriteHeaders(worksheet As IXLWorksheet)
        Dim headers = New String() {
            "Nazwa pliku PDF",
            "Liczba podpisów",
            "Zdublowane podpisy",
            "PESEL",
            "VAT/NIP",
            "Nazwisko",
            "Imię",
            "Nazwa podpisującego (CN)",
            "Daty podpisu",
            "Typy podpisów",
            "Nazwy pól podpisu",
            "Podpis obejmuje cały dokument",
            "Status odczytu",
            "Błędy podpisów",
            "Status certyfikatu",
            "Subject certyfikatu"
        }

        For columnIndex = 0 To headers.Length - 1
            worksheet.Cell(1, columnIndex + 1).Value = headers(columnIndex)
            worksheet.Cell(1, columnIndex + 1).Style.Font.Bold = True
        Next
    End Sub

    Private Sub WriteRow(worksheet As IXLWorksheet, rowIndex As Integer, reportRow As PdfReportRow)
        worksheet.Cell(rowIndex, 1).Value = reportRow.FileName
        worksheet.Cell(rowIndex, 2).Value = reportRow.SignatureCount
        worksheet.Cell(rowIndex, 3).Value = reportRow.DuplicateSignatures
        worksheet.Cell(rowIndex, 4).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.Pesel)
        worksheet.Cell(rowIndex, 5).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.Vat)
        worksheet.Cell(rowIndex, 6).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.Surname)
        worksheet.Cell(rowIndex, 7).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.GivenName)
        worksheet.Cell(rowIndex, 8).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.CommonName)
        worksheet.Cell(rowIndex, 9).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.SignDateText)
        worksheet.Cell(rowIndex, 10).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.SignatureType)
        worksheet.Cell(rowIndex, 11).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.FieldName)
        worksheet.Cell(rowIndex, 12).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) If(signature.CoversWholeDocument, "Tak", "Nie"))
        worksheet.Cell(rowIndex, 13).Value = reportRow.ReadStatus
        worksheet.Cell(rowIndex, 14).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.ErrorMessage)
        worksheet.Cell(rowIndex, 15).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.CertificateStatus)
        worksheet.Cell(rowIndex, 16).Value = JoinSignatureValues(reportRow.Signatures, Function(signature) signature.Subject)
        worksheet.Row(rowIndex).Style.Alignment.WrapText = True
    End Sub

    Private Function JoinSignatureValues(signatures As IEnumerable(Of SignatureInfo), selector As Func(Of SignatureInfo, String)) As String
        Dim values = signatures.Select(selector).Where(Function(value) Not String.IsNullOrWhiteSpace(value)).Distinct().ToList()
        Return String.Join(Separator, values)
    End Function
End Module

Public Class PdfReportRow
    Public Property FileName As String = String.Empty
    Public Property SignatureCount As Integer
    Public Property DuplicateSignatures As String = String.Empty
    Public Property ReadStatus As String = String.Empty
    Public Property Signatures As New List(Of SignatureInfo)()
End Class

Public Class SignatureInfo
    Public Property FieldName As String = String.Empty
    Public Property Pesel As String = String.Empty
    Public Property Vat As String = String.Empty
    Public Property Surname As String = String.Empty
    Public Property GivenName As String = String.Empty
    Public Property CommonName As String = String.Empty
    Public Property SignDate As DateTime?
    Public Property SignatureType As String = String.Empty
    Public Property CoversWholeDocument As Boolean
    Public Property SignatureValid As Boolean
    Public Property CertificateStatus As String = String.Empty
    Public Property Subject As String = String.Empty
    Public Property ErrorMessage As String = String.Empty

    Public ReadOnly Property SignDateText As String
        Get
            If SignDate.HasValue Then
                Return SignDate.Value.ToString("yyyy-MM-dd HH:mm:ss")
            End If

            Return String.Empty
        End Get
    End Property

    Public ReadOnly Property SignerDisplayName As String
        Get
            Dim parts = New String() {Pesel, Vat, Surname, GivenName, CommonName}.Where(Function(value) Not String.IsNullOrWhiteSpace(value))
            Return String.Join(" ", parts)
        End Get
    End Property

    Public ReadOnly Property SignerKey As String
        Get
            If Not String.IsNullOrWhiteSpace(Pesel) Then
                Return "PESEL:" & Pesel
            End If

            If Not String.IsNullOrWhiteSpace(Vat) Then
                Return "VAT:" & Vat
            End If

            If Not String.IsNullOrWhiteSpace(CommonName) Then
                Return "CN:" & CommonName
            End If

            Return String.Empty
        End Get
    End Property
End Class

Public Class ErrorLogger
    Private ReadOnly _logPath As String
    Private ReadOnly _syncRoot As New Object()

    Public Sub New(logPath As String)
        _logPath = logPath
        Dim header = "=== PdfSignatureReport log " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & " ===" & Environment.NewLine
        File.WriteAllText(_logPath, header, Encoding.UTF8)
    End Sub

    Public Sub LogInfo(category As String, fileName As String, signatureName As String, message As String)
        WriteEntry("INFO", category, fileName, signatureName, message, Nothing)
    End Sub

    Public Sub LogWarning(category As String, fileName As String, signatureName As String, message As String)
        WriteEntry("OSTRZEŻENIE", category, fileName, signatureName, message, Nothing)
    End Sub

    Public Sub LogError(category As String, fileName As String, signatureName As String, message As String, Optional ex As Exception = Nothing)
        WriteEntry("BŁĄD", category, fileName, signatureName, message, ex)
    End Sub

    Private Sub WriteEntry(level As String, category As String, fileName As String, signatureName As String, message As String, ex As Exception)
        Dim builder As New StringBuilder()
        builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        builder.Append("	")
        builder.Append(level)
        builder.Append("	")
        builder.Append(category)
        builder.Append("	Plik: ")
        builder.Append(If(String.IsNullOrWhiteSpace(fileName), "-", fileName))
        builder.Append("	Podpis: ")
        builder.Append(If(String.IsNullOrWhiteSpace(signatureName), "-", signatureName))
        builder.Append("	")
        builder.Append(message)

        If ex IsNot Nothing Then
            builder.Append(Environment.NewLine)
            builder.Append(ex.ToString())
        End If

        builder.Append(Environment.NewLine)

        SyncLock _syncRoot
            File.AppendAllText(_logPath, builder.ToString(), Encoding.UTF8)
        End SyncLock
    End Sub
End Class
