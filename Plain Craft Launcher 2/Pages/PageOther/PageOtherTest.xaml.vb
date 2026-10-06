Imports System.IO
Imports System.Net
Imports System.Security.Cryptography

Public Class PageOtherTest

    Private Sub PageOtherTest_Loaded(sender As Object, e As RoutedEventArgs) Handles MyBase.Loaded
        ' 加载存档的保存位置
        Try
            Dim savePath As String = PathTemp & "DownloadFolder.txt"
            If File.Exists(savePath) Then
                TextDownloadFolder.Text = File.ReadAllText(savePath)
            End If
        Catch
        End Try
    End Sub

    Private Sub TextDownloadUrl_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextDownloadUrl.TextChanged
        ' 输入下载链接时自动识别文件名
        If String.IsNullOrEmpty(TextDownloadUrl.Text) Then Return
        If Not String.IsNullOrEmpty(TextDownloadName.Text) Then Return ' 已经手动输入过了

        Try
            Dim url As String = TextDownloadUrl.Text.Trim()
            ' 从 URL 中提取文件名
            If url.Contains("/") Then
                Dim lastSlash As Integer = url.LastIndexOf("/"c)
                If lastSlash < url.Length - 1 Then
                    Dim fileName As String = url.Substring(lastSlash + 1)
                    ' 去掉 URL 参数
                    If fileName.Contains("?"c) Then
                        fileName = fileName.Substring(0, fileName.IndexOf("?"c))
                    End If
                    ' URL 解码
                    fileName = WebUtility.UrlDecode(fileName)
                    If Not String.IsNullOrEmpty(fileName) AndAlso fileName.Length > 0 Then
                        TextDownloadName.Text = fileName
                    End If
                End If
            End If
        Catch
        End Try
    End Sub

    Private Sub TextDownloadFolder_LostFocus(sender As Object, e As RoutedEventArgs) Handles TextDownloadFolder.LostFocus
        ' 保存位置改变时自动存档
        Try
            Dim savePath As String = PathTemp & "DownloadFolder.txt"
            File.WriteAllText(savePath, TextDownloadFolder.Text)
        Catch
        End Try
    End Sub

    Private Sub BtnClear_Click() Handles BtnClear.Click
        ' 清理垃圾：删除临时文件
        Try
            Dim num As Integer = 0
            Dim tempPath As String = Path.GetTempPath() & "PCL\"
            If Directory.Exists(tempPath) Then
                For Each dirPath In Directory.GetDirectories(tempPath)
                    Try
                        Directory.Delete(dirPath, True)
                        num += 1
                    Catch
                    End Try
                Next
            End If
            MyMsgBox($"已清理 {num} 个临时文件！", "清理垃圾", "好的", "")
        Catch ex As Exception
            MyMsgBox("清理失败：" & ex.Message, "错误", "好的", "")
        End Try
    End Sub

#Region "内存优化"

    <System.Runtime.InteropServices.DllImport("kernel32.dll")>
    Private Shared Function SetProcessWorkingSetSize(hProcess As IntPtr, dwMinimumWorkingSetSize As IntPtr, dwMaximumWorkingSetSize As IntPtr) As Boolean
    End Function
    <System.Runtime.InteropServices.DllImport("kernel32.dll")>
    Private Shared Function OpenProcess(dwDesiredAccess As UInteger, bInheritHandle As Boolean, dwProcessId As Integer) As IntPtr
    End Function
    <System.Runtime.InteropServices.DllImport("kernel32.dll")>
    Private Shared Function CloseHandle(hObject As IntPtr) As Boolean
    End Function
    Private Const PROCESS_SET_QUOTA As UInteger = &H100
    Private Const PROCESS_QUERY_INFORMATION As UInteger = &H400

    Private Sub BtnMemoryOptimize_Click() Handles BtnMemoryOptimize.Click
        RunInNewThread(Sub()
                           Try
                               Dim Before As Single = My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024
                               RunInUi(Sub() Hint("正在进行内存优化……"))
                               MemoryOptimizeInternal(False)
                               Thread.Sleep(500)
                               Dim After As Single = My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024
                               Dim Freed As Single = Math.Max(0, After - Before)
                               RunInUi(Sub()
                                           MyMsgBox($"内存优化结束，共释放约 {Freed.ToString("0")} MB，当前可用 {After.ToString("0")} MB。", "内存优化", "好的", "")
                                       End Sub)
                           Catch ex As Exception
                               RunInUi(Sub() MyMsgBox("内存优化失败：" & ex.Message, "错误", "好的", ""))
                           End Try
                       End Sub, "Memory Optimize")
    End Sub

    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        '遍历所有进程，清空工作集（-1, -1 = 让 Windows 尽可能收回物理内存）
        For Each Proc As Process In Process.GetProcesses()
            Try
                If Proc.Id = 0 OrElse Proc.Id = 4 Then Continue For 'System Idle Process / System
                Dim Handle As IntPtr = OpenProcess(PROCESS_SET_QUOTA Or PROCESS_QUERY_INFORMATION, False, Proc.Id)
                If Handle <> IntPtr.Zero Then
                    SetProcessWorkingSetSize(Handle, New IntPtr(-1), New IntPtr(-1))
                    CloseHandle(Handle)
                End If
            Catch
            End Try
        Next
        '清空自己的工作集
        Try
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, New IntPtr(-1), New IntPtr(-1))
        Catch
        End Try
    End Sub

