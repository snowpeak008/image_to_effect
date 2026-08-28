param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    function Assert-Ignored([string] $Path) {
        & git check-ignore -q -- $Path
        if ($LASTEXITCODE -ne 0) { throw "Expected ignored path was not ignored: $Path" }
        Write-Host "ignored: $Path"
    }

    function Assert-NotIgnored([string] $Path) {
        & git check-ignore -q -- $Path
        if ($LASTEXITCODE -eq 0) {
            $rule = (& git check-ignore -v -- $Path) -join [Environment]::NewLine
            throw "Formal source path is ignored: $Path`n$rule"
        }
        if ($LASTEXITCODE -ne 1) { throw "git check-ignore failed for $Path with exit code $LASTEXITCODE" }
        Write-Host "tracked-eligible: $Path"
    }

    # Formal Unity source/assets and their metadata are protected from every
    # ignore rule, especially the package's Editor/Build directory.
    @(
        'project/Packages/com.vfxcomposer.unity/Editor/Build/VfxCompiler.cs',
        'project/Packages/com.vfxcomposer.unity/Editor/Build/VfxCompiler.cs.meta',
        'project/Assets/VFX/Templates/3D/Prefabs/PFT_3D_FireCore.prefab',
        'project/Assets/VFX/Templates/3D/Prefabs/PFT_3D_FireCore.prefab.meta',
        'project/ProjectSettings/ProjectVersion.txt'
    ) | ForEach-Object { Assert-NotIgnored $_ }

    # Unity output must still be ignored, but only at the nested project root.
    @(
        'project/Library/audit-placeholder', 'project/Temp/audit-placeholder',
        'project/Obj/audit-placeholder', 'project/Build/audit-placeholder',
        'project/Builds/audit-placeholder', 'project/Logs/audit-placeholder',
        'project/UserSettings/audit-placeholder', 'project/MemoryCaptures/audit-placeholder',
        'project/Recordings/audit-placeholder', 'test-results/audit-placeholder',
        'spike/audit-placeholder'
    ) | ForEach-Object { Assert-Ignored $_ }

    Write-Host 'Git ignore audit passed.'
}
finally { Pop-Location }
