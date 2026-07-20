param(
    [int]$Seconds = 20,
    [string]$ProcessName = 'WinPicker'
)

$process = Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
$logicalProcessors = [Environment]::ProcessorCount
$startCpu = $process.TotalProcessorTime.TotalSeconds
$start = Get-Date
Start-Sleep -Seconds $Seconds
$process.Refresh()
$elapsed = ((Get-Date) - $start).TotalSeconds
$cpuSeconds = $process.TotalProcessorTime.TotalSeconds - $startCpu
$oneCorePercent = if ($elapsed -gt 0) { 100.0 * $cpuSeconds / $elapsed } else { 0 }
$allCpuPercent = if ($logicalProcessors -gt 0) { $oneCorePercent / $logicalProcessors } else { 0 }

[pscustomobject]@{
    ProcessId       = $process.Id
    MeasurementSec  = [math]::Round($elapsed, 2)
    CpuTimeDeltaSec = [math]::Round($cpuSeconds, 3)
    OneCorePercent  = [math]::Round($oneCorePercent, 2)
    AllCpuPercent   = [math]::Round($allCpuPercent, 2)
    Threads         = $process.Threads.Count
    Handles         = $process.HandleCount
    PrivateMemoryMB = [math]::Round($process.PrivateMemorySize64 / 1MB, 1)
}
