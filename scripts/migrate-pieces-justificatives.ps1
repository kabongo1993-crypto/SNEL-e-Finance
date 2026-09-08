# Migration contrôlée : pièces justificatives DPM
# Ancien root (relatif bin) → nouveau root absolu
# Les StorageKey (CheminRelatif) SQL restent inchangés.

param(
    [string]$OldRoot = "D:\InstantFLOW\Instant_Flow\src\BudgetWeb.API\bin\Debug\net8.0\App_Data\pieces-justificatives",
    [string]$NewRoot = "D:\BudgetWebData\pieces-justificatives",
    [string]$SqlServer = "localhost\HEROS_SQL19",
    [string]$Database = "BD_SNEL"
)

$ErrorActionPreference = "Stop"

function Get-Sha256Hex([string]$path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $fs = [System.IO.File]::OpenRead($path)
        try {
            ($sha.ComputeHash($fs) | ForEach-Object { $_.ToString("x2") }) -join ""
        } finally { $fs.Dispose() }
    } finally { $sha.Dispose() }
}

Write-Host "=== Migration pièces justificatives ==="
Write-Host "OldRoot: $OldRoot"
Write-Host "NewRoot: $NewRoot"

if (-not (Test-Path $NewRoot)) {
    New-Item -ItemType Directory -Path $NewRoot -Force | Out-Null
    Write-Host "Créé: $NewRoot"
}

$oldFiles = @()
if (Test-Path $OldRoot) {
    $oldFiles = @(Get-ChildItem -Path $OldRoot -Recurse -File)
}
Write-Host "Fichiers trouvés (ancien): $($oldFiles.Count)"

$migrated = 0
$skippedSame = 0
$conflicts = @()
$missingPhysical = @()

foreach ($f in $oldFiles) {
    $rel = $f.FullName.Substring((Resolve-Path $OldRoot).Path.Length).TrimStart('\', '/').Replace('\', '/')
    $dest = Join-Path $NewRoot ($rel -replace '/', [IO.Path]::DirectorySeparatorChar)
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    if (Test-Path $dest) {
        $srcHash = Get-Sha256Hex $f.FullName
        $dstHash = Get-Sha256Hex $dest
        if ($f.Length -eq (Get-Item $dest).Length -and $srcHash -eq $dstHash) {
            $skippedSame++
            continue
        }
        $conflicts += [PSCustomObject]@{
            Relatif = $rel
            SrcSize = $f.Length
            DstSize = (Get-Item $dest).Length
            SrcHash = $srcHash
            DstHash = $dstHash
        }
        Write-Host "CONFLIT: $rel (non écrasé)"
        continue
    }

    Copy-Item -LiteralPath $f.FullName -Destination $dest -Force:$false
    $migrated++
    Write-Host "Migré: $rel"
}

Write-Host "Migrés: $migrated | Identiques déjà présents: $skippedSame | Conflits: $($conflicts.Count)"

# Vérification vs SQL
Write-Host "`n=== Vérification PIECE_JOINTE ==="
$rows = sqlcmd -S $SqlServer -d $Database -E -h -1 -W -s "`t" -Q "SET NOCOUNT ON; SELECT CAST(IdPieceJointe AS varchar(20)), CAST(FK_DemandePaiement AS varchar(20)), CheminRelatif, CAST(TailleOctets AS varchar(20)), HashSha256 FROM dpm.PIECE_JOINTE;"
$inconsistencies = @()
$ok = 0
$sqlMissingFile = 0

foreach ($line in $rows) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line -split "`t"
    if ($parts.Count -lt 5) { continue }
    $id = $parts[0].Trim()
    $idDemande = $parts[1].Trim()
    $key = $parts[2].Trim()
    $sqlSize = [long]$parts[3].Trim()
    $sqlHash = $parts[4].Trim().ToLowerInvariant()

    $abs = Join-Path $NewRoot ($key -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $abs)) {
        $sqlMissingFile++
        $missingPhysical += [PSCustomObject]@{
            IdPieceJointe = $id
            IdDemandePaiement = $idDemande
            StorageKey = $key
            TailleSql = $sqlSize
            HashSql = $sqlHash
        }
        continue
    }

    $fileSize = (Get-Item -LiteralPath $abs).Length
    $fileHash = (Get-Sha256Hex $abs).ToLowerInvariant()
    if ($fileSize -ne $sqlSize -or $fileHash -ne $sqlHash) {
        $inconsistencies += [PSCustomObject]@{
            IdPieceJointe = $id
            IdDemandePaiement = $idDemande
            StorageKey = $key
            TailleSql = $sqlSize
            TailleFichier = $fileSize
            HashSql = $sqlHash
            HashCalcule = $fileHash
        }
        Write-Host "INCOHERENCE IdPieceJointe=$id key=$key"
        Write-Host "  taille SQL=$sqlSize fichier=$fileSize"
        Write-Host "  hash SQL=$sqlHash"
        Write-Host "  hash calc=$fileHash"
        continue
    }
    $ok++
}

Write-Host "`nOK hash/taille: $ok"
Write-Host "Fichiers manquants (SQL sans fichier physique): $sqlMissingFile"
Write-Host "Incohérences: $($inconsistencies.Count)"
Write-Host "Ancien dossier NON supprimé: $OldRoot"

if ($inconsistencies.Count -gt 0) {
    Write-Host "ARRÊT: incohérences détectées."
    exit 2
}

Write-Host "Migration terminée (StorageKey SQL inchangés)."
