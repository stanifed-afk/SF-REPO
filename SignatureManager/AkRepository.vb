Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports ClosedXML.Excel

Friend NotInheritable Class AkRepository
    Public Shared Function Load(path As String) As Dictionary(Of String, List(Of EmployeeRecord))
        Dim result = New Dictionary(Of String, List(Of EmployeeRecord))(StringComparer.Ordinal)

        Using workbook As New XLWorkbook(path)
            Dim sheet = workbook.Worksheet("SAP_AK")
            Dim headerRow = sheet.Row(1)
            Dim employeeColumn = FindColumn(headerRow, "Numer osobowy")
            Dim emailColumn = FindColumn(headerRow, "E-mail")
            Dim peselColumn = FindColumn(headerRow, "Pesel")

            Dim lastRow = sheet.LastRowUsed()
            If lastRow Is Nothing Then Return result

            For rowNumber = 2 To lastRow.RowNumber()
                Dim employeeNumber = NormalizeDigits(sheet.Cell(rowNumber, employeeColumn).GetFormattedString(), 6)
                If employeeNumber.Length <> 6 Then Continue For

                Dim record = New EmployeeRecord With {
                    .EmployeeNumber = employeeNumber,
                    .Email = sheet.Cell(rowNumber, emailColumn).GetString().Trim(),
                    .Pesel = NormalizeDigits(sheet.Cell(rowNumber, peselColumn).GetFormattedString(), 11)
                }

                Dim records As List(Of EmployeeRecord) = Nothing
                If Not result.TryGetValue(employeeNumber, records) Then
                    records = New List(Of EmployeeRecord)()
                    result.Add(employeeNumber, records)
                End If
                records.Add(record)
            Next
        End Using

        Return result
    End Function

    Private Shared Function FindColumn(headerRow As IXLRow, expectedName As String) As Integer
        For Each cell In headerRow.CellsUsed()
            If String.Equals(cell.GetString().Trim(), expectedName, StringComparison.OrdinalIgnoreCase) Then
                Return cell.Address.ColumnNumber
            End If
        Next
        Throw New InvalidOperationException("Brak wymaganej kolumny w arkuszu SAP_AK: " & expectedName)
    End Function

    Private Shared Function NormalizeDigits(value As String, width As Integer) As String
        Dim trimmed = If(value, String.Empty).Trim()
        If trimmed.EndsWith(".0", StringComparison.Ordinal) Then trimmed = trimmed.Substring(0, trimmed.Length - 2)
        If trimmed.Length = 0 OrElse Not trimmed.All(AddressOf Char.IsDigit) OrElse trimmed.Length > width Then Return trimmed
        Return trimmed.PadLeft(width, "0"c)
    End Function
End Class
