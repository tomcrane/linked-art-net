<#
.SYNOPSIS
  Copies the newest linkedart.net data snapshot from the server to this machine, so a backup also exists off the box.

.DESCRIPTION
  The server keeps nightly snapshots in /srv/backups/linkedart (deploy/linkedart/backup.sh). This pulls latest.tar.gz
  over scp with your normal SSH key and stores it under a dated name. Run it by hand now and then, or schedule it with
  Task Scheduler (weekly is plenty: the data is the trainer queue and the capture file, a few kilobytes).

.EXAMPLE
  .\deploy\linkedart\pull-backup.ps1
  .\deploy\linkedart\pull-backup.ps1 -Destination D:\Backups\linkedart -Keep 12
#>
param(
    [string]$Server = "root@linkedart.net",
    [string]$Destination = (Join-Path $env:USERPROFILE "Backups\linkedart"),
    [int]$Keep = 26
)

New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmm")
$target = Join-Path $Destination "data-$stamp.tar.gz"

scp "${Server}:/srv/backups/linkedart/latest.tar.gz" $target
if ($LASTEXITCODE -ne 0) { Write-Error "scp failed; is the nightly backup installed on the server? (docs/09-hosting.md section 8)"; exit 1 }

$size = (Get-Item $target).Length
Write-Output ("Pulled {0} ({1:N0} bytes)" -f $target, $size)

# Keep the newest $Keep copies.
Get-ChildItem $Destination -Filter "data-*.tar.gz" | Sort-Object Name -Descending | Select-Object -Skip $Keep | Remove-Item -Force
Write-Output ("{0} copies kept in {1}" -f (Get-ChildItem $Destination -Filter "data-*.tar.gz").Count, $Destination)
