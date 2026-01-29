# Dump complet du jeu Inazuma Eleven: Victory Road
# Ce script extrait tous les fichiers CPK vers le dossier 'dump'

$IECODE_EXE = "$PSScriptRoot\..\bin\publish\iecode.exe"
$OUTPUT_DIR = "C:\iecode\dump"

Write-Host "--- Début du Dump complet ---" -ForegroundColor Cyan
& $IECODE_EXE dump -o $OUTPUT_DIR --verbose

Write-Host "--- Dump terminé ! ---" -ForegroundColor Green
