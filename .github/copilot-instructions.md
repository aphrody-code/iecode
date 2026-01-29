# IECODE - AI Coding Instructions

## 1. Project Context
- **Tooling**: .NET 9.0 based toolkit for **Inazuma Eleven: Victory Road** reverse engineering.
- **Core Components**:
  - `IECODE.Core`: Binary parsing (CPK, G4TX, G4PK, CFG.BIN), decryption, and game logic.
  - `IECODE.CLI`: Command-line interface (`iecode.exe`).
  - `IECODE.Desktop`: Avalonia-based GUI (Memory Editor, Game Launcher with EAC bypass).

## 2. Extraction & Conversion Workflow
**ALWAYS** follow the 3-step script workflow located in `scripts/`:

1.  **Setup** (`./scripts/setup.ps1`):
    - Installs .NET 9.0 (via winget).
    - Compiles the project in Release mode (`bin/publish`).
    - Adds the bin folder to the global user PATH.
2.  **Dump** (`./scripts/dump.ps1`):
    - Extracts all CPK archives to `C:\iecode\dump\`.
    - Handles smart resume and multi-threaded extraction.
3.  **Convert** (`./scripts/convert_to_json.ps1`):
    - Recursive conversion of `*.cfg.bin` to JSON.
    - Preserves directory architecture in `C:\iecode\dump\data_json\`.
    - Skips `map` directories to prioritize game data over geometry.

## 3. Critical Rules for AI Agents
- **Data Integrity**: Never modify raw `cfg.bin` files. Always work on JSON exports in `data_json/`.
- **Paths**: Default workspace is `C:\iecode\`. Extracted data resides in `C:\iecode\dump\`.
- **Command Usage**: Use the global `iecode` command if `setup.ps1` has been run, otherwise fallback to `./bin/publish/iecode.exe`.
- **Ignore Large Files**: Ensure `.gitignore` is respected. Never commit `dump/` or `data_json/` folders.

## 4. Technical Stack
- **Framework**: .NET 9.0 (C#).
- **GUI Framework**: Avalonia UI.
- **Binary Utilities**: Criware (CPK), G4TX (textures), G4PK (packages), CFG.BIN (configs).

## 5. Development Rituals
- **Commit Pattern**: Follow conventional commits (e.g., `feat:`, `fix:`, `docs:`, `tooling:`).
- **Testing**: Verify conversions by checking for key character entries (e.g., "Mamoru Endo") in `chara_text` exports.
