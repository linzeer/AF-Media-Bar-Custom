# upload_to_github.ps1  (robust v2: handles empty dirs / 404 as "absent")
param(
    [Parameter(Mandatory=$true)][string]$Token,
    [string]$Repo = "linzeer/AF-Media-Bar-Custom",
    [string]$LocalRoot = "F:\deepseekharness\work\afm\AF-Media-Bar-1.1.1\AF-Media-Bar-Custom",
    [string]$ExeDir = "F:\deepseekharness\work\afm\AF-Media-Bar-1.1.1\AF-Media-Bar-1.1.1\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish",
    [string]$ReleaseTag = "v1.2.2"
)
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$headers = @{ "Authorization"="Bearer $Token"; "User-Agent"="AFMediaBarCustom/1.2.2"; "Accept"="application/vnd.github+json" }
$Base = "https://api.github.com/repos/$Repo"
function Log($m){ Write-Host "[$(Get-Date -Format HH:mm:ss)] $m" }
function ApiGet($url){
    try { return Invoke-RestMethod -Uri $url -Headers $headers -TimeoutSec 60 }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -eq 404) { return $null }   # absent == null (empty dir / not exist)
        throw
    }
}
function ApiJson($method,$url,$body){
    Invoke-RestMethod -Method $method -Uri $url -Headers $headers -Body ($body | ConvertTo-Json -Depth 8) -ContentType "application/json" -TimeoutSec 60
}

# identity & permission
$user = ApiGet "https://api.github.com/user"
Log "Account: $($user.login) ($($user.name))"
$repoInfo = ApiGet "$Base"
Log "Repo: $($repoInfo.full_name) push=$($repoInfo.permissions.push)"
if (-not $repoInfo.permissions.push){ throw "No push permission." }
$sep = [System.IO.Path]::DirectorySeparatorChar

# list top-level entries
$root = ApiGet "$Base/contents/"
$nameSet = @{}
foreach($e in $root){ $nameSet[$e.name]=$e }

# --- archive original structure -> archive-original/ (robust) ---
$archiveDirs = @("src","tests","prototypes","docs",".github")
$archiveFiles = @("global.json","AFMediaBar.slnx","CHANGELOG.en-US.md","CHANGELOG.md","CONTRIBUTING.md","README.en-US.md","README.md","RELEASE_NOTES.md","SECURITY.md",".gitignore")

function PushToArchive($rel){
    $src = ApiGet "$Base/contents/$rel"
    if ($null -eq $src){ return }
    $tar = "archive-original/$rel"
    $b64 = $src.content -replace "\s",""
    $body = @{ message="Archive: $rel"; content=$b64; branch="main" }
    try { ApiJson "PUT" "$Base/contents/$tar" $body; Log "  archive $tar" }
    catch { Log "  archive $tar FAIL: $($_.Exception.Message)" }
}
function DeleteEntry($rel){
    $info = ApiGet "$Base/contents/$rel"
    if ($null -eq $info){ return }
    $d = @{ message="Remove: $rel"; sha=$info.sha; branch="main" }
    try { ApiJson "DELETE" "$Base/contents/$rel" $d; Log "  del $rel" } catch { Log "  del $rel FAIL: $($_.Exception.Message)" }
}
function WalkDelete($rel){
    $list = ApiGet "$Base/contents/$rel"
    if ($null -eq $list){ return }
    foreach($child in $list){
        $cpath = "$rel/$($child.name)"
        if ($child.type -eq "dir"){ WalkDelete $cpath } else { DeleteEntry $cpath }
    }
    DeleteEntry $rel
}
function ArchiveAndRemove($rel){
    $list = ApiGet "$Base/contents/$rel"
    if ($null -eq $list){
        # dir already empty after children removed, or not exist -> just try remove entry
        DeleteEntry $rel
        return
    }
    foreach($child in $list){
        $cpath = "$rel/$($child.name)"
        if ($child.type -eq "dir"){ ArchiveAndRemove $cpath }
        else { PushToArchive $cpath }
    }
    DeleteEntry $rel
}
foreach($d in $archiveDirs){ if ($nameSet.ContainsKey($d)){ Log "Archive dir $d"; ArchiveAndRemove $d } }
foreach($f in $archiveFiles){ if ($nameSet.ContainsKey($f)){ Log "Archive file $f"; PushToArchive $f; DeleteEntry $f } }

# --- upload single-project source ---
function UploadLocalFile($full,$rpath){
    $b64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($full))
    $body = @{ message="Add/update: $rpath"; content=$b64; branch="main" }
    $ex = ApiGet "$Base/contents/$rpath"
    if ($null -ne $ex){ $body["sha"]=$ex.sha }
    ApiJson "PUT" "$Base/contents/$rpath" $body
}
Log "Upload single-project source..."
$files = Get-ChildItem $LocalRoot -Recurse -File
$c=0
foreach($f in $files){
    $rel = $f.FullName.Substring($LocalRoot.Length+1) -replace [regex]::Escape($sep),"/"
    if ($rel -match "^(bin|obj|dist|artifacts)/"){ continue }
    UploadLocalFile $f.FullName $rel
    $c++
    if ($c % 10 -eq 0){ Log "  uploaded $c files..." }
}
Log "Source uploaded: $c files."

# --- release ---
Log "Create Release $ReleaseTag ..."
$releaseBody = @{ tag_name=$ReleaseTag; name="AF Media Bar $ReleaseTag (custom)"; body="Custom fork: FAN/temp via LibreHardwareMonitor, battery, font size, no-media-keep-metrics. See CHANGES.md. Original: Fervent-Tempo/AF-Media-Bar (MIT)."; draft=$false; prerelease=$false }
$release=$null
try { $release = ApiJson "POST" "$Base/releases" $releaseBody }
catch { $all=ApiGet "$Base/releases"; $release = $all | Where-Object { $_.tag_name -eq $ReleaseTag } | Select-Object -First 1; if(-not $release){ throw "release fail" } }
Log "Release id=$($release.id)"

# --- upload exe assets ---
$assets = @("AFMediaBar_v1.0.exe","AFMediaBar_v1.1.exe","AFMediaBar_v1.2.1.exe","AFMediaBar_v1.2.2.exe")
foreach($a in $assets){
    $local = Join-Path $ExeDir $a
    if(-not (Test-Path $local)){ Log "skip $a"; continue }
    Log "Upload asset $a ($([math]::Round((Get-Item $local).Length/1MB,1)) MB)"
    $url = "$Base/releases/$($release.id)/assets?name=$a"
    $gh = $headers.Clone(); $gh["Content-Type"]="application/octet-stream"
    try { Invoke-WebRequest -Method Post -Uri $url -Headers $gh -InFile $local -UseBasicParsing -TimeoutSec 1200 | Out-Null; Log "  OK $a" }
    catch { Log "  asset $a FAIL: $($_.Exception.Message)" }
}
Log "ALL DONE. https://github.com/$Repo  Release $($release.html_url)"