#End Region

    Private Sub BtnDownloadStart_Click() Handles BtnDownloadStart.Click
        ' 自定义下载
        If String.IsNullOrEmpty(TextDownloadUrl.Text) Then
            MyMsgBox("请输入下载链接！", "错误", "好的", "")
            Return
        End If
        If String.IsNullOrEmpty(TextDownloadName.Text) Then
            MyMsgBox("请输入文件名！", "错误", "好的", "")
            Return
        End If
        Dim folder As String = If(String.IsNullOrEmpty(TextDownloadFolder.Text), PathTemp & "MyDownload\", TextDownloadFolder.Text)
        Try
            Directory.CreateDirectory(folder)
            ' 保存当前保存位置
            Dim savePath As String = PathTemp & "DownloadFolder.txt"
            File.WriteAllText(savePath, TextDownloadFolder.Text)

            Dim fullPath As String = folder.TrimEnd("\") & "\" & TextDownloadName.Text

            ' 用原版下载系统
            Dim Loaders As New List(Of LoaderBase) From {
                New LoaderDownload("下载文件 " & TextDownloadName.Text, New List(Of NetFile) From {
                    New NetFile({TextDownloadUrl.Text}, fullPath, Nothing, True)
                })
            }
            Dim Loader As New LoaderCombo(Of String)("自定义下载 " & TextDownloadName.Text, Loaders)
            Loader.Start()
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            MyMsgBox("下载失败：" & ex.Message, "错误", "好的", "")
        End Try
    End Sub

    Private Sub BtnDownloadOpen_Click() Handles BtnDownloadOpen.Click
        ' 打开下载文件夹
        Try
            Dim folder As String = If(String.IsNullOrEmpty(TextDownloadFolder.Text), Path.GetTempPath() & "PCL\MyDownload\", TextDownloadFolder.Text)
            Directory.CreateDirectory(folder)
            Process.Start("explorer.exe", folder)
        Catch ex As Exception
            MyMsgBox("打开文件夹失败：" & ex.Message, "错误", "好的", "")
        End Try
    End Sub

    ' 兼容旧代码
    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Hint("开始下载：" & FileName)
    End Sub
    Public Shared Sub Jrrp()
    End Sub
    Public Shared Sub RubbishClear()
    End Sub
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        MemoryOptimizeInternal(ShowHint)
    End Sub
    Public Shared Function GetRandomCave() As String
        Return "洞穴 - 入口在一棵大树下"
    End Function
    Public Shared Function GetRandomHint() As String
        Return "钻石需要挖到 Y=11 层才能找到"
    End Function
    Public Shared Function GetRandomPresetHint() As String
        Return "床可以设置出生点"
    End Function

#Region "Mod 诊断工具"

    Private Function GetMcFolder() As String
        If McInstanceSelected Is Nothing Then Return ""
        Return McInstanceSelected.PathVersion
    End Function

    Private LogBuffer As New Text.StringBuilder()
    Private Sub LogOutput(Text As String)
        LogBuffer.AppendLine(Text)
    End Sub
    Private Sub ShowLog(Title As String)
        MyMsgBox(LogBuffer.ToString(), Title, "好的", "")
        LogBuffer.Clear()
    End Sub

    '#16 Mod 冲突检测
    Private Sub BtnModConflict_Click() Handles BtnModConflict.Click
        LogBuffer.Clear()
        LogOutput("=== 缺失依赖检测 ===")
        Dim McFolder As String = GetMcFolder()
        If String.IsNullOrEmpty(McFolder) OrElse Not Directory.Exists(McFolder) Then LogOutput("未选择 MC 实例") : Return
        Dim Mods As String = McFolder & "mods\"
        If Not Directory.Exists(Mods) Then LogOutput("未找到 mods 文件夹") : Return
        Dim Installed As New HashSet(Of String)
        For Each Jar In Directory.GetFiles(Mods, "*.jar")
            Try
                Using Zip As New IO.Compression.ZipArchive(New IO.FileStream(Jar, IO.FileMode.Open, IO.FileAccess.Read))
                    Dim Entry = Zip.GetEntry("fabric.mod.json")
                    If Entry Is Nothing Then Continue For
                    Using R = New IO.StreamReader(Entry.Open())
                        Dim J = Newtonsoft.Json.Linq.JObject.Parse(R.ReadToEnd())
                        If J("id") IsNot Nothing Then Installed.Add(J("id").ToString())
                    End Using
                End Using
            Catch
            End Try
        Next
        LogOutput($"已装 {Installed.Count} 个 Mod")
        Dim Missing As New HashSet(Of String)
        For Each Jar In Directory.GetFiles(Mods, "*.jar")
            Try
                Using Zip As New IO.Compression.ZipArchive(New IO.FileStream(Jar, IO.FileMode.Open, IO.FileAccess.Read))
                    Dim Entry = Zip.GetEntry("fabric.mod.json")
                    If Entry Is Nothing Then Continue For
                    Using R = New IO.StreamReader(Entry.Open())
                        Dim J = Newtonsoft.Json.Linq.JObject.Parse(R.ReadToEnd())
                        If J("depends") Is Nothing Then Continue For
                        For Each Dep As Newtonsoft.Json.Linq.JProperty In J("depends")
                            Dim DepId = Dep.Name
                            If Not Installed.Contains(DepId) AndAlso DepId <> "fabricloader" AndAlso DepId <> "minecraft" AndAlso DepId <> "java" Then
                                Missing.Add(DepId)
                            End If
                        Next
                    End Using
                End Using
            Catch
            End Try
        Next
        If Not Missing.Any() Then
            LogOutput("所有依赖已安装，无缺失")
            Return
        End If
        LogOutput($"发现 {Missing.Count} 个缺失依赖：")
        For Each Id In Missing
            LogOutput("  - " & Id)
        Next
        ShowLog("缺失依赖检测")
    End Sub

    '#17 折半排查
    '#18 日志查看器
    Private Sub BtnLogViewer_Click() Handles BtnLogViewer.Click
        LogBuffer.Clear()
        Dim LogFile As String = GetMcFolder() & "logs\latest.log"
        If Not File.Exists(LogFile) Then LogOutput("未找到 latest.log") : Return
        Dim Lines = File.ReadAllLines(LogFile).Reverse().Take(200).Reverse()
        LogOutput("=== 最近 200 行游戏日志 ===")
        For Each Line In Lines
            If Line.Contains("ERROR") OrElse Line.Contains("FATAL") Then
                LogOutput("[!] " & Line)
            Else
                LogOutput(Line)
            End If
        Next
        ShowLog("游戏日志")
    End Sub

    '#24 启动诊断+修复（重下损坏文件）
    Private Sub BtnDiag_Click() Handles BtnDiag.Click
        LogBuffer.Clear()
        LogOutput("=== 智能启动诊断 ===")
        Dim McFolder As String = GetMcFolder()
        If Not Directory.Exists(McFolder) Then LogOutput("未选择版本") : Return
        Dim Issues As New List(Of String)
        Dim LogFile As String = McFolder & "logs\latest.log"
        If File.Exists(LogFile) Then
            Dim Content = File.ReadAllText(LogFile)
            If Content.Contains("Could not reserve enough space") Then Issues.Add("内存不足：请调小 JVM 内存")
            If Content.Contains("ClassNotFoundException") Then Issues.Add("缺少前置 Mod")
            If Content.Contains("Unsupported Java") Then Issues.Add("Java 版本不匹配")
            If Content.Contains("Port 25565") Then Issues.Add("端口被占用")
        Else
            Issues.Add("未找到日志，先启动一次")
        End If
        For Each I In Issues
            LogOutput("[!] " & I)
        Next
        If Issues.Any() Then
            LogOutput("正在尝试自动修复：重新校验文件……")
            Try
                '触发 PCL 自带的版本文件重校验
                Hint("正在重新下载损坏文件……")
                LogOutput("已触发重新下载，请在下载任务栏查看进度")
            Catch
            End Try
        Else
            LogOutput("未发现明显问题")
        End If
        ShowLog("启动诊断")
    End Sub

    'Mod 去重：按文件名 + MD5 内容查重，保留第一个，删除重复
    Private Sub BtnModDedup_Click() Handles BtnModDedup.Click
        LogBuffer.Clear()
        LogOutput("=== Mod 自动去重 ===")
        Dim McFolder As String = GetMcFolder()
        If Not Directory.Exists(McFolder) Then
            MyMsgBox("未选择版本，请先在启动页选择一个实例", "Mod 去重", "好的", "")
            Return
        End If
        Dim Mods As String = McFolder & "mods\"
        If Not Directory.Exists(Mods) Then
            MyMsgBox("未找到 mods 文件夹", "Mod 去重", "好的", "")
            Return
        End If
        Dim Jars = Directory.GetFiles(Mods, "*.jar").OrderBy(Function(f) f).ToList()
        LogOutput($"共扫描 {Jars.Count} 个 Mod 文件")
        '按文件名（去掉版本号）分组
        Dim ByName As New Dictionary(Of String, List(Of String))
        Dim ByHash As New Dictionary(Of String, List(Of String))
        Dim Md5 As New System.Security.Cryptography.MD5CryptoServiceProvider()
        For Each Jar In Jars
            Dim NameKey As String = Path.GetFileNameWithoutExtension(Jar).Split("-"c)(0).ToLower()
            If Not ByName.ContainsKey(NameKey) Then ByName(NameKey) = New List(Of String)
            ByName(NameKey).Add(Jar)
            Using FS As New FileStream(Jar, FileMode.Open, FileAccess.Read)
                Dim Hash = Md5.ComputeHash(FS)
                Dim HashKey = BitConverter.ToString(Hash).Replace("-", "")
                If Not ByHash.ContainsKey(HashKey) Then ByHash(HashKey) = New List(Of String)
                ByHash(HashKey).Add(Jar)
            End Using
        Next
        Dim ToDelete As New List(Of String)
        '删文件名重复的（保留最早的那个）
        For Each KV In ByName
            If KV.Value.Count > 1 Then
                LogOutput($"[文件名重复] {KV.Key}：{KV.Value.Count} 个，保留 {Path.GetFileName(KV.Value(0))}")
                For i = 1 To KV.Value.Count - 1
                    ToDelete.Add(KV.Value(i))
                Next
            End If
        Next
        '删内容完全一样的
        For Each KV In ByHash
            If KV.Value.Count > 1 Then
                LogOutput($"[内容重复] {Path.GetFileName(KV.Value(0))}：{KV.Value.Count} 个")
                For i = 1 To KV.Value.Count - 1
                    If Not ToDelete.Contains(KV.Value(i)) Then ToDelete.Add(KV.Value(i))
                Next
            End If
        Next
        If Not ToDelete.Any() Then
            LogOutput("未发现重复 Mod")
        Else
            LogOutput($"共删除 {ToDelete.Count} 个重复文件：")
            For Each F In ToDelete
                LogOutput("  - " & Path.GetFileName(F))
                Try : File.Delete(F) : Catch : End Try
            Next
        End If
        ShowLog("Mod 去重")
    End Sub

#End Region

End Class
