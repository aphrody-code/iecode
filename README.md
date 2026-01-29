# IECODE - Inazuma Eleven Victory Road Toolkit

IECODE est une suite d'outils puissante conçue pour le reverse engineering, l'extraction de données et le modding d'**Inazuma Eleven: Victory Road** (Beta & Full Version). Ce projet permet d'extraire les archives du jeu, d'analyser les formats propriétaires de Level-5 et de convertir les données binaires en formats exploitables (JSON, PNG, GLB).

---

## 🛠️ Installation et Utilisation (Workflow complet)

Pour une installation propre et une extraction complète, suivez les scripts dans cet ordre précis :

### 1. Installation Globale
Installe .NET 9.0 (si nécessaire), compile le projet et ajoute `iecode` à votre variable d'environnement PATH.
```powershell
# À lancer une seule fois (en Administrateur pour winget)
./scripts/setup.ps1
```
*Note : Redémarrez votre terminal après cette étape pour activer la commande `iecode` partout.*

### 2. Extraction du Jeu (Dump)
Extrait tous les fichiers des archives `.cpk` vers le dossier local `C:\iecode\dump`.
```powershell
# À lancer pour récupérer les fichiers bruts du jeu
./scripts/dump.ps1
```

### 3. Conversion en JSON
Convertit les fichiers binaires (`.cfg.bin`) en JSON lisible tout en préservant l'arborescence des dossiers.
```powershell
# À lancer une fois le dump terminé
./scripts/convert_to_json.ps1
```
*Note : Ce script ignore automatiquement les dossiers `map` pour optimiser le temps de traitement.*

---

## 📂 Détail des Scripts (Dossier `/scripts`)

| Script | Rôle |
| :--- | :--- |
| `setup.ps1` | Setup complet : SDK .NET, Build Release, Configuration du PATH global. |
| `dump.ps1` | Extraction massive des assets via la commande `iecode dump`. |
| `convert_to_json.ps1` | Conversion intelligente gamedata + text avec respect de l'arborescence. |

---

## 🛠️ Architecture du Projet

*   **IECODE.Core** : Bibliothèque centrale (Logique CPK, Config, Crypto).
*   **IECODE.CLI** : Interface ligne de commande (`iecode.exe`).
*   **IECODE.Desktop** : Interface graphique (Avalonia) incluant un Memory Editor et un Game Launcher sans EAC.

## 📁 Formats de Fichiers Supportés

| Extension | Description | Commande CLI |
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
