param(
    [Parameter(Mandatory=$true)][string]$LegacyManagedPath,
    [Parameter(Mandatory=$true)][string]$CurrentManagedPath,
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [switch]$Deploy,
    [ValidateSet('AutoTerrainDesignations','AutoForestryDesignations','DesignerToolkit','KaysersPreIndustrialEra')]
    [string[]]$ModIds=@('AutoTerrainDesignations','AutoForestryDesignations','DesignerToolkit','KaysersPreIndustrialEra')
)
$ErrorActionPreference='Stop'
$helperRoot=Split-Path -Parent $PSScriptRoot
$modsRoot=Split-Path -Parent $helperRoot
$legacyVersion=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $LegacyManagedPath 'Mafi.dll')).Version
$currentVersion=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $CurrentManagedPath 'Mafi.dll')).Version
if($legacyVersion -ne [Version]'0.8.7.0' -or $currentVersion -ne [Version]'0.8.8.0'){
    throw 'Expected complete 0.8.7 and 0.8.8 Managed reference directories.'
}
$staging=Join-Path $helperRoot ('artifacts\version-switching\'+$Configuration)
foreach($project in $ModIds){
    $repo=Join-Path $modsRoot $project
    $output=Join-Path $staging $project
    $null=New-Item -ItemType Directory -Path $output -Force
    foreach($branch in @('pre-update-4.3','update-4.3')){
        $managed=if($branch -eq 'pre-update-4.3'){$LegacyManagedPath}else{$CurrentManagedPath}
        $destination=Join-Path $output ('implementations\'+$branch)
        $null=New-Item -ItemType Directory -Path $destination -Force
        & dotnet build (Join-Path $repo "$project.csproj") -c $Configuration "-p:CaptainIndustryManagedPath=$managed" '-p:SkipModDeployment=true'
        if($LASTEXITCODE -ne 0){throw "Build failed: $project/$branch"}
        Copy-Item -LiteralPath (Join-Path $repo "bin\$Configuration\net48\$project.dll") -Destination $destination
    }
    & dotnet build (Join-Path $PSScriptRoot 'VersionSelector\VersionSelector.csproj') -c $Configuration "-p:ModId=$project" "-p:CaptainIndustryManagedPath=$LegacyManagedPath"
    if($LASTEXITCODE -ne 0){throw "Selector build failed: $project"}
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "VersionSelector\bin\$Configuration\net48\$project.Loader.dll") -Destination $output
    Copy-Item -LiteralPath (Join-Path $repo "bin\$Configuration\net48\0Harmony.dll") -Destination $output
    $manifest=Get-Content -LiteralPath (Join-Path $repo 'manifest.json') -Raw|ConvertFrom-Json
    $manifest.primary_dlls=@('0Harmony.dll',"$project.Loader.dll")
    $manifest|ConvertTo-Json -Depth 20|Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding utf8
}
# Finish every build before altering an installed manifest.
if($Deploy){
    foreach($project in $ModIds){
        $repo=Join-Path $modsRoot $project
        $source=Join-Path $staging $project
        Copy-Item -LiteralPath (Join-Path $source 'implementations') -Destination $repo -Recurse -Force
        Copy-Item -LiteralPath (Join-Path $source "$project.Loader.dll") -Destination $repo -Force
        Copy-Item -LiteralPath (Join-Path $source '0Harmony.dll') -Destination $repo -Force
        Copy-Item -LiteralPath (Join-Path $source 'manifest.json') -Destination $repo -Force
    }
}
Write-Output "Version-switching binary overlays: $staging"
