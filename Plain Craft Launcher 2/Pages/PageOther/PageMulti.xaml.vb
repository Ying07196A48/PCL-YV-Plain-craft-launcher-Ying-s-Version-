Imports System.IO

Public Class PageMulti

    Private Sub PageMulti_Loaded(sender As Object, e As RoutedEventArgs) Handles MyBase.Loaded
        RefreshGames()
    End Sub

    Public Sub RefreshGames()
        Dim Games As New List(Of Object)
        For Each W In RunningGames
            If Not W.GameProcess.HasExited Then
                Games.Add(New With {.Title = W.GameProcess.MainWindowTitle, .PID = W.GameProcess.Id})
            End If
        Next
        ListRunningGames.ItemsSource = Games
        LabEmpty.Visibility = If(Games.Count = 0, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub BtnKillGame_Click(sender As Object, e As RoutedEventArgs)
        Dim Pid As Integer = CInt(CType(sender, FrameworkElement).Tag)
        Try
            Dim P = Process.GetProcessById(Pid)
            P.Kill()
            Hint("已关闭 MC 进程 " & Pid, HintType.Green)
        Catch
            Hint("关闭失败，进程可能已退出", HintType.Red)
        End Try
        RefreshGames()
    End Sub

End Class
