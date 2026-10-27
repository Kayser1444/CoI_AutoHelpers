# PRE_4_3_COMPAT: retire the dual build when pre-4.3 support ends.
function Build-CoIVersionSwitchingPackageInputs {
    param([string]$ModId,[string]$Configuration,[string]$LegacyManagedPath,[string]$CurrentManagedPath)
    $helpersRoot=Split-Path -Parent $PSScriptRoot
    # Consuming projects reach AH through external/CoI_AutoHelpers junctions.
    # Resolve that junction before looking for sibling projects/reference snapshots.
    $helpersItem=Get-Item -LiteralPath $helpersRoot -Force
    if($helpersItem.LinkType -and $helpersItem.Target){
        $target=@($helpersItem.Target)[0]
        $helpersRoot=if([IO.Path]::IsPathRooted($target)){$target}else{Join-Path (Split-Path -Parent $helpersRoot) $target}
    }
    if(-not $LegacyManagedPath){$LegacyManagedPath=$env:COI_LEGACY_MANAGED_PATH}
    if(-not $CurrentManagedPath){$CurrentManagedPath=$env:CAPTAIN_INDUSTRY_MANAGED_PATH}
    if(-not $CurrentManagedPath){$CurrentManagedPath='C:\Program Files (x86)\Steam\steamapps\common\Captain of Industry\Captain of Industry_Data\Managed'}
    if(-not $LegacyManagedPath){
        $snapshots=Join-Path (Split-Path -Parent $helpersRoot) 'AutoTerrainDesignations\.scratch\compatibility'
        $reference=Get-ChildItem -LiteralPath $snapshots -Directory -Filter 'reference-0.8.7d-*' -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending | Select-Object -First 1
        if($reference){$LegacyManagedPath=Join-Path $reference.FullName 'Managed'}
    }
    if(-not $LegacyManagedPath){throw 'Packaging requires -LegacyManagedPath (or COI_LEGACY_MANAGED_PATH) for complete 0.8.7d references.'}
    & (Join-Path $helpersRoot 'tools\Build-VersionSwitchingMods.ps1') -LegacyManagedPath $LegacyManagedPath -CurrentManagedPath $CurrentManagedPath -Configuration $Configuration -ModIds $ModId -Deploy
    if($LASTEXITCODE -ne 0){throw "Dual implementation build failed for $ModId."}
}
