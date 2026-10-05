Imports System
Imports System.IO
Imports System.Text

Friend NotInheritable Class Logger
    Private ReadOnly _path As String
    Private ReadOnly _syncRoot As New Object()

    Public Sub New(logPath As String)
        _path = logPath
        Dim directoryPath = System.IO.Path.GetDirectoryName(logPath)
        If Not String.IsNullOrEmpty(directoryPath) Then
            System.IO.Directory.CreateDirectory(directoryPath)
        End If
    End Sub

    Public Sub Info(message As String)
        Write("INFO", message)
    End Sub

    Public Sub Warn(message As String)
        Write("WARN", message)
    End Sub

    Public Sub [Error](message As String, ex As Exception)
        Write("ERROR", message & Environment.NewLine & ex.ToString())
    End Sub

    Private Sub Write(level As String, message As String)
        SyncLock _syncRoot
            File.AppendAllText(_path,
                               String.Format(Globalization.CultureInfo.InvariantCulture,
                                             "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}{3}",
                                             DateTime.Now, level, message, Environment.NewLine),
                               New UTF8Encoding(False))
        End SyncLock
    End Sub
End Class
