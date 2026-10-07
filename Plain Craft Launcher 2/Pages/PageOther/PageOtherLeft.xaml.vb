Public Class PageOtherLeft

    Private IsLoad As Boolean = False
    Private IsPageSwitched As Boolean = False

    Private Sub PageOtherLeft_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsLoad Then Return
        IsLoad = True
        ItemTest.SetChecked(True, False, False)
    End Sub
    Private Sub PageOtherLeft_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        IsPageSwitched = False
    End Sub

#Region "页面切换"

    Public PageID As FormMain.PageSubType
    Public Sub New()
        InitializeComponent()
        PageID = FormMain.PageSubType.OtherTest
    End Sub

    Private Sub PageCheck(sender As FrameworkElement, e As RouteEventArgs) Handles ItemTest.Check, ItemExtensions.Check, ItemMulti.Check, ItemCrawler.Check
        If sender.Tag Is Nothing Then Return
        Dim Tag As Integer = Val(sender.Tag)
        If Tag = 0 Then
            PageChange(FormMain.PageSubType.OtherTest)
        ElseIf Tag = 2 Then
            PageChange(FormMain.PageSubType.OtherMulti)
        ElseIf Tag = 4 Then
            PageChange(FormMain.PageSubType.OtherCrawler)
        Else
            If FrmExtensionsMain Is Nothing Then FrmExtensionsMain = New PageExtensions
            PageChangeRun(FrmExtensionsMain)
            PageID = -1
        End If
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.OtherTest
                If FrmOtherTest Is Nothing Then FrmOtherTest = New PageOtherTest
                Return FrmOtherTest
            Case FormMain.PageSubType.OtherMulti
                If FrmOtherMulti Is Nothing Then FrmOtherMulti = New PageMulti
                Return FrmOtherMulti
            Case FormMain.PageSubType.OtherCrawler
                If FrmOtherCrawler Is Nothing Then FrmOtherCrawler = New PageOtherCrawler
                Return FrmOtherCrawler
            Case Else
                If FrmExtensionsMain Is Nothing Then FrmExtensionsMain = New PageExtensions
                Return FrmExtensionsMain
        End Select
    End Function

    ' 用数字判断子页面
    Public Sub PageChangeByTag(Tag As Integer)
        If Tag = 0 Then
            PageChange(FormMain.PageSubType.OtherTest)
        Else
            ' 拓展页
            If FrmExtensionsMain Is Nothing Then FrmExtensionsMain = New PageExtensions
            PageChangeRun(FrmExtensionsMain)
            PageID = -1 ' 标记为拓展页
        End If
    End Sub

    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        IsPageSwitched = True
        Try
            PageChangeRun(PageGet(ID))
            PageID = ID
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
