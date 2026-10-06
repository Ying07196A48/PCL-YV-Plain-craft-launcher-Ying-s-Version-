Imports System.Net
Imports System.Text.RegularExpressions
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports System.Windows.Shapes

Public Class PageExtensions

    ' ==================== 数据模型 ====================
    Public Class ExtensionItem
        Public Property Name As String = ""
        Public Property Url As String = ""
        Public Property Type As String = "Web" ' Web / File / Exe
        Public Property Cover As String = ""
    End Class

    Private Items As New List(Of ExtensionItem)
    Private _DataLoaded As Boolean = False
    '图片缓存：避免重复加载同一张图片
    Private ReadOnly Property ImageCache As New Dictionary(Of String, BitmapImage)
    Private ReadOnly Property SavePath As String
        Get
            Return Paths.AppDataThenName & "ExtensionsLinks.json"
        End Get
    End Property
    Private ReadOnly Property IconCacheDir As String
        Get
            Dim Dir As String = Paths.AppDataThenName & "Extensions\Icons\"
            If Not IO.Directory.Exists(Dir) Then IO.Directory.CreateDirectory(Dir)
            Return Dir
        End Get
    End Property

    ' ==================== 页面加载 ====================
    Private Sub PageExtensions_Loaded(sender As Object, e As RoutedEventArgs) Handles MyBase.Loaded
        If Not _DataLoaded Then
            LoadItems()
            _DataLoaded = True
        End If
        RefreshCards()
    End Sub

    '页面卸载时释放资源
    Private Sub PageExtensions_Unloaded(sender As Object, e As RoutedEventArgs) Handles MyBase.Unloaded
        ImageCache.Clear()
        PanItems.Children.Clear()
    End Sub

    Private Sub LoadItems()
        Try
            If IO.File.Exists(SavePath) Then
                Dim Json As String = IO.File.ReadAllText(SavePath)
                If Not String.IsNullOrEmpty(Json) Then
                    Items = Newtonsoft.Json.JsonConvert.DeserializeObject(Of List(Of ExtensionItem))(Json)
                    If Items Is Nothing Then Items = New List(Of ExtensionItem)()
                    For Each It In Items
                        If Not String.IsNullOrEmpty(It.Cover) AndAlso Not IO.File.Exists(It.Cover) Then It.Cover = ""
                    Next
                End If
            End If
        Catch ex As Exception
            Logger.Error(ex, "加载拓展页链接失败")
            Items = New List(Of ExtensionItem)()
        End Try
    End Sub

    Private Sub SaveItems()
        Try
            IO.File.WriteAllText(SavePath, Newtonsoft.Json.JsonConvert.SerializeObject(Items, Newtonsoft.Json.Formatting.Indented))
        Catch ex As Exception
            Logger.Error(ex, "保存拓展页链接失败")
        End Try
    End Sub

    ' ==================== 添加网页链接 ====================
    Private Sub BtnAddUrl_Click() Handles BtnAddUrl.Click
        Dim Input As String = Interaction.InputBox("请输入网页链接（http/https 开头）：", "添加网页链接", "")
        If String.IsNullOrEmpty(Input) Then Return
        Input = Input.Trim()
        If Not Input.StartsWith("http://") AndAlso Not Input.StartsWith("https://") Then
            MyMsgBox("请输入以 http:// 或 https:// 开头的链接", "链接格式错误")
            Return
        End If

        Dim Item As New ExtensionItem With {.Url = Input, .Type = "Web", .Name = "正在识别网页…"}
        Items.Add(Item)
        SaveItems()
        RefreshCards()

        System.Threading.Tasks.Task.Run(
            Sub()
                Try
                    Dim Html As String = DownloadString(Input)
                    Dim TitleMatch As Match = Regex.Match(Html, "<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase Or RegexOptions.Singleline)
                    If TitleMatch.Success Then
                        Item.Name = WebUtility.HtmlDecode(TitleMatch.Groups(1).Value.Trim())
                        If Item.Name.Length > 40 Then Item.Name = Item.Name.Substring(0, 40) & "…"
                    End If
                    Dim ImgUrl As String = ExtractOgImage(Html)
                    If Not String.IsNullOrEmpty(ImgUrl) Then
                        If Not ImgUrl.StartsWith("http") Then
                            Dim Base As New Uri(Input)
                            ImgUrl = New Uri(New Uri(Base.GetLeftPart(UriPartial.Authority)), ImgUrl).ToString()
                        End If
                        Dim CachePath As String = IconCacheDir & Guid.NewGuid().ToString("N") & ".jpg"
                        DownloadFile(ImgUrl, CachePath)
                        If IO.File.Exists(CachePath) AndAlso New IO.FileInfo(CachePath).Length > 1000 Then
                            Item.Cover = CachePath
                        End If
                    End If
                Catch ex As Exception
                    Logger.Error(ex, "识别网页失败：" & Input)
                    If Item.Name = "正在识别网页…" Then Item.Name = Input
                End Try
                RunInUi(Sub()
                            SaveItems()
                            RefreshCards()
                        End Sub)
            End Sub)
    End Sub

    ' ==================== 添加本地文件 ====================
    Private Sub BtnAddFile_Click() Handles BtnAddFile.Click
        Dim FileList = Dialogs.SelectFile("选择要添加的文件（exe、快捷方式、文档等）", False)
        If FileList Is Nothing OrElse FileList.Count = 0 Then Return
        Dim Path As String = FileList.FirstOrDefault()
        If String.IsNullOrEmpty(Path) OrElse Not IO.File.Exists(Path) Then Return
        Dim Ext As String = IO.Path.GetExtension(Path).ToLower()
        Dim Item As New ExtensionItem With {
            .Url = Path,
            .Type = If(Ext = ".exe", "Exe", "File"),
            .Name = IO.Path.GetFileNameWithoutExtension(Path),
            .Cover = ""
        }
        If Ext = ".exe" Then
            Try
                Dim IconPath As String = IconCacheDir & Guid.NewGuid().ToString("N") & ".png"
                ExtractExeIcon(Path, IconPath)
                If IO.File.Exists(IconPath) Then Item.Cover = IconPath
            Catch ex As Exception
                Logger.Error(ex, "提取 exe 图标失败")
            End Try
        End If
        Items.Add(Item)
        SaveItems()
        RefreshCards()
    End Sub

    Private Sub ExtractExeIcon(exePath As String, savePath As String)
        Try
            Using ico As System.Drawing.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath)
                If ico IsNot Nothing Then
                    Using bmp As System.Drawing.Bitmap = ico.ToBitmap()
                        bmp.Save(savePath, System.Drawing.Imaging.ImageFormat.Png)
                    End Using
                End If
            End Using
        Catch
        End Try
    End Sub

    ' ==================== 刷新列表 ====================
    Private Sub RefreshCards()
        PanItems.Children.Clear()
        If Items.Count = 0 Then
            Dim Hint As New TextBlock With {
                .Text = "还没有添加任何链接" & vbCrLf & "点击右上角按钮添加网页链接或本地文件",
                .FontSize = 14,
                .Foreground = TryFindResource("ColorBrushGray3"),
                .HorizontalAlignment = HorizontalAlignment.Center,
                .Margin = New Thickness(0, 80, 0, 0),
                .TextWrapping = TextWrapping.Wrap,
                .TextAlignment = TextAlignment.Center
            }
            PanItems.Children.Add(Hint)
            Return
        End If
        For Each Item In Items
            PanItems.Children.Add(CreateCard(Item))
        Next
    End Sub

    Private Function CreateCard(Item As ExtensionItem) As FrameworkElement
        ' 条目容器：和原版下载页列表一致
        Dim Row As New Border With {
            .Background = TryFindResource("ColorBrushBackground2"),
            .CornerRadius = New CornerRadius(4),
            .Margin = New Thickness(0, 0, 0, 8)
        }

        Dim Grid As New Grid With {.Margin = New Thickness(12, 8, 12, 8)}
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(32)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(8)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})

        ' 左侧图标/封面（32x32，圆角）
        Dim IconBox As New Border With {
            .Width = 32, .Height = 32,
            .CornerRadius = New CornerRadius(4),
            .ClipToBounds = True
        }
        If Not String.IsNullOrEmpty(Item.Cover) AndAlso IO.File.Exists(Item.Cover) Then
            Try
                Dim Bmp As BitmapImage
                '先从缓存找
                If ImageCache.ContainsKey(Item.Cover) Then
                    Bmp = ImageCache(Item.Cover)
                Else
                    Bmp = New BitmapImage()
                    Bmp.BeginInit()
                    Bmp.CacheOption = BitmapCacheOption.OnLoad
                    Bmp.UriSource = New Uri(Item.Cover)
                    Bmp.EndInit()
                    ImageCache(Item.Cover) = Bmp
                End If
                IconBox.Child = New Image With {.Source = Bmp, .Stretch = Stretch.UniformToFill}
            Catch
                IconBox.Child = DefaultIconCover(Item)
            End Try
        Else
            IconBox.Child = DefaultIconCover(Item)
        End If
        Grid.Children.Add(IconBox)
        Grid.SetColumn(IconBox, 0)

        ' 右侧文字区
        Dim TextStack As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        Grid.Children.Add(TextStack)
        Grid.SetColumn(TextStack, 2)

        ' 名称
        Dim NameBlock As New TextBlock With {
            .Text = Item.Name,
            .FontSize = 13,
            .Foreground = TryFindResource("ColorBrush1"),
            .TextTrimming = TextTrimming.CharacterEllipsis
        }
        TextStack.Children.Add(NameBlock)

        ' 路径/类型小字
        Dim SubBlock As New TextBlock With {
            .FontSize = 11,
            .Foreground = TryFindResource("ColorBrushGray3"),
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .Margin = New Thickness(0, 1, 0, 0)
        }
        Dim TypeText As String = If(Item.Type = "Web", "网页链接", If(Item.Type = "Exe", "可执行程序", "本地文件"))
        SubBlock.Text = TypeText & "  ·  " & If(Item.Url.Length > 50, "…" & Item.Url.Substring(Item.Url.Length - 47), Item.Url)
        TextStack.Children.Add(SubBlock)

        Row.Child = Grid

        ' 左键打开
        AddHandler Row.MouseLeftButtonUp, Sub(s, ev) OpenItem(Item)
        ' hover 效果
        AddHandler Row.MouseEnter, Sub(s, ev)
            Row.Background = TryFindResource("ColorBrushSemiTransparentHigh")
        End Sub
        AddHandler Row.MouseLeave, Sub(s, ev)
            Row.Background = TryFindResource("ColorBrushSemiTransparent")
        End Sub

        ' 右键菜单
        Dim Menu As New ContextMenu()
        Dim MiOpen As New MenuItem With {.Header = "打开"}
        AddHandler MiOpen.Click, Sub(s, ev) OpenItem(Item)
        Dim MiRename As New MenuItem With {.Header = "重命名"}
        AddHandler MiRename.Click, Sub(s, ev)
            Dim NewName As String = Interaction.InputBox("请输入新名称：", "重命名", Item.Name)
            If Not String.IsNullOrEmpty(NewName) Then
                Item.Name = NewName.Trim()
                SaveItems()
                RefreshCards()
            End If
        End Sub
        Dim MiFolder As New MenuItem With {.Header = "打开所在文件夹"}
        AddHandler MiFolder.Click, Sub(s, ev)
            Try
                If Item.Type = "Web" Then
                    OpenWebsite(Item.Url)
                Else
                    Dim Dir As String = IO.Path.GetDirectoryName(Item.Url)
                    If IO.Directory.Exists(Dir) Then
                        Process.Start(New ProcessStartInfo With {.FileName = "explorer.exe", .Arguments = """" & Dir & """", .UseShellExecute = True})
                    End If
                End If
            Catch
            End Try
        End Sub
        Dim MiDelete As New MenuItem With {.Header = "删除"}
        AddHandler MiDelete.Click, Sub(s, ev)
            If MyMsgBox($"是否删除 ""{Item.Name}""？", "删除链接", "删除", "取消") = 1 Then
                Items.Remove(Item)
                SaveItems()
                RefreshCards()
            End If
        End Sub
        Menu.Items.Add(MiOpen)
        Menu.Items.Add(MiRename)
        Menu.Items.Add(MiFolder)
        Menu.Items.Add(New Separator())
        Menu.Items.Add(MiDelete)
        Row.ContextMenu = Menu

        Return Row
    End Function

    Private Function DefaultIconCover(Item As ExtensionItem) As FrameworkElement
        Dim IconText As String = If(Item.Type = "Web", "🌐", If(Item.Type = "Exe", "⚙", "📄"))
        Dim Border As New Border With {
            .Background = TryFindResource("ColorBrushSemiTransparent"),
            .CornerRadius = New CornerRadius(6)
        }
        Border.Child = New TextBlock With {
            .Text = IconText,
            .FontSize = 22,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center
        }
        Return Border
    End Function

    ' ==================== 打开链接 ====================
    Private Sub OpenItem(Item As ExtensionItem)
        Try
            Select Case Item.Type
                Case "Web"
                    OpenWebsite(Item.Url)
                Case "Exe", "File"
                    Process.Start(New ProcessStartInfo With {.FileName = Item.Url, .UseShellExecute = True})
            End Select
        Catch ex As Exception
            MyMsgBox("打开失败：" & ex.Message, "错误")
        End Try
    End Sub

    ' ==================== 工具方法 ====================
    Private Function DownloadString(Url As String) As String
        Dim Req As HttpWebRequest = CType(WebRequest.Create(Url), HttpWebRequest)
        Req.Timeout = 10000
        Req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PCL-Extension"
        Req.Accept = "text/html"
        Using Resp As HttpWebResponse = CType(Req.GetResponse(), HttpWebResponse)
            Using Stream As IO.Stream = Resp.GetResponseStream()
                Using Reader As New IO.StreamReader(Stream)
                    Return Reader.ReadToEnd()
                End Using
            End Using
        End Using
    End Function

    Private Sub DownloadFile(Url As String, SavePath As String)
        Dim Req As HttpWebRequest = CType(WebRequest.Create(Url), HttpWebRequest)
        Req.Timeout = 10000
        Req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PCL-Extension"
        Using Resp As HttpWebResponse = CType(Req.GetResponse(), HttpWebResponse)
            Using Stream As IO.Stream = Resp.GetResponseStream()
                Using Fs As New IO.FileStream(SavePath, IO.FileMode.Create)
                    Stream.CopyTo(Fs)
                End Using
            End Using
        End Using
    End Sub

    Private Function ExtractOgImage(Html As String) As String
        Dim M As Match = Regex.Match(Html, "<meta[^>]+property=[""']og:image[""'][^>]+content=[""'](.*?)[""']", RegexOptions.IgnoreCase Or RegexOptions.Singleline)
        If Not M.Success Then
            M = Regex.Match(Html, "<meta[^>]+content=[""'](.*?)[""'][^>]+property=[""']og:image[""']", RegexOptions.IgnoreCase Or RegexOptions.Singleline)
        End If
        If Not M.Success Then Return ""
        Return M.Groups(1).Value
    End Function

End Class
