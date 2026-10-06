Public Class PageLinkLeft

    Private IsLoad As Boolean = False
    Private IsPageSwitched As Boolean = False

    Private Sub PageLinkLeft_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsLoad Then Return
        IsLoad = True
        ItemRoom.SetChecked(True, False, False)
    End Sub
    Private Sub PageLinkLeft_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        IsPageSwitched = False
    End Sub

#Region "页面切换"

    Public PageID As FormMain.PageSubType
    Public Sub New()
        InitializeComponent()
        PageID = FormMain.PageSubType.LinkRoom
    End Sub

    Private Sub PageCheck(sender As FrameworkElement, e As RouteEventArgs) Handles ItemRoom.Check, ItemServer.Check
        If sender.Tag Is Nothing Then Return
        Dim Tag As Integer = Val(sender.Tag)
        If Tag = 0 Then
            PageChange(FormMain.PageSubType.LinkRoom)
        Else
            PageChange(FormMain.PageSubType.LinkServer)
        End If
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.LinkRoom
                If FrmLinkMain Is Nothing Then FrmLinkMain = New PageLinkMain
                Return FrmLinkMain
            Case Else
                If FrmLinkMain Is Nothing Then FrmLinkMain = New PageLinkMain
                Return FrmLinkMain
        End Select
    End Function

    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        IsPageSwitched = True
        Try
            PageChangeRun(PageGet(ID))
            PageID = ID
            ' 通知 PageLinkMain 切换标签
            If FrmLinkMain IsNot Nothing Then
                If ID = FormMain.PageSubType.LinkRoom Then
                    FrmLinkMain.ShowRoom()
                Else
                    FrmLinkMain.ShowServer()
                End If
            End If
        Catch ex As Exception
            Logger.Error(ex, $"切换分页面失败（ID {ID}）")
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub
    Private Shared Sub PageChangeRun(Target As MyPageRight)
        AniStop("FrmMain PageChangeRight")
        If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
        FrmMain.PageRight = Target
        CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
        AniStart({
                         AaCode(Sub()
                                    CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                                    FrmMain.PanMainRight.Child = FrmMain.PageRight
                                    FrmMain.PageRight.Opacity = 0
                                End Sub, 130),
                         AaCode(Sub()
                                    FrmMain.PageRight.Opacity = 1
                                    FrmMain.PageRight.PageOnEnter()
                                End Sub, 30, True)
                     }, "PageLeft PageChange")
    End Sub

#End Region

End Class
