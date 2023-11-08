Write-Host "Removing DevHome Azure Extension..."
Get-Appxpackage *DevHomeAzureExtension* | Remove-AppxPackage

Write-Host "Terminating Windows Widget Host..."
Get-Process Widgets -ErrorAction Ignore | Stop-Process

Write-Host "Terminating DevHome..."
Get-Process DevHome -ErrorAction Ignore | Stop-Process

Write-Host "Terminating WidgetService..."
Get-Process WidgetService -ErrorAction Ignore | Stop-Process

$widgetSessionsPath = Join-Path -Path $env:localappdata -ChildPath "Packages\MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy\LocalState\WidgetSessions"
Write-Host "Removing WidgetSessions folder: $widgetSessionsPath"
Remove-Item -Path $widgetSessionsPath -Force -Recurse -ErrorAction SilentlyContinue

Write-Host "Cleaned all Widget State. Redeploy the Azure Extension and relaunch DevHome."