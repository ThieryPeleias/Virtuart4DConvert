$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'Virtuart4DConvert.csproj'
$expected = if ($args.Count -eq 1) { $args[0] } else { $null }
function FileHash([string]$path) { $sha = [Security.Cryptography.SHA256]::Create(); $stream = [IO.File]::OpenRead($path); try { return (($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) -join '') } finally { $stream.Dispose(); $sha.Dispose() } }
$beforeHash = if (!$expected) { FileHash $project } else { $null }
if ($args.Count -gt 1 -or ($expected -and $expected -notmatch '^\d+\.\d+\.\d+$')) { throw 'Usage: build.bat [expected-mpxj-version]' }
function Run([string[]]$a) { & dotnet @a; if ($LASTEXITCODE -ne 0) { throw "dotnet failed" } }
function Gate($dir, $version) {
  $exe = Join-Path $dir 'Virtuart4DConvert.exe'
  if (!(Test-Path $exe -PathType Leaf)) { throw 'missing executable' }
  if (!(Test-Path (Join-Path $dir 'ikvm/win-x64') -PathType Container)) { throw 'missing ikvm/win-x64' }
  foreach ($n in 'mpxj-lgpl2.1.txt','apache-poi-apache2.txt','jsoup-mit.txt','rtfparserkit-apache2.txt','sqlite-jdbc-apache2.txt') { if (!(Test-Path (Join-Path $dir "licenses/$n") -PathType Leaf)) { throw "missing license $n" } }
  if (@(Get-ChildItem $dir -Recurse -File -Filter '*.pdb').Count) { throw 'PDB present' }
  $v = (& $exe --version).Trim(); if ($LASTEXITCODE -ne 0 -or $v -ne '0.1.1') { throw 'invalid version' }
  $raw = & $exe --info; if ($LASTEXITCODE -ne 0) { throw 'info failed' }; $i = (($raw) -join "`n") | ConvertFrom-Json
  $props = @($i.psobject.Properties); if ($props.Count -ne 2 -or $props.Name -notcontains 'converterVersion' -or $props.Name -notcontains 'mpxjVersion' -or $i.converterVersion -isnot [string] -or $i.mpxjVersion -isnot [string] -or $i.converterVersion -ne $v -or $i.mpxjVersion -ne $version) { throw 'identity mismatch' }
}
$declared = (Select-String -LiteralPath $project -Pattern 'PackageReference Include="MPXJ\.Net" Version="([^"]+)"').Matches.Groups[1].Value
if (!$declared -or ($expected -and $declared -ne $expected)) { throw 'MPXJ declaration mismatch' }
if ($expected) { Run @('restore', $project, '--runtime', 'win-x64') } else { Run @('restore', $project, '--runtime', 'win-x64'); & dotnet list $project package --outdated --format json; if ($LASTEXITCODE -ne 0) { throw 'outdated query failed' } }
$assets = Get-Content (Join-Path $root 'obj/project.assets.json') -Raw | ConvertFrom-Json
$target = $assets.targets.'net8.0/win-x64'; if (!$target) { throw 'target assets missing' }
$package = $target.psobject.Properties | Where-Object Name -eq "MPXJ.Net/$declared"; if (@($package).Count -ne 1) { throw 'target MPXJ assets mismatch' }
if (!$package.Value.compile.'lib/net6.0/MPXJ.Net.dll' -or !$package.Value.runtime.'lib/net6.0/MPXJ.Net.dll') { throw 'MPXJ compile/runtime asset missing' }
$id = [guid]::NewGuid().ToString('N'); $stage = Join-Path $env:TEMP "v4d-stage-$id"; $extract = Join-Path $env:TEMP "v4d-extract-$id"; $attemptZip = Join-Path $env:TEMP "v4d-$id.zip"
try {
  New-Item $stage -ItemType Directory | Out-Null
  Run @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', '-p:DebugType=none', '-p:DebugSymbols=false', '-p:CopyOutputSymbolsToPublishDirectory=false', '-p:AllowedReferenceRelatedFileExtensions=.xml', '-o', $stage)
  New-Item (Join-Path $stage 'licenses') -ItemType Directory | Out-Null; Copy-Item (Join-Path $root 'licenses/*') (Join-Path $stage 'licenses') -Force
  Gate $stage $declared; Compress-Archive (Join-Path $stage '*') $attemptZip -Force
  New-Item $extract -ItemType Directory | Out-Null; Expand-Archive $attemptZip $extract -Force; Gate $extract $declared
  if ($beforeHash -and (FileHash $project) -ne $beforeHash) { throw 'project changed during developer build' }
  New-Item (Join-Path $root 'zip') -ItemType Directory -Force | Out-Null; Copy-Item $attemptZip (Join-Path $root 'zip/Virtuart4DConvert.zip') -Force
} finally { foreach ($p in $stage, $extract) { if (Test-Path $p) { Remove-Item $p -Recurse -Force } }; if (Test-Path $attemptZip) { Remove-Item $attemptZip -Force } }
