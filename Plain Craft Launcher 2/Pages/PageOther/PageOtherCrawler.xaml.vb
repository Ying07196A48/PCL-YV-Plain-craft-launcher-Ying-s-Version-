Imports System.Net
Imports System.Text.RegularExpressions
Imports System.IO

Public Class PageOtherCrawler

    Private Sub PageOtherCrawler_Loaded(sender As Object, e As RoutedEventArgs) Handles MyBase.Loaded
        TxtSavePath.Text = Paths.Base & "PCL\CrawlerDownloads\"
        Directory.CreateDirectory(TxtSavePath.Text)
    End Sub

    Private Sub BtnBrowse_Click(sender As Object, e As EventArgs) Handles BtnBrowse.Click
        Dim Dialog = New System.Windows.Forms.FolderBrowserDialog()
        Dialog.Description = "选择保存文件夹"
        If Dialog.ShowDialog() = System.Windows.Forms.DialogResult.OK Then
            TxtSavePath.Text = Dialog.SelectedPath & "\"
        End If
    End Sub

    Private Sub BtnCrawl_Click(sender As Object, e As EventArgs) Handles BtnCrawl.Click
        Dim Url As String = TxtUrl.Text.Trim()
        If Url = "" Then
            Hint("请输入网址", HintType.Red)
            Return
        End If
        If Not Url.StartsWithF("http") Then Url = "https://" & Url

        LstFiles.Items.Clear()
        Progress.Value = 0
        LabStatus.Text = "正在获取页面..."
        BtnCrawl.IsEnabled = False

        RunInNewThread(Sub()
            Try
                Dim Client As New WebClient()
                Client.Encoding = Encoding.UTF8
                Dim Html As String = Client.DownloadString(Url)

                ' 提取所有链接
                Dim Links As New List(Of String)
                For Each Match As Match In Regex.Matches(Html, "href=""([^""]+)""", RegexOptions.IgnoreCase)
                    Dim Href As String = Match.Groups(1).Value
                    If Href.StartsWithF("http") Then
                        Links.Add(Href)
                    ElseIf Href.StartsWithF("/") Then
                        Dim Base As New Uri(Url)
                        Links.Add(Base.Scheme & "://" & Base.Host & Href)
                    End If
                Next

                ' 过滤文件链接
                Dim FileExts As New List(Of String) From {".zip", ".jar", ".exe", ".rar", ".7z", ".png", ".jpg", ".json", ".txt", ".md", ".dll"}
                Dim Files As New List(Of String)
                For Each Link In Links.Distinct()
                    For Each Ext In FileExts
                        If Link.ToLower().EndsWithF(Ext) Then
                            Files.Add(Link)
                            Exit For
                        End If
                    Next
                Next

                RunInUi(Sub()
                    LabStatus.Text = $"找到 {Files.Count} 个文件，开始下载..."
                    Progress.Value = 0
                    For Each File In Files
                        LstFiles.Items.Add(File)
                    Next
                End Sub)

                ' 下载文件
                Dim SavedCount As Integer = 0
                For i As Integer = 0 To Files.Count - 1
                    Dim FileUrl As String = Files(i)
                    Try
                        Dim FileName As String = Path.GetFileName(New Uri(FileUrl).LocalPath)
                        If FileName = "" Then FileName = "file_" & i
                        Client.DownloadFile(FileUrl, TxtSavePath.Text & FileName)
                        SavedCount += 1
                        RunInUi(Sub()
                            Progress.Value = If(Files.Count > 0, i / Files.Count * 100, 100)
                            LabStatus.Text = $"正在下载 ({i + 1}/{Files.Count}): {FileName}"
                        End Sub)
                    Catch ex As Exception
                        Logger.Error(ex, "下载失败: " & FileUrl)
                    End Try
                Next

                RunInUi(Sub()
                    Progress.Value = 100
                    LabStatus.Text = $"完成！共下载 {SavedCount}/{Files.Count} 个文件到 {TxtSavePath.Text}"
                    BtnCrawl.IsEnabled = True
                    Hint("爬虫完成！", HintType.Green)
                End Sub)

            Catch ex As Exception
                RunInUi(Sub()
                    LabStatus.Text = "出错: " & ex.Message
                    BtnCrawl.IsEnabled = True
                End Sub)
            End Try
        End Sub, "网页爬虫", ThreadPriority.BelowNormal)
    End Sub

End Class
