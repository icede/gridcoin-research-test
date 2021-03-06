﻿Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports System.Drawing
Imports System.Diagnostics
Imports System.Timers
Imports System.Windows.Forms.DataVisualization.Charting
Imports System.Threading
Imports BoincStake

Public Class frmMining
    Private MaxHR As Double = 1
    Private LastMHRate As String = ""
    Private lMHRateCounter As Long = 0
    Private mIDelay As Long = 0
    Private msNeuralReport As String = ""
    Private WM_SETREDRAW = &HB


    Private RefreshCount As Long
    Private bUICharted As Boolean = False
    Public bDisposing As Boolean
    Public bSuccessfullyLoaded As Boolean
    Private bCharting As Boolean
    Private mEnabled(10) As Boolean
    Private msReaperOut(10) As String
    Private miInitCounter As Long
    Private msLastBlockHash As String
    Private mlElapsedTime As Long
    Private msLastSleepStatus As String
  

    Private Sub UpdateCharts()
        Try
            ChartBoinc()
            UpdateChartHashRate()
           
            Me.Update()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub OneMinuteUpdate()
        Try

            ChartBoinc()
            lblCPID.Text = "CPID: " + KeyValue("PrimaryCPID")
            Dim r As Row = GetDataValue("Historical", "Magnitude", "LastTimeSynced")

            lblLastSynced.Text = "Last Synced: " + Trim(r.Synced)
            r = GetDataValue("Historical", "Magnitude", "QuorumHash")

            lblSuperblockAge.Text = "Superblock Age: " + Trim(r.DataColumn1)
            lblQuorumHash.Text = "Popular Quorum Hash: " + Trim(r.DataColumn2)
            lblTimestamp.Text = "Superblock Timestamp: " + Trim(r.DataColumn3)
            lblBlock.Text = "Superblock Block #: " + Trim(r.DataColumn4)


        Catch exx As Exception
            Log("One minute update:" + exx.Message)
        End Try
    End Sub
    

    Public Sub ChartBoinc()
        'Dim seriesAvgCredits As New Series
        Dim seriesNetworkMagnitude As New Series
        Dim seriesUserMagnitude As New Series

        Try
            If bCharting Then Exit Sub
            bCharting = True
            If Chart1.Titles.Count < 1 Then
                Chart1.Series.Clear()

                Chart1.Titles.Clear()
                Chart1.Titles.Add("Historical Contribution")
                Chart1.Titles(0).ForeColor = Color.LightGreen
                Chart1.BackColor = Color.Transparent : Chart1.ForeColor = Color.Lime
                Chart1.ChartAreas(0).AxisX.IntervalType = DateTimeIntervalType.Weeks : Chart1.ChartAreas(0).AxisX.TitleForeColor = Color.White
                Chart1.ChartAreas(0).BackSecondaryColor = Color.Transparent : Chart1.ChartAreas(0).AxisX.LabelStyle.ForeColor = Color.Lime
                Chart1.ChartAreas(0).AxisY.LabelStyle.ForeColor = Color.Lime : Chart1.ChartAreas(0).ShadowColor = Color.Chocolate
                Chart1.ChartAreas(0).BackSecondaryColor = Color.Gray : Chart1.ChartAreas(0).BorderColor = Color.Gray
                Chart1.Legends(0).ForeColor = Color.Lime
                Chart1.ChartAreas(0).AxisX.LabelStyle.Format = "MM-dd-yyyy"
                Chart1.ChartAreas(0).AxisX.Interval = 2
                Chart1.ForeColor = Color.GreenYellow
                'Network Magnitude
                seriesNetworkMagnitude.ChartType = SeriesChartType.FastLine
                seriesNetworkMagnitude.Name = "Network Magnitude"
                ' seriesNetworkMagnitude.LabelForeColor = Color.GreenYellow
                Chart1.Series.Add(seriesNetworkMagnitude)
                'User Magnitude
                seriesUserMagnitude.ChartType = SeriesChartType.FastLine
                seriesUserMagnitude.Name = "User Magnitude"
                'seriesUserMagnitude.LabelForeColor = Color.GreenYellow
                Chart1.Series.Add(seriesUserMagnitude)
            End If
            seriesNetworkMagnitude.Points.Clear()
            seriesUserMagnitude.Points.Clear()
            '''''''''''''''''''''''''''''' Chart Bar of Historical Contribution ''''''''''''''''''''''''''''''''''''''''
            Dim lUserMag As Double = 0
            Dim lNetworkMag As Double = 0
            Dim lAvgNetMag As Double = 0
            Dim lAvgUserMag As Double = 0
            Dim sCPID As String = KeyValue("PrimaryCPID")
            For x = 30 To 1 Step -1
                'Dim dpAvgCredits As New DataPoint
                'dpAvgCredits.SetValueXY(ChartDate, lAvgCredits)
                'seriesAvgCredits.Points.Add(pCreditsAvg)
                Dim ChartDate As Date = DateAdd(DateInterval.Day, -x, Now)
                lUserMag = GetHistoricalMagnitude(ChartDate, sCPID, lAvgUserMag)
                lNetworkMag = GetHistoricalMagnitude(ChartDate, "Network", lAvgNetMag)
                Dim dpUserMag As New DataPoint()
                dpUserMag.SetValueXY(ChartDate, lUserMag)
                seriesUserMagnitude.Points.Add(dpUserMag)
                Dim dpNetworkMag As New DataPoint()
                dpNetworkMag.SetValueXY(ChartDate, lAvgNetMag)
                seriesNetworkMagnitude.Points.Add(dpNetworkMag)
            Next
            '''''''''''''''''''''''''''''''  Chart Pie of Current Contribution '''''''''''''''''''''''''''''''''''''''''
            Call ChartBoincUtilization(lUserMag, lAvgNetMag)

        Catch ex As Exception
        End Try
        bCharting = False
    End Sub

    Public Sub ChartBoincUtilization(bu As Long, netBU As Long)
        Try
            If chtCurCont.Titles.Count < 1 Then
                chtCurCont.Series.Clear()
                chtCurCont.Titles.Clear()
                chtCurCont.BackColor = Color.Transparent : chtCurCont.ForeColor = Color.Blue
                chtCurCont.Titles.Add("Contribution")
                chtCurCont.Titles(0).ForeColor = Color.LightGreen
                chtCurCont.ChartAreas(0).BackColor = Color.Transparent
                chtCurCont.ChartAreas(0).BackSecondaryColor = Color.White
                chtCurCont.Legends(0).BackColor = Color.Transparent
                chtCurCont.Legends(0).ForeColor = Color.Honeydew
                Dim sUtilization As New Series
                sUtilization.Name = "Magnitude" : sUtilization.ChartType = SeriesChartType.Pie
                sUtilization.LegendText = "Boinc Magnitude"
                sUtilization.LabelBackColor = Color.Lime : sUtilization.IsValueShownAsLabel = False
                sUtilization.LabelForeColor = Color.Honeydew
                chtCurCont.Series.Add(sUtilization)
            End If
            chtCurCont.Series(0).Points.Clear()
            If Not bUICharted Then bUICharted = True : bu = 2
            chtCurCont.Series(0).Points.AddY(bu)
            chtCurCont.Series(0).LabelBackColor = Color.Transparent
            chtCurCont.Series(0).Points(0).Label = Trim(bu)
            chtCurCont.Series(0).Points(0).Color = Color.Blue
            chtCurCont.Series(0).Points(0).LegendToolTip = Trim(bu) + " magnitude"
            chtCurCont.Series(0).Points.AddY(netBU - bu)
            chtCurCont.Series(0).Points(1).IsVisibleInLegend = False
            chtCurCont.Series(0)("PointWidth") = "0.5"
            chtCurCont.Series(0).IsValueShownAsLabel = False
            chtCurCont.Series(0)("BarLabelStyle") = "Center"
            chtCurCont.ChartAreas(0).Area3DStyle.Enable3D = True
            chtCurCont.Series(0)("DrawingStyle") = "Cylinder"
        Catch ex As Exception

        End Try
    End Sub


    Private Sub frmMining_Activated(sender As Object, e As System.EventArgs) Handles Me.Activated

    End Sub
    Private Sub frmMining_FormClosing(ByVal sender As Object, ByVal e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing

        Me.Hide()
        e.Cancel = True
    End Sub


    Private Sub btnHide_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnHide.Click
        Me.Hide()
    End Sub

    Private Sub HideToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles HideToolStripMenuItem.Click
        Me.Hide()
    End Sub


    Public Sub UpdateChartHashRate()

        Try
            ChartHashRate.Series.Clear()
            ChartHashRate.Titles.Clear()
            ChartHashRate.BackColor = Color.Transparent
            ChartHashRate.ForeColor = Color.Red
            ChartHashRate.Titles.Add("GPU Hash Rate")
            ChartHashRate.Titles(0).ForeColor = Color.Green

            ChartHashRate.ChartAreas(0).BackColor = Color.Transparent
            ChartHashRate.ChartAreas(0).BackSecondaryColor = Color.PaleVioletRed
            Dim sHR As New Series
            sHR.Name = "HR"
            sHR.ChartType = SeriesChartType.Pie
            sHR.LabelBackColor = Color.Lime
            sHR.IsValueShownAsLabel = False
            sHR.LabelForeColor = Color.Honeydew
            ChartHashRate.Series.Add(sHR)


        Catch ex As Exception
        End Try

    End Sub
    Private Sub frmMining_Load(sender As Object, e As System.EventArgs) Handles Me.Load

        Try

            Call OneMinuteUpdate()
            Me.TabControl1.SelectedIndex = 2
            If mbTestNet Then lblTestnet.Text = "TESTNET"

        Catch ex As Exception

        End Try


    End Sub


    Public Sub New()
        InitializeComponent()
    End Sub

    
    Public Sub PopulateNeuralData()

        Dim sReport As String = ""
        Dim sReportRow As String = ""

        Dim sHeader As String = "CPID,Local Magnitude,Neural Magnitude,Total RAC,Synced Til,Address,CPID Valid"
        sReport += sHeader + vbCrLf
        dgv.Rows.Clear()
        dgv.Columns.Clear()
        dgv.BackgroundColor = Drawing.Color.Black
        dgv.ForeColor = Drawing.Color.Lime
        Dim grr As New GridcoinReader.GridcoinRow
        Dim sHeading As String = "CPID;Local Magnitude;Neural Magnitude;Total RAC;Synced Til;Address;CPID Valid"
        Dim vHeading() As String = Split(sHeading, ";")
        PopulateHeadings(vHeading, dgv, False)
        Dim sData As String = modPersistedDataSystem.GetMagnitudeContractDetails()
        Dim vData() As String = Split(sData, ";")
        Dim iRow As Long = 0
        Dim sValue As String
        'dgv.Visible = False
        Me.Cursor.Current = Cursors.WaitCursor
        dgv.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.ColumnHeader)
        dgv.ReadOnly = True
        dgv.EditingPanel.Visible = False
  
        For y = 0 To UBound(vData) - 1
            dgv.Rows.Add()
            sReportRow = ""
            For x = 0 To UBound(vHeading)
                Dim vRow() As String = Split(vData(y), ",")
                sValue = vRow(x)
                'Sort numerically:
                If x = 1 Or x = 2 Or x = 3 Then
                    dgv.Rows(iRow).Cells(x).Value = Val(sValue)
                Else
                    dgv.Rows(iRow).Cells(x).Value = sValue
                End If
                sReportRow += sValue + ","
            Next x
            sReport += sReportRow + vbCrLf
            iRow = iRow + 1
            If iRow Mod 10 = 0 Then Application.DoEvents()

        Next
  
        SetAutoSizeMode2(vHeading, dgv)

        Me.Cursor.Current = Cursors.Default

        'Get the Neural Hash
        Dim sMyNeuralHash As String
        Dim sContract = GetMagnitudeContract()
        sMyNeuralHash = GetQuorumHash(sContract)
        dgv.Rows.Add()
        dgv.Rows(iRow).Cells(0).Value = "Hash: " + sMyNeuralHash + " (" + Trim(iRow) + ")"
        sReport += "Hash: " + sMyNeuralHash + " (" + Trim(iRow) + ")"
        msNeuralReport = sReport

        'Populate Projects

        dgvProjects.Rows.Clear()
        dgvProjects.Columns.Clear()
        dgvProjects.BackgroundColor = Drawing.Color.Black
        dgvProjects.ForeColor = Drawing.Color.Lime
        sHeading = "Project Name;Total RAC;Avg RAC;Whitelisted"
        vHeading = Split(sHeading, ";")

        PopulateHeadings(vHeading, dgvProjects, False)

        Dim surrogateRow As New Row
        Dim lstWhitelist As List(Of Row)
        Dim surrogateWhitelistRow As New Row
        surrogateWhitelistRow.Database = "Whitelist"
        surrogateWhitelistRow.Table = "Whitelist"
        lstWhitelist = GetList(surrogateWhitelistRow, "*")
        Dim WhitelistedProjects As Double = 0
        Dim PrjCount As Double = 0
        iRow = 0

        'Loop through the whitelist
        lstWhitelist.Sort(Function(x, y) x.PrimaryKey.CompareTo(y.PrimaryKey))
        Dim rPRJ As New Row
        rPRJ.Database = "Project"
        rPRJ.Table = "Projects"

        Dim lstProjects As List(Of Row) = GetList(rPRJ, "*")
        lstProjects.Sort(Function(x, y) x.PrimaryKey.CompareTo(y.PrimaryKey))

        PrjCount = lstWhitelist.Count
        For Each prj As Row In lstProjects
            Dim bIsThisWhitelisted = IsInList(prj.PrimaryKey, lstWhitelist, False)
            If bIsThisWhitelisted Then
                WhitelistedProjects += 1
            End If
            dgvProjects.Rows.Add()
            dgvProjects.Rows(iRow).Cells(0).Value = prj.PrimaryKey
            dgvProjects.Rows(iRow).Cells(1).Value = prj.RAC
            dgvProjects.Rows(iRow).Cells(2).Value = prj.AvgRAC

            dgvProjects.Rows(iRow).Cells(3).Value = Trim(bIsThisWhitelisted)
            iRow = iRow + 1
        Next

        lblTotalProjects.Text = Trim(PrjCount)
        lblWhitelistedProjects.Text = Trim(WhitelistedProjects)


    End Sub

    Private Sub TabControl1_SelectedIndexChanged(sender As Object, e As System.EventArgs) Handles TabControl1.SelectedIndexChanged
        If TabControl1.SelectedIndex = 2 Then
            PopulateNeuralData()

        End If
    End Sub

    Private Sub dgv_CellContentDoubleClick(sender As Object, e As System.Windows.Forms.DataGridViewCellEventArgs) Handles dgv.CellContentDoubleClick
        'Drill into CPID
        If e.RowIndex < 0 Then Exit Sub
        'Get whitelist total first
        Dim lstWhitelist As List(Of Row)
        Dim surrogateWhitelistRow As New Row
        surrogateWhitelistRow.Database = "Whitelist"
        surrogateWhitelistRow.Table = "Whitelist"
        lstWhitelist = GetList(surrogateWhitelistRow, "*")
        Dim rPRJ As New Row
        rPRJ.Database = "Project"
        rPRJ.Table = "Projects"
        Dim lstProjects1 As List(Of Row) = GetList(rPRJ, "*")
        lstProjects1.Sort(Function(x, y) x.PrimaryKey.CompareTo(y.PrimaryKey))
        Dim WhitelistedProjects As Double = GetWhitelistedCount(lstProjects1, lstWhitelist)
        Dim TotalProjects As Double = lstProjects1.Count
        Dim PrjCount As Double = 0

        'Loop through the whitelist
        lstWhitelist.Sort(Function(x, y) x.PrimaryKey.CompareTo(y.PrimaryKey))
        Dim TotalRAC As Double = 0
        Dim TotalNetworkRAC As Double = 0

        'Drill
        Dim sCPID As String = Trim(dgv.Rows(e.RowIndex).Cells(0).Value)
        If sCPID.Contains("Hash") Then Exit Sub
        If Len(sCPID) > 1 Then
            '7-10-2015 - Expose Project Mag and Cumulative Mag:
            Dim dgvProjects As New DataGridView
            Dim sHeading As String = "CPID,Project,RAC,Project Total RAC,Project Avg RAC,Project Mag,Cumulative RAC,Cumulative Mag"
            Dim vHeading() As String = Split(sHeading, ",")
            PopulateHeadings(vHeading, dgvProjects, True)
            Dim surrogatePrj As New Row
            surrogatePrj.Database = "Project"
            surrogatePrj.Table = "Projects"
            Dim lstProjects As List(Of Row) = GetList(surrogatePrj, "*")
            Dim iRow As Long = 0
            dgvProjects.Rows.Clear()

            Dim CumulativeMag As Double = 0
            For Each prj As Row In lstProjects
                Dim surrogatePrjCPID As New Row
                surrogatePrjCPID.Database = "Project"
                surrogatePrjCPID.Table = prj.PrimaryKey + "CPID"
                surrogatePrjCPID.PrimaryKey = prj.PrimaryKey + "_" + sCPID
                Dim rowRAC = Read(surrogatePrjCPID)
                Dim CPIDRAC As Double = Val(rowRAC.RAC)
                Dim PrjRAC As Double = Val(prj.RAC)
                If CPIDRAC > 0 Then
                    iRow += 1
                    dgvProjects.Rows.Add()
                    dgvProjects.Rows(iRow - 1).Cells(0).Value = sCPID
                    dgvProjects.Rows(iRow - 1).Cells(1).Value = prj.PrimaryKey
                    dgvProjects.Rows(iRow - 1).Cells(2).Value = Val(Trim(CPIDRAC))
                    dgvProjects.Rows(iRow - 1).Cells(3).Value = Val(Trim(prj.RAC))
                    dgvProjects.Rows(iRow - 1).Cells(4).Value = Val(Trim(prj.AvgRAC))
                    'Cumulative Mag:
                    Dim bIsThisWhitelisted As Boolean = False
                    bIsThisWhitelisted = IsInList(prj.PrimaryKey, lstWhitelist, False)
                    Dim IndMag As Double = 0
                    If Not bIsThisWhitelisted Then
                        dgvProjects.Rows(iRow - 1).Cells(2).Style.BackColor = Color.Red
                    End If

                    If bIsThisWhitelisted Then
                        IndMag = Math.Round(((CPIDRAC / (PrjRAC + 0.01)) / (WhitelistedProjects + 0.01)) * NeuralNetworkMultiplier, 2)
                        CumulativeMag += IndMag
                        TotalRAC += CPIDRAC
                        TotalNetworkRAC += PrjRAC
                    End If
                    dgvProjects.Rows(iRow - 1).Cells(5).Value = Val(IndMag)
                    dgvProjects.Rows(iRow - 1).Cells(6).Value = Val(RoundedMag(TotalRAC))
                    dgvProjects.Rows(iRow - 1).Cells(7).Value = Val(RoundedMag(CumulativeMag))

                End If

            Next

            'Formula for individual drill-in for Magnitude
            'Magnitude = (TotalRACContributions  /  ProjectRAC) / (WhitelistedProjectsCount)) * NeuralNetworkMultiplier

            iRow += 1
            dgvProjects.Rows.Add()

            dgvProjects.Rows(iRow - 1).Cells(0).Value = "Total Mag: " + Trim(RoundedMag(CumulativeMag))

            dgvProjects.Rows(iRow - 1).Cells(3).Value = RoundedMag(TotalNetworkRAC)


            dgvProjects.Rows(iRow - 1).Cells(6).Value = RoundedMag(TotalRAC)
            dgvProjects.Rows(iRow - 1).Cells(7).Value = RoundedMag(CumulativeMag)

            Dim oNewForm As New Form
            oNewForm.Width = Screen.PrimaryScreen.WorkingArea.Width / 1.6
            oNewForm.Height = Screen.PrimaryScreen.WorkingArea.Height / 2.2
            oNewForm.Text = "CPID Magnitude Details - Gridcoin Neural Network - (Red=Blacklisted)"

            oNewForm.Controls.Add(dgvProjects)
            dgvProjects.Left = 5
            dgvProjects.Top = 5
            Dim TotalControlHeight As Long = (dgvProjects.RowTemplate.Height * (iRow + 2)) + 20
            dgvProjects.Height = TotalControlHeight
            oNewForm.Height = dgvProjects.Height + 285
            dgvProjects.Width = oNewForm.Width - 25
            Dim rtbRac As New System.Windows.Forms.RichTextBox

            Dim sXML As String = GetXMLOnly(sCPID)
            rtbRac.Left = 5
            rtbRac.Top = dgvProjects.Height + 8
            rtbRac.Height = 245
            rtbRac.Width = oNewForm.Width - 5
            rtbRac.Text = sXML
            oNewForm.Controls.Add(rtbRac)
            oNewForm.Show()
        End If
    End Sub

    Private Sub ContractDetailsToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles ContractDetailsToolStripMenuItem.Click
        Dim sData As String = GetMagnitudeContract()
        Dim sMags As String = ExtractXML(sData, "<MAGNITUDES>")
        Dim vCt() As String = Split(sMags, ";")
        Dim sHash As String = GetQuorumHash(sData)
        MsgBox(sData + " - Count " + Trim(vCt.Length() - 1) + " - Hash " + sHash)
    End Sub

    Private Sub btnExport_Click(sender As System.Object, e As System.EventArgs) Handles btnExport.Click
        Dim sWritePath As String = GetGridFolder() + "reports\NeuralMagnitudeReport.csv"
        If Not System.IO.Directory.Exists(GetGridFolder() + "reports") Then MkDir(GetGridFolder() + "reports")
        Using objWriter As New System.IO.StreamWriter(sWritePath)
            objWriter.WriteLine(msNeuralReport)
            objWriter.Close()
        End Using
        ExportToCSV2()
        MsgBox("Exported to Reports\" + "NeuralMagnitudeReport.csv")
    End Sub
    Private Sub TextBox1_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtSearch.TextChanged
        Dim sPhrase As String = txtSearch.Text
        For y = 1 To dgv.Rows.Count - 1
            For x = 0 To dgv.Rows(y).Cells.Count - 1
                If LCase(Trim("" & dgv.Rows(y).Cells(x).Value)) Like LCase(Trim(txtSearch.Text)) + "*" Then
                    dgv.Rows(y).Selected = True
                    dgv.CurrentCell = dgv.Rows(y).Cells(0)
                    Exit Sub
                End If
            Next
        Next
    End Sub

    Private Sub btnRefresh_Click(sender As System.Object, e As System.EventArgs) Handles btnRefresh.Click
        PopulateNeuralData()
        Call OneMinuteUpdate()
        If ((Rnd(1) * 1000) < 333) Then
            'Ask the other nodes what the averages are...
            pbSync.Visible = True
            pbSync.Maximum = 100
            pbSync.Value = 50
            Try
                ReconnectToNeuralNetwork()
                Dim sMemoryName = IIf(mbTestNet, "magnitudes_testnet", "magnitudes")
                mdictNeuralNetworkMemories = mGRCData.GetNeuralNetworkQuorumData(sMemoryName)
            Catch ex As Exception
                Log("Unable to connect to neural network for memories.")
            End Try
            Threading.Thread.Sleep(10)
            pbSync.Value = 0
            pbSync.Visible = False

        End If
    End Sub

    Private Sub TimerSync_Tick(sender As System.Object, e As System.EventArgs) Handles TimerSync.Tick
        If mlPercentComplete <> 0 Then
            pbSync.Visible = True
            pbSync.Maximum = 100
            If mlPercentComplete <= pbSync.Maximum Then pbSync.Value = mlPercentComplete
            Application.DoEvents()
            If mlPercentComplete < 50 Then pbSync.ForeColor = Color.Red
            If mlPercentComplete > 50 And mlPercentComplete < 90 Then pbSync.ForeColor = Color.Yellow
            If mlPercentComplete > 90 Then pbSync.ForeColor = Color.Green
        Else
            If pbSync.Visible = True Then pbSync.Visible = False : PopulateNeuralData()
            pbSync.Visible = False
        End If
    End Sub

    Private Sub PoolsToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles PoolsToolStripMenuItem.Click

    End Sub

    Private Sub InstallGridcoinGalazaToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles InstallGridcoinGalazaToolStripMenuItem.Click
        InstallGalaza()

    End Sub

    Private Sub tOneMinute_Tick(sender As System.Object, e As System.EventArgs) Handles tOneMinute.Tick
        Call OneMinuteUpdate()

    End Sub
End Class