param(
    [string]$GameDirectory = "$env:APPDATA\Vintagestory",
    [switch]$DisableCombatOverhaul,
    [switch]$DisableCrossbows,
    [switch]$DisableArmory,
    [switch]$DisableRecyclingTools,
    [switch]$DisableToolsmith
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$out = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $out 'build-log.txt'
$downloads = Join-Path $env:USERPROFILE 'Downloads'
$zip = Join-Path $downloads 'Steelworks_1.0.4_BUILD_RESULTS.zip'
$mod = Join-Path $downloads 'Steelworks_1.0.4_VS1.22.7.zip'
$files = @($log, (Join-Path $PSScriptRoot 'NATIVE_ASSET_REPORT.json'), (Join-Path $PSScriptRoot 'EQUIPMENT_AUDIT_0.9.6.json'))
$files += @(Get-ChildItem $PSScriptRoot -File -Filter '*AUDIT*' | ForEach-Object { $_.FullName } | Where-Object { $_ -notin $files })
$exit = 1
function Run-Dotnet([string[]]$Arguments) {
    $old = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & dotnet @Arguments 2>&1 | ForEach-Object { $_.ToString() } | Tee-Object -FilePath $log -Append | Out-Host
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $old }
    if ($code -ne 0) { throw "dotnet returned $code" }
}
try {
    "Steelworks 1.0.4 development build" | Set-Content $log
    if(Test-Path $mod){Remove-Item $mod -Force}
    Run-Dotnet @('--info')
    Run-Dotnet @('build','tests/Steelworks.Tests.csproj','-c','Release')
    Run-Dotnet @('run','--project','tests/Steelworks.Tests.csproj','-c','Release','--no-build')
    foreach($dll in @('VintagestoryAPI.dll','Vintagestory.dll','Mods\VSSurvivalMod.dll','Mods\VSEssentials.dll','Lib\Newtonsoft.Json.dll','Lib\0Harmony.dll')) {
        if(!(Test-Path (Join-Path $GameDirectory $dll))) {throw "Missing game assembly: $dll in $GameDirectory"}
    }
    Run-Dotnet @('build','native/Steelworks.csproj','-c','Release',"-p:GameDirectory=$GameDirectory")
    $stage = Join-Path $out 'mod-stage'
    if(Test-Path $stage){Remove-Item $stage -Recurse -Force}
    New-Item -ItemType Directory -Force $stage | Out-Null
    Copy-Item 'content\*' $stage -Recurse -Force
    # Build-time integration selection: never modify source assets or installed saves.
    $patchDir = Join-Path $stage 'assets\steelworks\patches'
    $disabled = @()
    if ($DisableCombatOverhaul) { $disabled += 'combatoverhaulfork'; $disabled += 'armoryfork' }
    if ($DisableCrossbows) { $disabled += 'crossbowsfork' }
    if ($DisableArmory) { $disabled += 'armoryfork' }
    if ($DisableRecyclingTools) { $disabled += 'recyclingtools' }
    if ($DisableToolsmith) { $disabled += 'toolsmith' }
    if ($disabled.Count -gt 0) {
        foreach ($patch in Get-ChildItem $patchDir -Filter '*.json') {
            $ops = @(Get-Content $patch.FullName -Raw | ConvertFrom-Json)
            $kept = @($ops | Where-Object {
                $deps = @($_.dependsOn | ForEach-Object { $_.modid })
                @($deps | Where-Object { $disabled -contains $_ }).Count -eq 0
            })
            if ($kept.Count -eq 0) { Remove-Item $patch.FullName -Force }
            elseif ($kept.Count -ne $ops.Count) { ConvertTo-Json -InputObject $kept -Depth 100 | Set-Content $patch.FullName -Encoding UTF8 }
            "Integration profile: $($patch.Name) $($kept.Count)/$($ops.Count) operations retained" | Add-Content $log
        }
    }
    Copy-Item 'native\bin\Release\net10.0\Steelworks.dll' $stage -Force
    # Explicit ZIP entry names prevent Windows backslashes from breaking Linux asset discovery.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stageFull = [IO.Path]::GetFullPath($stage).TrimEnd([char[]]@('\','/'))
    $archive = [IO.Compression.ZipFile]::Open($mod,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $stageFull -Recurse -File) {
            $entry = $file.FullName.Substring($stageFull.Length+1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$entry,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose() }
    $check = [IO.Compression.ZipFile]::OpenRead($mod)
    try {
        if (@($check.Entries | Where-Object {$_.FullName.Contains('\')}).Count -ne 0) { throw 'ZIP contains invalid backslash asset paths' }
        if (!$check.GetEntry('modinfo.json') -or !$check.GetEntry('Steelworks.dll') -or !($check.Entries | Where-Object {$_.FullName.StartsWith('assets/steelworks/')})) { throw 'ZIP package is missing required root files or assets' }
    } finally { $check.Dispose() }
    $files += (Join-Path $stage 'Steelworks.dll')
    $exit = 0
} catch { $_ | Out-String | Tee-Object -FilePath $log -Append | Out-Host }
finally {
    "Build exit status: $exit" | Add-Content $log
    Compress-Archive -Path $files -DestinationPath $zip -Force
    Write-Host "UPLOAD THIS FILE: $zip" -ForegroundColor Green
    if($exit -eq 0){Write-Host "BUILT MOD: $mod" -ForegroundColor Yellow}
    explorer.exe "/select,$zip"
}
if($exit -ne 0){exit 1}
