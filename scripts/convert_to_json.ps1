# Conversion massive de gamedata et text en JSON
# Ce script préserve l'architecture des dossiers et ignore le dossier 'map'

$IECODE_EXE = "$PSScriptRoot\..\bin\publish\iecode.exe"
$DUMP_ROOT = "C:\iecode\dump"
$JSON_ROOT = "C:\iecode\dump\data_json"

function Convert-Dir($srcSubPath, $destSubName) {
    $srcDir = Join-Path $DUMP_ROOT $srcSubPath
    $destBase = Join-Path $JSON_ROOT $destSubName
    
    if (!(Test-Path $srcDir)) {
        Write-Host "Répertoire introuvable : $srcDir" -ForegroundColor Yellow
        return
    }

    Write-Host "Traitement de : $srcSubPath" -ForegroundColor Cyan
    
    Get-ChildItem -Path $srcDir -Filter "*.cfg.bin" -Recurse | Where-Object { $_.FullName -notmatch "\\map\\" } | ForEach-Object {
        $relPath = $_.FullName.Substring($srcDir.Length + 1)
        $destFile = Join-Path $destBase ($relPath + ".json")
        $destDir = Split-Path $destFile
        
        if (!(Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        
        Write-Host "  -> Conversion : $relPath"
        & $IECODE_EXE config read $_.FullName -o $destFile
    }
}

# 1. Conversion de Gamedata
Convert-Dir "data\common\gamedata" "gamedata"

# 2. Conversion des Textes (FR, EN, JA)
$langs = @("fr", "en", "ja")
foreach ($lang in $langs) {
    Convert-Dir "data\common\text\$lang" "text\$lang"
}

Write-Host "--- Conversion terminée ! ---" -ForegroundColor Green
