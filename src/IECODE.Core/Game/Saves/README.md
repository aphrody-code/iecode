# IECODE.Core - Save System & Steam API Integration

**Implementation Date**: December 7, 2025  
**Source Documentation**:
- `docs/nie-analysis/SAVE_SYSTEM_ANALYSIS.md`
- `docs/nie-analysis/STEAM_API_INTEGRATION.md`

---

## 📦 New Modules

### SaveSystem Namespace

#### `SaveType.cs`
- **Purpose**: Enum for save type identifiers
- **Types**: Regular (0x00), System (0x01), AutoSave (0x02), Header (0x03), Unknown4 (0x04)
- **Source**: Lines 2180562-2180566 in nie.exe

#### `SaveSlotEntry.cs`
- **Purpose**: Individual save slot structure (32 bytes)
- **Features**:
  - UTF-8 name parsing
  - Identifier matching (`AUTOSAVE`, `SYSTEM`, `HEADERSAVE`)
  - Save ID extraction at offset +8
- **Source**: FUN_14151d1a0 (lines 3724305-3724350)

#### `SaveContainer.cs`
- **Purpose**: Main save container structure (0x1040 bytes)
- **Capacity**: 8 slots maximum (256 bytes total)
- **Features**:
  - Dual save name support (short/long)
  - Slot enumeration and filtering
  - Save type detection
  - Metadata extraction
- **Source**: FUN_14151d4a0 (lines 3724771-3724820)

#### `SaveSystemManager.cs`
- **Purpose**: High-level save operations with Steam validation
- **Features**:
  - Save container parsing
  - AutoSave slot discovery
  - Header/System save detection
  - Steam ticket validation integration
  - Container summary generation

---

### Steam Namespace

#### `SteamCallbackIds.cs`
- **Purpose**: Steam callback ID constants
- **Callbacks**:
  - `UserAchievementStored` (0x44e / 1102)
  - `UserStatsStored` (0x44f / 1103)
  - `OverlayActivated` (0x14b / 331)
  - `GameOverlayActivated` (0xa8 / 168)
  - `UserStatsReceived` (0x2ca / 714)
  - 3 unknown callbacks (0x8fe, 0xaf1, 0xaf2)
- **Source**: Lines 697152-698000

#### `EncryptedTicketContext.cs`
- **Purpose**: Steam encrypted ticket structure
- **Fields**:
  - Encrypted/decrypted data buffers
  - 32-byte decryption key
  - App ID and Steam ID
  - Custom user data (save metadata)
- **Source**: Lines 698515-698560

#### `SteamEncryptedAppTicket.cs`
- **Purpose**: P/Invoke wrappers for `sdkencryptedappticket64.dll`
- **Functions**:
  - `BDecryptTicket` - Decrypt ticket with 32-byte key
  - `BIsTicketForApp` - Verify app ID
  - `GetTicketSteamID` - Extract user Steam ID
  - `GetUserVariableData` - Get save metadata
  - `ValidateSaveTicket` - High-level validation
- **Source**: Lines 698523-698540

---

## 🔧 Usage Examples

### Parsing a Save Container

```csharp
// Read save data from file/EOS
byte[] saveData = await ReadSaveDataAsync();

// Parse container
var container = SaveSystemManager.ParseContainer(saveData);

// Get summary
var info = SaveSystemManager.GetContainerInfo(container);
Console.WriteLine($"Save: {info.SaveName}");
Console.WriteLine($"Type: {info.SaveType}");
Console.WriteLine($"Slots: {info.SlotCount}");
Console.WriteLine($"AutoSaves: {info.AutoSaveCount}");
```

### Finding AutoSave Slots

```csharp
// Find all AutoSave entries
var autoSaves = SaveSystemManager.FindAutoSaveSlots(container);

foreach (var (slotIndex, saveId) in autoSaves)
{
    Console.WriteLine($"AutoSave #{saveId} in slot {slotIndex}");
}
```

### Validating Save with Steam Ticket

