param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [Parameter(Mandatory = $false)]
    [string]$Version = "1.0.0.0"
)

$ErrorActionPreference = 'Stop'

$publishPath = (Resolve-Path $PublishDir).Path
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$outputPath = (Resolve-Path $OutputDir).Path

$exePath = Join-Path $publishPath 'Fontloom.Desktop.exe'
if (-not (Test-Path $exePath)) {
    throw "Expected executable not found: $exePath"
}

$zipPath = Join-Path $outputPath 'fontloom-win-x64.zip'
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $zipPath -Force

$stagingPath = Join-Path $outputPath 'msix-staging'
if (Test-Path $stagingPath) {
    Remove-Item $stagingPath -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingPath | Out-Null
Copy-Item -Path (Join-Path $publishPath '*') -Destination $stagingPath -Recurse -Force

$assetsPath = Join-Path $stagingPath 'Assets'
New-Item -ItemType Directory -Path $assetsPath -Force | Out-Null

$pixelPngBase64 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+X2ioAAAAASUVORK5CYII='
$pixelPngBytes = [Convert]::FromBase64String($pixelPngBase64)
[IO.File]::WriteAllBytes((Join-Path $assetsPath 'Square150x150Logo.png'), $pixelPngBytes)
[IO.File]::WriteAllBytes((Join-Path $assetsPath 'Square44x44Logo.png'), $pixelPngBytes)

if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "MSIX Version must be in major.minor.build.revision format. Got: $Version"
}

$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">
  <Identity Name="rwrife.fontloom" Publisher="CN=Fontloom" Version="$Version" />
  <Properties>
    <DisplayName>fontloom</DisplayName>
    <PublisherDisplayName>rwrife</PublisherDisplayName>
    <Logo>Assets/Square150x150Logo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Applications>
    <Application Id="Fontloom" Executable="Fontloom.Desktop.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="fontloom"
        Description="Cross-platform desktop font manager"
        BackgroundColor="transparent"
        Square150x150Logo="Assets/Square150x150Logo.png"
        Square44x44Logo="Assets/Square44x44Logo.png" />
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@

$manifestPath = Join-Path $stagingPath 'AppxManifest.xml'
Set-Content -Path $manifestPath -Value $manifest -Encoding utf8

$makeAppxPath = $null
$makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue
if ($makeAppx) {
    $makeAppxPath = $makeAppx.Source
}

if (-not $makeAppxPath) {
    $candidates = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending
    if ($candidates.Count -gt 0) {
        $makeAppxPath = $candidates[0].FullName
    }
}

if (-not $makeAppxPath) {
    throw 'makeappx.exe not found on PATH or Windows SDK location.'
}

$msixPath = Join-Path $outputPath 'fontloom-win-x64.msix'
if (Test-Path $msixPath) {
    Remove-Item $msixPath -Force
}

& $makeAppxPath pack /d $stagingPath /p $msixPath /o | Out-Host

Write-Host "Created $zipPath"
Write-Host "Created $msixPath"
