# Script d'installation, build et déploiement de IECODE
# Ce script vérifie .NET 9.0, compile le projet et l'ajoute au PATH global de l'utilisateur.

$REQUIRED_DOTNET = "9.0"
$PROJECT_ROOT = "$PSScriptRoot\.."
$PUBLISH_DIR = "$PROJECT_ROOT\bin\publish"

Write-Host "--- Installation & Build IECODE ---" -ForegroundColor Cyan

# 1. Vérification de .NET SDK
Write-Host "[1/4] Vérification de .NET SDK $REQUIRED_DOTNET..."
$dotnetInstalled = $false
try {
    $dotnetVersion = & dotnet --version
    if ($dotnetVersion -like "$REQUIRED_DOTNET*") {
        Write-Host "      .NET $dotnetVersion est déjà installé." -ForegroundColor Green
        $dotnetInstalled = $true
    }
} catch {
    Write-Host "      .NET SDK n'est pas installé." -ForegroundColor Red
}

if (!$dotnetInstalled) {
    Write-Host "      Installation de .NET SDK 9.0 en cours (via winget)..." -ForegroundColor Yellow
    winget install Microsoft.DotNet.SDK.9 --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        Write-Host "      Erreur lors de l'installation de .NET. Veuillez l'installer manuellement : https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Red
        exit 1
    }
}

# 2. Build et Publish
Write-Host "[2/4] Compilation (Build & Publish) de IECODE..." -ForegroundColor Cyan
Set-Location $PROJECT_ROOT
& dotnet publish src/IECODE.CLI/IECODE.CLI.csproj -c Release -o $PUBLISH_DIR --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Host "      Erreur lors de la compilation." -ForegroundColor Red
    exit 1
}
Write-Host "      Compilation réussie dans : $PUBLISH_DIR" -ForegroundColor Green

# 3. Ajout au PATH Global
Write-Host "[3/4] Ajout au PATH de l'utilisateur..." -ForegroundColor Cyan
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($currentPath -notlike "*$PUBLISH_DIR*") {
    $newPath = "$currentPath;$PUBLISH_DIR"
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    Write-Host "      $PUBLISH_DIR a été ajouté au PATH de l'utilisateur." -ForegroundColor Green
    Write-Host "      NOTE: Redémarrez votre terminal pour utiliser la commande 'iecode'." -ForegroundColor Yellow
} else {
    Write-Host "      Le chemin est déjà présent dans le PATH." -ForegroundColor Gray
}

# 4. Vérification finale
Write-Host "[4/4] Finalisation..." -ForegroundColor Cyan
Write-Host "--- Opération terminée avec succès ! ---" -ForegroundColor Green
Write-Host "Vous pouvez maintenant utiliser 'iecode --help' dans un nouveau terminal."