```csharp
using var saveManager = new SaveSystemManager(
    appId: 2697250,  // IEVR Steam App ID
    decryptionKey: GetHardcodedKey()  // 32-byte key from executable
);

// Get current user's Steam ID
var steamId = SteamUser.GetSteamID();

// Validate save
bool isValid = saveManager.ValidateSave(
    container,
    encryptedTicket,
    steamId,
    expectedUserData
);

if (isValid)
{
    Console.WriteLine("Save validated successfully");
}
else
{
    Console.WriteLine("Save validation failed - possible tampering");
}
```

### Manual Ticket Validation

```csharp
bool valid = SteamEncryptedAppTicket.ValidateSaveTicket(
    encryptedTicket: ticketBytes,
    decryptionKey: keyBytes,
    appId: 2697250,
    expectedSteamId: steamId,
    expectedUserData: saveMetadata
);
```

---

## 🏗️ Architecture

### Save Flow

```
┌─────────────────────────────────────────────────────┐
│                  Game Engine                        │
│                  (nie.exe)                          │
└────────────┬────────────────────────┬───────────────┘
             │                        │
     ┌───────▼───────┐        ┌──────▼──────────┐
     │  Steam API    │        │   EOS SDK       │
     │  (DRM/Auth)   │        │   (Cloud Save)  │
     └───────┬───────┘        └──────┬──────────┘
             │                        │
   ┌─────────▼─────────┐    ┌────────▼─────────────┐
   │ Encrypted Ticket  │    │ PlayerDataStorage    │
   │ • App ID verify   │    │ • WriteFile          │
   │ • Steam ID check  │    │ • ReadFile           │
   │ • MD5 integrity      │    │                      │
   └───────────────────┘    └──────────────────────┘
```

### Data Structures

```
SaveContainer (0x1040 bytes)
├─ Flags (0x08)
├─ SlotPointer (0x60)
├─ SaveName (0x120 or 0x1A0)
├─ Slots[8] (0x220 - 0x31F)
│  └─ SaveSlotEntry (32 bytes each)
│     ├─ Name[32]
│     └─ SaveId (offset +8)
├─ SlotCount (0x680)
├─ Metadata (0x688-0x694)
└─ HasLongName (0x699)
```

---

## 🔐 Security Model

**Multi-layered validation**:

1. **Client Layer** (IECODE.Core)
   - Parse save structure
   - Validate format
   - Extract metadata

2. **Steam Layer** (`sdkencryptedappticket64.dll`)
   - Decrypt ticket (AES-256)
   - Verify app ownership
   - Authenticate user Steam ID
   - Validate custom data

3. **EOS Layer** (Epic Online Services)
   - MD5 integrity check
   - Server-side validation
   - Cloud storage management

**Result**: Cannot load saves from different Steam accounts or tampered files.

---

## ⚡ Performance

### Zero-Allocation Parsing

- `SaveSlotEntry`: Uses `fixed` buffers, stack-only access
- `SaveContainer`: `MemoryMarshal.Read<T>` for direct casting
- Steam API: `Span<byte>` parameters via `LibraryImport`
- No heap allocations during validation

### AOT Compatibility

✅ All code is Native AOT-compatible:
- `LibraryImport` instead of `DllImport`
- `Span<T>` for zero-copy operations
- No reflection usage
- `[SupportedOSPlatform("windows")]` attributes

---

## 📚 References

- [Save System Analysis](../../docs/nie-analysis/SAVE_SYSTEM_ANALYSIS.md)
- [Steam API Integration](../../docs/nie-analysis/STEAM_API_INTEGRATION.md)
- [Steamworks.NET GitHub](https://github.com/rlabrecque/Steamworks.NET)
- [EOS PlayerDataStorage](https://dev.epicgames.com/docs/game-services/player-data-storage)

---

**Build Status**: ✅ 0 errors, 0 warnings  
**AOT Compatible**: ✅ Yes  
**Platform**: Windows x64
