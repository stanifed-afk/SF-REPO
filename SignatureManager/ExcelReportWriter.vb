Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ClosedXML.Excel

Friend NotInheritable Class ExcelReportWriter
    Public Shared Sub Write(outputPath As String, records As IReadOnlyList(Of ReportRecord))
        Dim fullOutputPath = System.IO.Path.GetFullPath(outputPath)
        Dim directoryPath = System.IO.Path.GetDirectoryName(fullOutputPath)
        System.IO.Directory.CreateDirectory(directoryPath)
        Dim output = New FileInfo(fullOutputPath)
        If output.Exists Then output.Delete()

        Using workbook As New XLWorkbook()
            Dim sheet = workbook.Worksheets.Add("Raport")
            Dim headers = {"Numer pracownika", "Data dokumentu", "Tytuł / nazwa pliku", "Status podpisu", "Data podpisu"}
            For column = 1 To headers.Length
                sheet.Cell(1, column).Value = headers(column - 1)
            Next

            For index = 0 To records.Count - 1
                Dim row = index + 2
                Dim record = records(index)
                sheet.Cell(row, 1).Value = record.EmployeeNumber
                If record.PdfCreationDate.HasValue Then
                    sheet.Cell(row, 2).Value = record.PdfCreationDate.Value
                    sheet.Cell(row, 2).Style.DateFormat.Format = "yyyy-mm-dd"
                End If
                sheet.Cell(row, 3).Value = record.Title
                sheet.Cell(row, 4).Value = record.Status
                If record.SignatureDate.HasValue Then
                    sheet.Cell(row, 5).Value = record.SignatureDate.Value
                    sheet.Cell(row, 5).Style.DateFormat.Format = "yyyy-mm-dd"
                End If
            Next

            sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = True
            sheet.Columns().AdjustToContents()
            sheet.SheetView.FreezeRows(1)
            workbook.SaveAs(fullOutputPath)
        End Using
    End Sub
End Class
