#Requires -Version 5.1
# ViVeToolGUI.ps1 - WPF GUI for ViVeTool Feature Enabler
# Loads feature catalog LIVE from pureinfotech.com on every launch.
# Run from any terminal - auto-elevates to Administrator.

param([switch]$NoAutoElevate)

# Auto-elevate
if (-not $NoAutoElevate) {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        $argList = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -NoAutoElevate"
        Start-Process powershell.exe -ArgumentList $argList -Verb RunAs
        exit
    }
}

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }

# ---- ViveTool path ----
$ViveExe = Join-Path $ScriptDir "vivetool.exe"
if (-not (Test-Path $ViveExe)) {
    $onPath = Get-Command vivetool.exe -ErrorAction SilentlyContinue
    if ($onPath) { $ViveExe = $onPath.Source } else { $ViveExe = $null }
}

# ---- XAML UI ----
[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="ViVeTool Feature Enabler  |  Live from Pureinfotech" Height="800" Width="1150"
        MinHeight="620" MinWidth="860"
        WindowStartupLocation="CenterScreen"
        Background="#1C1C1C" Foreground="#FFFFFF">
    <Window.Resources>
        <Style TargetType="Button">
            <Setter Property="Padding" Value="14,8"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Foreground" Value="White"/>
        </Style>
        <Style TargetType="CheckBox">
            <Setter Property="Foreground" Value="#CCCCCC"/>
            <Setter Property="VerticalAlignment" Value="Center"/>
        </Style>
        <Style TargetType="TextBox">
            <Setter Property="Background" Value="#2D2D2D"/>
            <Setter Property="Foreground" Value="#FFFFFF"/>
            <Setter Property="BorderBrush" Value="#444"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="8,6"/>
            <Setter Property="FontSize" Value="13"/>
        </Style>
        <Style TargetType="ListView">
            <Setter Property="Background" Value="#1C1C1C"/>
            <Setter Property="BorderBrush" Value="#333"/>
            <Setter Property="Foreground" Value="#CCCCCC"/>
        </Style>
        <Style TargetType="ListViewItem">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="#CCCCCC"/>
            <Setter Property="Padding" Value="2,3"/>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#2A2A2A"/>
                </Trigger>
                <Trigger Property="IsSelected" Value="True">
                    <Setter Property="Background" Value="#0D3352"/>
                    <Setter Property="Foreground" Value="White"/>
                </Trigger>
            </Style.Triggers>
        </Style>
        <Style TargetType="GridViewColumnHeader">
            <Setter Property="Background" Value="#252525"/>
            <Setter Property="Foreground" Value="#AAAAAA"/>
            <Setter Property="BorderBrush" Value="#444"/>
            <Setter Property="Padding" Value="8,5"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
        </Style>
    </Window.Resources>
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="52"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="185"/>
            <RowDefinition Height="32"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <Border Grid.Row="0" Background="#111111">
            <Grid Margin="16,0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                    <TextBlock Text="&#xE770;" FontFamily="Segoe MDL2 Assets" FontSize="22" Foreground="#0078D4" VerticalAlignment="Center" Margin="0,0,10,0"/>
                    <TextBlock Text="ViVeTool Feature Enabler" FontSize="18" FontWeight="Bold" Foreground="White" VerticalAlignment="Center"/>
                    <TextBlock x:Name="txtBuildInfo" Text="" FontSize="11" Foreground="#888" VerticalAlignment="Center" Margin="16,2,0,0"/>
                </StackPanel>
                <Button x:Name="btnRefresh" Grid.Column="1" Content="&#x21BA;  Refresh from Web" Background="#1A3A1A"
                        Foreground="#88FF88" FontSize="11" Padding="10,6" BorderThickness="1" BorderBrush="#2A6A2A"
                        ToolTip="Re-fetch the latest codes from pureinfotech.com"/>
            </Grid>
        </Border>

        <!-- Main content -->
        <Grid Grid.Row="1" Margin="12,8,12,4">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="16"/>
                <ColumnDefinition Width="285"/>
            </Grid.ColumnDefinitions>

            <!-- Left: Feature List -->
            <Border Grid.Column="0" Background="#232323" CornerRadius="6" Padding="0">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="44"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <Border Grid.Row="0" Background="#1A1A1A" CornerRadius="6,6,0,0" Padding="8,4">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="145"/>
                            </Grid.ColumnDefinitions>
                            <TextBox x:Name="txtSearch" Grid.Column="0" Text="" ToolTip="Search by description or ID number..."/>
                            <ComboBox x:Name="cmbGroup" Grid.Column="1" Margin="8,0,0,0"
                                      Background="#2D2D2D" Foreground="White" BorderBrush="#444" Padding="6,4"/>
                        </Grid>
                    </Border>
                    <Grid Grid.Row="1">
                        <ListView x:Name="lvFeatures" SelectionMode="Extended">
                            <ListView.View>
                                <GridView>
                                    <GridViewColumn Width="36">
                                        <GridViewColumn.CellTemplate>
                                            <DataTemplate>
                                                <CheckBox IsChecked="{Binding IsSelected, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" HorizontalAlignment="Center"/>
                                            </DataTemplate>
                                        </GridViewColumn.CellTemplate>
                                    </GridViewColumn>
                                    <GridViewColumn Header="Track" Width="85" DisplayMemberBinding="{Binding Group}"/>
                                    <GridViewColumn Header="Build / Update" Width="210" DisplayMemberBinding="{Binding BuildLabel}"/>
                                    <GridViewColumn Header="Feature Description" Width="310" DisplayMemberBinding="{Binding Description}"/>
                                    <GridViewColumn Header="ID(s)" Width="170" DisplayMemberBinding="{Binding IDsDisplay}"/>
                                </GridView>
                            </ListView.View>
                        </ListView>
                        <!-- Loading overlay -->
                        <Border x:Name="loadingOverlay" Background="#CC1C1C1C" CornerRadius="0,0,6,6" Visibility="Collapsed">
                            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
                                <TextBlock x:Name="loadingText" Text="Fetching latest codes from pureinfotech.com..." FontSize="14" Foreground="#0078D4" HorizontalAlignment="Center"/>
                                <ProgressBar IsIndeterminate="True" Height="4" Width="300" Margin="0,12,0,0" Foreground="#0078D4" Background="#333" BorderThickness="0"/>
                            </StackPanel>
                        </Border>
                    </Grid>
                </Grid>
            </Border>

            <!-- Right Panel -->
            <StackPanel Grid.Column="2" Spacing="8">

                <!-- Summary Card -->
                <Border Background="#232323" CornerRadius="6" Padding="14,10">
                    <StackPanel>
                        <TextBlock Text="Selection" FontWeight="Bold" FontSize="12" Foreground="#888" Margin="0,0,0,6"/>
                        <TextBlock x:Name="txtSummary" Text="Loading..." FontSize="13" Foreground="White" FontWeight="SemiBold"/>
                        <TextBlock x:Name="txtLastUpdated" Text="" FontSize="10" Foreground="#555" Margin="0,4,0,0"/>
                        <ProgressBar x:Name="pbProgress" Height="4" Margin="0,8,0,0" Foreground="#0078D4" Background="#444" BorderThickness="0" Minimum="0" Maximum="100" Value="0"/>
                    </StackPanel>
                </Border>

                <!-- Select Buttons -->
                <Border Background="#232323" CornerRadius="6" Padding="10,8">
                    <StackPanel Spacing="6">
                        <Grid>
                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="6"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                            <Button x:Name="btnSelectAll"   Grid.Column="0" Content="Select All"   Background="#2D5A8E" FontSize="11"/>
                            <Button x:Name="btnClearAll"    Grid.Column="2" Content="Clear All"    Background="#4A3728" FontSize="11"/>
                        </Grid>
                        <Grid>
                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="6"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                            <Button x:Name="btnSelectGroup" Grid.Column="0" Content="Sel. Group"   Background="#2D4A2D" FontSize="10"/>
                            <Button x:Name="btnClearGroup"  Grid.Column="2" Content="Clr. Group"   Background="#4A2D2D" FontSize="10"/>
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- Action Buttons -->
                <Border Background="#232323" CornerRadius="6" Padding="10,8">
                    <StackPanel Spacing="6">
                        <Button x:Name="btnEnable"  Content="Enable Selected" Background="#0078D4" FontSize="13"/>
                        <Button x:Name="btnDisable" Content="Disable / Rollback" Background="#C05000" FontSize="11"/>
                    </StackPanel>
                </Border>

                <!-- Options -->
                <Border Background="#232323" CornerRadius="6" Padding="10,8">
                    <StackPanel Spacing="6">
                        <TextBlock Text="Options" FontWeight="Bold" FontSize="11" Foreground="#888" Margin="0,0,0,2"/>
                        <CheckBox x:Name="chkWhatIf"  Content="Dry-Run (-WhatIf)"           Foreground="#CCC" FontSize="11"/>
                        <CheckBox x:Name="chkRestart" Content="Restart Explorer when done"  Foreground="#CCC" FontSize="11" IsChecked="True"/>
                        <Button   x:Name="btnDownload" Content="Download ViVeTool" Background="#2A2A2A" FontSize="10" BorderBrush="#555" BorderThickness="1" Margin="0,4,0,0"/>
                    </StackPanel>
                </Border>

                <!-- ViveTool Status -->
                <Border Background="#232323" CornerRadius="6" Padding="10,8">
                    <StackPanel>
                        <TextBlock Text="ViVeTool" FontWeight="Bold" FontSize="11" Foreground="#888" Margin="0,0,0,4"/>
                        <TextBlock x:Name="txtViveStatus" Text="Checking..." FontSize="10" Foreground="#AAAAAA" TextWrapping="Wrap"/>
                    </StackPanel>
                </Border>

            </StackPanel>
        </Grid>

        <!-- Log Panel -->
        <Border Grid.Row="2" Margin="12,0,12,4" Background="#0D1117" CornerRadius="6">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="24"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>
                <Border Grid.Row="0" Background="#161B22" CornerRadius="6,6,0,0" Padding="10,3">
                    <Grid>
                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                        <TextBlock Text="Output Log" FontSize="10" FontWeight="SemiBold" Foreground="#666" VerticalAlignment="Center"/>
                        <Button x:Name="btnClearLog" Grid.Column="1" Content="Clear" Background="Transparent" Foreground="#555" FontSize="9" Padding="6,1" BorderThickness="0"/>
                    </Grid>
                </Border>
                <ScrollViewer x:Name="logScroll" Grid.Row="1" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto">
                    <TextBlock x:Name="txtLog" FontFamily="Consolas,Courier New" FontSize="10.5"
                               Foreground="#E6EDF3" Padding="10,6" TextWrapping="Wrap"/>
                </ScrollViewer>
            </Grid>
        </Border>

        <!-- Status Bar -->
        <Border Grid.Row="3" Background="#111111">
            <Grid Margin="12,0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="200"/>
                    <ColumnDefinition Width="50"/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name="txtStatus" Text="Fetching latest codes..." FontSize="11" Foreground="#888" VerticalAlignment="Center"/>
                <ProgressBar x:Name="pbMain" Grid.Column="1" Height="5" Margin="0,0,12,0" Foreground="#0078D4" Background="#333" BorderThickness="0" Minimum="0" Maximum="100" Value="0" VerticalAlignment="Center"/>
                <TextBlock x:Name="txtPct" Grid.Column="2" Text="" FontSize="11" Foreground="#666" VerticalAlignment="Center" HorizontalAlignment="Right"/>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

$reader = [System.Xml.XmlNodeReader]::new($xaml)
$Window = [Windows.Markup.XamlReader]::Load($reader)

# Get controls
$lvFeatures      = $Window.FindName("lvFeatures")
$txtSearch       = $Window.FindName("txtSearch")
$cmbGroup        = $Window.FindName("cmbGroup")
$txtSummary      = $Window.FindName("txtSummary")
$txtLastUpdated  = $Window.FindName("txtLastUpdated")
$pbProgress      = $Window.FindName("pbProgress")
$btnSelectAll    = $Window.FindName("btnSelectAll")
$btnClearAll     = $Window.FindName("btnClearAll")
$btnSelectGroup  = $Window.FindName("btnSelectGroup")
$btnClearGroup   = $Window.FindName("btnClearGroup")
$btnEnable       = $Window.FindName("btnEnable")
$btnDisable      = $Window.FindName("btnDisable")
$chkWhatIf       = $Window.FindName("chkWhatIf")
$chkRestart      = $Window.FindName("chkRestart")
$btnDownload     = $Window.FindName("btnDownload")
$txtViveStatus   = $Window.FindName("txtViveStatus")
$txtLog          = $Window.FindName("txtLog")
$logScroll       = $Window.FindName("logScroll")
$txtStatus       = $Window.FindName("txtStatus")
$pbMain          = $Window.FindName("pbMain")
$txtPct          = $Window.FindName("txtPct")
$btnClearLog     = $Window.FindName("btnClearLog")
$txtBuildInfo    = $Window.FindName("txtBuildInfo")
$btnRefresh      = $Window.FindName("btnRefresh")
$loadingOverlay  = $Window.FindName("loadingOverlay")
$loadingText     = $Window.FindName("loadingText")

# Build info in title bar
try {
    $regKey  = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion"
    $build   = $regKey.CurrentBuild
    $ubr     = $regKey.UBR
    $dv      = $regKey.DisplayVersion
    $txtBuildInfo.Text = "  |  Windows 11 $dv  Build $build.$ubr"
} catch {}

# ViveTool status
function Update-ViveStatus {
    if ($script:ViveExe -and (Test-Path $script:ViveExe)) {
        $txtViveStatus.Text       = "Found: $(Split-Path -Leaf $script:ViveExe)"
        $txtViveStatus.Foreground = "#4CAF50"
    } else {
        $onPath = Get-Command vivetool.exe -ErrorAction SilentlyContinue
        if ($onPath) {
            $script:ViveExe       = $onPath.Source
            $txtViveStatus.Text   = "PATH: $($onPath.Source)"
            $txtViveStatus.Foreground = "#4CAF50"
        } else {
            $txtViveStatus.Text       = "Not found -- click Download"
            $txtViveStatus.Foreground = "#FF6B6B"
        }
    }
}
Update-ViveStatus

# ---- Shared state ----
$script:FeatureItems = [System.Collections.Generic.List[PSCustomObject]]::new()
$script:LogLines     = [System.Collections.Generic.List[string]]::new()

# ---- Log helper ----
function Write-GUILog([string]$Message) {
    $ts = Get-Date -Format "HH:mm:ss"
    $script:LogLines.Add("[$ts] $Message")
    if ($script:LogLines.Count -gt 600) { $script:LogLines.RemoveAt(0) }
    $txtLog.Text = $script:LogLines -join "`n"
    $logScroll.ScrollToBottom()
}

# ---- Summary update ----
function Update-Summary {
    $vis  = $lvFeatures.Items.Count
    $chk  = ($lvFeatures.Items | Where-Object { $_.IsSelected }).Count
    $tot  = $script:FeatureItems.Count
    $tchk = ($script:FeatureItems | Where-Object { $_.IsSelected }).Count
    $txtSummary.Text = "Visible $vis of $tot  |  Checked: $tchk"
    $pct = if ($tot -gt 0) { [int]($tchk * 100 / $tot) } else { 0 }
    $pbProgress.Value = $pct
}

# ---- Filter / refresh list ----
function Refresh-List {
    $search = $txtSearch.Text.Trim().ToLower()
    $gf     = $cmbGroup.SelectedItem
    $lvFeatures.Items.Clear()
    foreach ($item in $script:FeatureItems) {
        $mg = ($gf -eq "All Tracks" -or $item.Group -eq $gf)
        $ms = ($search -eq "" -or $item.Description.ToLower().Contains($search) -or $item.IDsDisplay.Contains($search) -or $item.BuildLabel.ToLower().Contains($search))
        if ($mg -and $ms) { $lvFeatures.Items.Add($item) | Out-Null }
    }
    Update-Summary
}

# ---- Rebuild group dropdown from current items ----
function Rebuild-Groups {
    $selected = $cmbGroup.SelectedItem
    $cmbGroup.Items.Clear()
    $null = $cmbGroup.Items.Add("All Tracks")
    $script:FeatureItems | Select-Object -ExpandProperty Group -Unique | Sort-Object | ForEach-Object { $null = $cmbGroup.Items.Add($_) }
    if ($selected -and $cmbGroup.Items.Contains($selected)) { $cmbGroup.SelectedItem = $selected }
    else { $cmbGroup.SelectedIndex = 0 }
}

# ---- Load catalog from Pureinfotech ----
function Load-LiveCatalog {
    $loadingOverlay.Visibility = "Visible"
    $txtStatus.Text = "Fetching latest codes from pureinfotech.com..."
    $btnRefresh.IsEnabled = $false
    [System.Windows.Forms.Application]::DoEvents()

    Write-GUILog "[INFO] Fetching live catalog from pureinfotech.com..."

    try {
        # Load the scraper
        . (Join-Path $ScriptDir "Get-LiveCatalog.ps1")
        $liveCatalog = Get-LiveCatalog

        if (-not $liveCatalog -or $liveCatalog.Count -eq 0) {
            throw "No entries returned from scraper."
        }

        $script:FeatureItems.Clear()

        # Clean group names
        foreach ($entry in $liveCatalog) {
            $groupClean = switch -Regex ($entry.Group) {
                "2026"   { "GA 2026" }
                "2025"   { "GA 2025" }
                "26H2"   { "26H2 Insider" }
                "25H2"   { "25H2 Insider" }
                "Canary" { "Canary / Feature Platforms" }
                default  { $entry.Group }
            }
            $idDisplay = ($entry.IDs | ForEach-Object { "$_" }) -join ", "
            $script:FeatureItems.Add([PSCustomObject]@{
                IsSelected  = $true
                Group       = $groupClean
                BuildLabel  = $entry.BuildLabel
                Description = $entry.Description
                IDsDisplay  = $idDisplay
                IDs         = $entry.IDs
            })
        }

        $allUniqueIDs = @($script:FeatureItems | ForEach-Object { $_.IDs } | Sort-Object -Unique)
        $ts = Get-Date -Format "yyyy-MM-dd HH:mm"
        $txtLastUpdated.Text = "Last fetched: $ts  |  $($script:FeatureItems.Count) entries, $($allUniqueIDs.Count) unique IDs"
        Write-GUILog "[SUCCESS] Loaded $($script:FeatureItems.Count) entries with $($allUniqueIDs.Count) unique feature IDs."

        Rebuild-Groups
        Refresh-List
        $txtStatus.Text = "Ready -- $($allUniqueIDs.Count) unique IDs loaded from pureinfotech.com"

    } catch {
        Write-GUILog "[ERROR] Failed to fetch live catalog: $_"
        Write-GUILog "[WARN]  Falling back to offline catalog (FeatureCatalog.ps1)..."
        $txtStatus.Text = "Live fetch failed -- using offline catalog"
        try {
            . (Join-Path $ScriptDir "FeatureCatalog.ps1")
            $script:FeatureItems.Clear()
            foreach ($key in $FeatureCatalog.Keys) {
                $g = switch -Regex ($key) {
                    "^GA_2026" { "GA 2026" }; "^GA_2025" { "GA 2025" }
                    "^26H2"    { "26H2 Insider" }; "^25H2" { "25H2 Insider" }
                    "^Canary"  { "Canary / Feature Platforms" }
                    default    { "Other" }
                }
                $lbl = $key -replace "^(GA_2026_|GA_2025_|26H2_Build[^_]+_|25H2_Build[^_]+_|Canary_Build[^_]+_)", "" -replace "_", " "
                $ids = @($FeatureCatalog[$key])
                $script:FeatureItems.Add([PSCustomObject]@{
                    IsSelected  = $true
                    Group       = $g
                    BuildLabel  = "(offline)"
                    Description = $lbl
                    IDsDisplay  = ($ids -join ", ")
                    IDs         = $ids
                })
            }
            $txtLastUpdated.Text = "Using offline catalog (no internet)"
            Rebuild-Groups; Refresh-List
        } catch {
            Write-GUILog "[ERROR] Offline catalog also failed: $_"
        }
    }

    $loadingOverlay.Visibility = "Collapsed"
    $btnRefresh.IsEnabled = $true
}

# ---- Enable/Disable runner ----
function Run-ViveCommand([string]$Mode, [bool]$WhatIf) {
    $selected = @($script:FeatureItems | Where-Object { $_.IsSelected })
    if ($selected.Count -eq 0) {
        [System.Windows.MessageBox]::Show("No features checked. Use the checkboxes to select features first.", "Nothing selected", "OK", "Warning") | Out-Null
        return
    }
    if (-not $script:ViveExe -or -not (Test-Path $script:ViveExe)) {
        [System.Windows.MessageBox]::Show("vivetool.exe not found. Use the Download button first.", "ViVeTool Missing", "OK", "Error") | Out-Null
        return
    }
    $ids   = @($selected | ForEach-Object { $_.IDs } | Sort-Object -Unique)
    $verb  = if ($Mode -eq "enable") { "/enable" } else { "/disable" }
    $label = if ($Mode -eq "enable") { "Enabling" } else { "Disabling" }
    Write-GUILog "=== $label $($ids.Count) IDs$(if ($WhatIf){' [DRY-RUN]'}) ==="
    $txtStatus.Text = "Running..."
    $btnEnable.IsEnabled = $false; $btnDisable.IsEnabled = $false
    $pbMain.Value = 0
    $ok = 0; $skip = 0; $err = 0; $i = 0
    foreach ($id in $ids) {
        $i++
        $pct = [int]($i * 100 / $ids.Count)
        $pbMain.Value = $pct; $txtPct.Text = "$pct%"
        $txtStatus.Text = "$label $i/$($ids.Count)  ID:$id"
        [System.Windows.Forms.Application]::DoEvents()
        if ($WhatIf) { Write-GUILog "[WHATIF] $verb /id:$id"; continue }
        try {
            $out     = & $script:ViveExe $verb /id:$id 2>&1
            $outStr  = ($out -join " ").Trim()
            if ($LASTEXITCODE -eq 0) {
                Write-GUILog "[SUCCESS] ID:$id  $outStr"; $ok++
            } elseif ($outStr -match "not found|unknown|unsupported|no feature") {
                Write-GUILog "[SKIP]    ID:$id  Unsupported"; $skip++
            } else {
                Write-GUILog "[WARN]    ID:$id  exit=$LASTEXITCODE  $outStr"; $err++
            }
        } catch { Write-GUILog "[ERROR]   ID:$id  $_"; $err++ }
        [System.Windows.Forms.Application]::DoEvents()
    }
    $pbMain.Value = 100
    Write-GUILog "=== Done: Success=$ok  Skipped=$skip  Errors=$err ==="
    $txtStatus.Text = "Done.  OK:$ok  Skip:$skip  Err:$err"
    $btnEnable.IsEnabled = $true; $btnDisable.IsEnabled = $true
    if ($chkRestart.IsChecked -and -not $WhatIf -and $Mode -eq "enable") {
        $ans = [System.Windows.MessageBox]::Show("Restart Windows Explorer now to apply shell changes?", "Restart Explorer?", "YesNo", "Question")
        if ($ans -eq "Yes") {
            Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
            Start-Sleep 2; Start-Process explorer.exe
            Write-GUILog "[INFO] Explorer restarted."
        }
    }
}

# ---- Wire events ----
$txtSearch.Add_TextChanged({ Refresh-List })
$cmbGroup.Add_SelectionChanged({ Refresh-List })

$btnSelectAll.Add_Click({
    $lvFeatures.Items | ForEach-Object { $_.IsSelected = $true }
    $lvFeatures.Items.Refresh(); Update-Summary
})
$btnClearAll.Add_Click({
    $lvFeatures.Items | ForEach-Object { $_.IsSelected = $false }
    $lvFeatures.Items.Refresh(); Update-Summary
})
$btnSelectGroup.Add_Click({
    $g = $cmbGroup.SelectedItem
    $tgt = if ($g -eq "All Tracks") { $lvFeatures.Items } else { $script:FeatureItems | Where-Object { $_.Group -eq $g } }
    $tgt | ForEach-Object { $_.IsSelected = $true }
    $lvFeatures.Items.Refresh(); Update-Summary
})
$btnClearGroup.Add_Click({
    $g = $cmbGroup.SelectedItem
    $tgt = if ($g -eq "All Tracks") { $lvFeatures.Items } else { $script:FeatureItems | Where-Object { $_.Group -eq $g } }
    $tgt | ForEach-Object { $_.IsSelected = $false }
    $lvFeatures.Items.Refresh(); Update-Summary
})
$btnEnable.Add_Click({ Run-ViveCommand -Mode "enable" -WhatIf ($chkWhatIf.IsChecked -eq $true) })
$btnDisable.Add_Click({
    $ans = [System.Windows.MessageBox]::Show("This will DISABLE all checked IDs.`nAre you sure?", "Confirm Rollback", "YesNo", "Warning")
    if ($ans -eq "Yes") { Run-ViveCommand -Mode "disable" -WhatIf ($chkWhatIf.IsChecked -eq $true) }
})
$btnRefresh.Add_Click({ Load-LiveCatalog })
$btnDownload.Add_Click({
    $txtStatus.Text = "Downloading ViVeTool..."; $btnDownload.IsEnabled = $false
    try {
        . (Join-Path $ScriptDir "Get-ViveTool.ps1") -InstallDir $ScriptDir
        $script:ViveExe = $ViveToolPath
        Update-ViveStatus
        Write-GUILog "[SUCCESS] ViVeTool downloaded: $ViveToolPath"
    } catch {
        Write-GUILog "[ERROR] Download failed: $_"
        [System.Windows.MessageBox]::Show("Download failed:`n$_", "Error", "OK", "Error") | Out-Null
    }
    $btnDownload.IsEnabled = $true; $txtStatus.Text = "Ready"
})
$btnClearLog.Add_Click({ $script:LogLines.Clear(); $txtLog.Text = "" })

# ---- Launch: fetch live data on startup ----
$Window.Add_Loaded({ Load-LiveCatalog })

Write-Host "Launching ViVeTool GUI..."
$Window.ShowDialog() | Out-Null
