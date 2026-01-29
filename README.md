# IECODE - Inazuma Eleven Victory Road Toolkit

IECODE est une suite d'outils puissante conçue pour le reverse engineering, l'extraction de données et le modding d'**Inazuma Eleven: Victory Road** (Beta & Full Version). Ce projet permet d'extraire les archives du jeu, d'analyser les formats propriétaires de Level-5 et de convertir les données binaires en formats exploitables (JSON, PNG, GLB).

## 🛠️ Installation Rapide

Pour installer automatiquement .NET, compiler le projet et l'ajouter à votre PATH global :

1. Ouvrez un terminal PowerShell en tant qu'administrateur.
2. Lancez le script de configuration :
```powershell
./scripts/setup.ps1
```
3. Redémarrez votre terminal. Vous pouvez maintenant utiliser partout la commande `iecode`.

---

## 🚀 Guide d'Extraction et de Conversion

Ce guide détaille les deux phases principales pour obtenir une base de données complète du jeu.

### 📦 Phase 1 : Extraction des fichiers (Dump)

Cette étape extrait les fichiers bruts des archives Criware (`.cpk`) vers un dossier local. L'outil gère automatiquement la décompression, le déchiffrement et permet la reprise en cas d'interruption.

**Commande recommandée :**
```powershell
# Extraction complète vers le dossier 'dump'
# -o : Dossier de sortie
# -t : Nombre de threads (défaut: 8)
# --verbose : Affiche la progression détaillée
./bin/publish/iecode.exe dump -o C:\iecode\dump --verbose
```

*   **Smart Resume** : Grâce au manifest `.iecode-manifest.json`, vous pouvez relancer la commande et elle ignorera les fichiers déjà extraits.
*   **Localisation** : L'outil détecte automatiquement votre installation Steam du jeu.

---

### 📂 Phase 2 : Conversion en JSON

Une fois les fichiers extraits dans le dossier `dump`, vous pouvez convertir les fichiers de configuration binaires (`.cfg.bin`) en fichiers JSON lisibles.

#### 1. Conversion des modèles typés (Rapide)
Utilisez cette commande pour extraire les données essentielles (personnages, noms, sous-titres) avec un formatage optimisé.
```powershell
./bin/publish/iecode.exe dump-gamedata all --dump C:\iecode\dump --output C:\iecode\dump\data_json
```

#### 2. Conversion massive avec structure d'origine (Complet)
Si vous avez besoin de convertir TOUT le dossier `gamedata` tout en préservant l'arborescence des dossiers (très utile pour les scripts d'analyse), utilisez ce script PowerShell :

```powershell
$srcBase = "C:\iecode\dump\data\common\gamedata"
$destBase = "C:\iecode\dump\data_json\gamedata"

Get-ChildItem -Path $srcBase -Filter "*.cfg.bin" -Recurse | Where-Object { $_.FullName -notmatch "\\map\\" } | ForEach-Object {
    $relPath = $_.FullName.Substring($srcBase.Length + 1)
    $destFile = Join-Path $destBase ($relPath + ".json")
    $destDir = Split-Path $destFile
    if (!(Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force }
    
    Write-Host "Converting: $relPath..."
    & "./bin/publish/iecode.exe" config read $_.FullName -o $destFile
}
```
*Note : Nous utilisons `Where-Object` pour ignorer le dossier `map`, car il contient des milliers de fichiers de géométrie peu utiles pour l'analyse de données.*

#### 3. Conversion des Textes (Localisation)
Pour extraire les textes français, anglais ou japonais :
```powershell
$langs = @("fr", "en", "ja")
foreach ($lang in $langs) {
    # Même logique de script que ci-dessus en ciblant /text/$lang
}
```

---

## 🛠️ Architecture du Projet

*   **IECODE.Core** : Bibliothèque centrale (Logique CPK, Config, Crypto).
*   **IECODE.CLI** : Interface ligne de commande (`iecode.exe`).
*   **IECODE.Desktop** : Interface graphique (Avalonia) incluant un Memory Editor et un Game Launcher sans EAC.

## 📁 Formats de Fichiers Supportés
| Extension | Description | conversion |
| :--- | :--- | :--- |
| `.cpk` | Archives Criware | `iecode dump` |
| `.cfg.bin` | Fichiers de configuration Level-5 | `iecode config read` |
| `.g4tx` | Textures propriétaires | `iecode convert` (-> PNG) |
| `.g4mg` | Géométrie / Modèles | `iecode convert` (-> GLB) |

---

## 🛡️ License
Distribué sous licence MIT. Voir `LICENSE` pour plus de détails.

---
*Made with ⚽ for the Inazuma Eleven community.*
