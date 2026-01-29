# Script d'archivage et de publication des données extraites (JSON)
# Ce script compresse le dossier data_json et l'ajoute à une release GitHub (nécessite GitHub CLI 'gh')

$JSON_DIR = "C:\iecode\dump\data_json"
$OUTPUT_ZIP = "C:\iecode\iecode_data_json.zip"
$TAG = "v1.0.0-data"

Write-Host "--- Archivage des données JSON ---" -ForegroundColor Cyan

if (!(Test-Path $JSON_DIR)) {
    Write-Host "Erreur : Le dossier $JSON_DIR n'existe pas." -ForegroundColor Red
    exit 1
}

# 1. Compression
Write-Host "[1/2] Compression de data_json en cours... (cela peut prendre du temps)" -ForegroundColor Yellow
if (Test-Path $OUTPUT_ZIP) { Remove-Item $OUTPUT_ZIP }
Compress-Archive -Path "$JSON_DIR\*" -DestinationPath $OUTPUT_ZIP -Force

Write-Host "      Archive créée : $OUTPUT_ZIP" -ForegroundColor Green

# 2. Upload vers GitHub Release
Write-Host "[2/2] Upload vers GitHub Release..." -ForegroundColor Cyan

# Vérification si 'gh' est installé
if (!(Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "Erreur : GitHub CLI (gh) n'est pas installé. L'upload est impossible automatiquement." -ForegroundColor Red
    Write-Host "Vous pouvez le télécharger ici : https://cli.github.com/" -ForegroundColor Yellow
    Write-Host "Alternativement, uploadez manuellement $OUTPUT_ZIP sur GitHub."
    exit 1
}

# Création ou mise à jour de la release
try {
    Write-Host "      Vérification de la release $TAG..."
    gh release view $TAG --repo aphrody-code/iecode 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "      Création d'une nouvelle release $TAG..."
        gh release create $TAG $OUTPUT_ZIP --title "Inazuma Eleven Game Data (JSON)" --notes "Export complet des fichiers cfg.bin au format JSON." --repo aphrody-code/iecode
    } else {
        Write-Host "      Mise à jour de la release existante $TAG..."
        gh release upload $TAG $OUTPUT_ZIP --clobber --repo aphrody-code/iecode
    }
    Write-Host "--- Upload terminé avec succès ! ---" -ForegroundColor Green
} catch {
    Write-Host "Erreur lors de l'interaction avec GitHub CLI." -ForegroundColor Red
}
