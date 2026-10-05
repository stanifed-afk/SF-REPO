Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO

Friend Module Program
    Private ReadOnly RandomNumber As New Random()

    Public Function Main(args As String()) As Integer
        If args.Length <> 4 Then
            Console.Error.WriteLine("Użycie: SignatureManager.exe <inputFolderPDF> <outputFolderPDF> <fileSAPAK> <outputExcel>")
            Return 1
        End If

        Dim inputFolder = Path.GetFullPath(args(0))
        Dim outputFolder = Path.GetFullPath(args(1))
        Dim akPath = Path.GetFullPath(args(2))
        Dim reportPath = Path.GetFullPath(args(3))

        If Not Directory.Exists(inputFolder) Then
            Console.Error.WriteLine("Folder wejściowy nie istnieje: " & inputFolder)
            Return 2
        End If
        If Not File.Exists(akPath) Then
            Console.Error.WriteLine("Plik SAP_AK nie istnieje: " & akPath)
            Return 3
        End If

        Directory.CreateDirectory(outputFolder)
        Dim logDirectory = Path.GetDirectoryName(reportPath)
        If String.IsNullOrEmpty(logDirectory) Then logDirectory = Environment.CurrentDirectory
        Dim logPath = Path.Combine(logDirectory, "SignatureManager_" & DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) & ".log")
        Dim logger = New Logger(logPath)

        Try
            logger.Info("Uruchomiono Signature Manager 3.")
            Try
                DependencyValidator.ValidateIText()
                logger.Info("Zależności kryptograficzne iText mają zgodne wersje.")
            Catch ex As Exception
                logger.Error("Nieprawidłowa konfiguracja zależności iText.", ex)
                Return 6
            End Try

            Dim pdfFiles As New List(Of String)()
            For Each discoveredFile As String In Directory.EnumerateFiles(inputFolder, "*", SearchOption.AllDirectories)
                If String.Equals(System.IO.Path.GetExtension(discoveredFile), ".pdf", StringComparison.OrdinalIgnoreCase) Then
                    pdfFiles.Add(discoveredFile)
                End If
            Next
            logger.Info("Liczba znalezionych plików PDF: " & pdfFiles.Count.ToString(CultureInfo.InvariantCulture))

            Dim employees = AkRepository.Load(akPath)
            Dim analyzer = New PdfAnalyzer(employees, logger)
            Dim records As New List(Of ReportRecord)(pdfFiles.Count)

            For Each pdfPath In pdfFiles
                Dim record = analyzer.Analyze(pdfPath)
                records.Add(record)
                logger.Info(String.Format(CultureInfo.InvariantCulture, "Analiza: {0}; status: {1}", pdfPath, record.Status))
            Next

            Try
                ExcelReportWriter.Write(reportPath, records)
                logger.Info("Zapisano raport: " & reportPath)
            Catch ex As Exception
                logger.Error("Nie udało się zapisać raportu: " & reportPath, ex)
                Return 4
            End Try

            Dim moveFailures = 0
            For Each record In records
                Try
                    Dim destination = BuildDestinationPath(outputFolder, record)
                    File.Move(record.SourcePath, destination)
                    logger.Info(String.Format(CultureInfo.InvariantCulture, "Przeniesienie: {0} -> {1}", record.SourcePath, destination))
                Catch ex As Exception
                    moveFailures += 1
                    logger.Error("Nie udało się przenieść pliku: " & record.SourcePath, ex)
                End Try
            Next

            logger.Info("Zakończono przetwarzanie.")
            Return If(moveFailures = 0, 0, 5)
        Catch ex As Exception
            logger.Error("Krytyczny błąd programu.", ex)
            Return 10
        End Try
    End Function

    Private Function BuildDestinationPath(outputFolder As String, record As ReportRecord) As String
        Dim baseName As String
        If UsesPhysicalFileName(record.Status) Then
            baseName = Path.GetFileNameWithoutExtension(record.SourcePath)
        Else
            baseName = Path.GetFileNameWithoutExtension(record.Title)
        End If

        Dim stamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
        Dim suffix As Integer
        SyncLock RandomNumber
            suffix = RandomNumber.Next(100000, 1000000)
        End SyncLock
        Dim candidateBase = String.Format(CultureInfo.InvariantCulture, "{0}__{1}_{2}", baseName, stamp, suffix)
        Dim candidate = Path.Combine(outputFolder, candidateBase & ".pdf")
        Dim version = 2
        While File.Exists(candidate)
            candidate = Path.Combine(outputFolder, candidateBase & "_v" & version.ToString(CultureInfo.InvariantCulture) & ".pdf")
            version += 1
        End While
        Return candidate
    End Function

    Private Function UsesPhysicalFileName(status As String) As Boolean
        Return String.Equals(status, SignatureStatus.InvalidTitle, StringComparison.Ordinal) OrElse
               String.Equals(status, SignatureStatus.EmptyTitle, StringComparison.Ordinal) OrElse
               String.Equals(status, SignatureStatus.Unreadable, StringComparison.Ordinal)
    End Function
End Module
