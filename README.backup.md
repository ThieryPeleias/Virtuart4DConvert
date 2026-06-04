# Virtuart4DConvert

A command-line converter that transforms project management files into the **Virtuart4D JSON** format (`.v4d.json`).  
Built on top of [MPXJ](https://github.com/joniles/mpxj) — the most comprehensive library for reading project schedules.

---

## Supported Input Formats

| Format | Extension | Source Application |
|---|---|---|
| Microsoft Project | `.mpp` | MS Project 98 – 2021+ |
| Primavera P6 (XER) | `.xer` | Oracle Primavera P6 |
| Primavera P6 (XML) | `.xml` | Oracle Primavera P6 (PMXML) |
| Asta Powerproject | `.pp` | Asta Powerproject / Easyplan |
| Phoenix Project Manager | `.ppx` | Phoenix PM |
| Project Planner | `.xpp` | Asta Project Planner |
| MS Project XML | `.xml` | MS Project (MSPDI) |

> MPXJ supports [many more formats](https://www.mpxj.org/supported-formats.html). The converter uses `UniversalProjectReader`, which auto-detects the input format — any file MPXJ can read will work.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (required only for building from source)

---

## Building from Source

### 1. Clone the repository

```bash
git clone https://github.com/Virtuart4D/Virtuart4DConvert.git
cd Virtuart4DConvert
```

### 2. Restore dependencies

```bash
dotnet restore
```

### 3. Build (debug)

```bash
dotnet build
```

### 4. Publish a self-contained release

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

The output will be in `publish/`. The key files are:

```
publish/
├── Virtuart4DConvert.exe    ← main executable
├── ikvm/
│   └── win-x64/             ← IKVM native dependencies (required!)
├── ikvm.dll
├── *.pdb                    ← debug symbols (optional)
└── ...
```

> **Important:** The `ikvm/win-x64/` folder **must** remain alongside the `.exe` for the converter to work. IKVM provides the Java runtime bridge that MPXJ depends on.

### 5. Create the distribution ZIP

Package the `publish/` output into a zip file:

```powershell
Compress-Archive -Path publish\* -DestinationPath Virtuart4DConvert-win-x64.zip
```

The zip **must** contain `Virtuart4DConvert.exe` and the `ikvm/win-x64/` folder at the root level.

---

## Usage

```bash
Virtuart4DConvert <input-file> <output.v4d.json>
```

### Examples

```bash
# Convert an MPP file
Virtuart4DConvert project.mpp output.v4d.json

# Convert a Primavera XER file
Virtuart4DConvert schedule.xer output.v4d.json

# Convert a Primavera P6 XML file
Virtuart4DConvert primavera.xml output.v4d.json

# Check installed version
Virtuart4DConvert --version
```

### Exit Codes

| Code | Meaning |
|---|---|
| `0` | Success |
| `1` | Error (invalid arguments, file not found, parse failure) |

Errors are printed to `stderr`.

---

## Output Format: v4d.json Schema v1

The converter outputs a JSON file following **schema version 1**. Below is the full structure:

```jsonc
{
  "schemaVersion": 1,
  "source": {
    "tool": "Virtuart4DConvert",
    "version": "0.1.0",
    "mpxjVersion": "16.2.0.0",
    "originalFile": "project.mpp"
  },
  "currency": {
    "symbol": "$",
    "code": "USD"
  },
  "calendars": [
    {
      "uid": 1,
      "name": "Standard",
      "isBase": true,
      "baseCalendarUid": -1,
      "workWeek": [
        { "day": "MON", "ranges": [["08:00", "12:00"], ["13:00", "17:00"]] }
      ],
      "exceptions": [
        { "from": "2025-12-25", "to": "2025-12-25", "working": false }
      ]
    }
  ],
  "resources": [
    {
      "uid": 1,
      "name": "Engineer",
      "type": "Work",
      "stdRate": 50.0,
      "calendarUid": 1
    }
  ],
  "tasks": [
    {
      "uid": 1,
      "guid": "...",
      "name": "Design Phase",
      "calendarUid": 1,
      "start": "2025-01-06T08:00:00",
      "finish": "2025-01-10T17:00:00",
      "durationHours": 40.0,
      "isSummary": false,
      "isMilestone": false,
      "outlineLevel": 1,
      "parentUid": -1,
      "wbs": "1.1",
      "percentComplete": 0.0,
      "cost": 2000.0,
      "accrual": "Prorated",
      "predecessors": [
        { "uid": 0, "type": "FS", "lagHours": 0.0 }
      ],
      "resourceAssignments": [
        { "resourceUid": 1, "units": 1.0, "workHours": 40.0 }
      ]
    }
  ]
}
```

### Field Reference

<details>
<summary><strong>Task Fields</strong></summary>

| Field | Type | Description |
|---|---|---|
| `uid` | `int` | Unique task ID |
| `guid` | `string?` | Global unique identifier |
| `name` | `string` | Task name |
| `calendarUid` | `int` | Calendar UID (-1 if none) |
| `start` | `string?` | Start date (`yyyy-MM-ddTHH:mm:ss`) |
| `finish` | `string?` | Finish date (`yyyy-MM-ddTHH:mm:ss`) |
| `durationHours` | `double` | Duration in hours |
| `isSummary` | `bool` | Summary (parent) task |
| `isMilestone` | `bool` | Milestone task |
| `outlineLevel` | `int` | WBS outline level |
| `parentUid` | `int` | Parent task UID (-1 if root) |
| `wbs` | `string?` | WBS code |
| `percentComplete` | `double?` | Completion percentage |
| `cost` | `double?` | Total cost |
| `accrual` | `string?` | Fixed cost accrual type |
| `predecessors` | `array?` | Predecessor dependencies |
| `resourceAssignments` | `array?` | Resource assignments |

</details>

<details>
<summary><strong>Calendar Fields</strong></summary>

| Field | Type | Description |
|---|---|---|
| `uid` | `int` | Unique calendar ID |
| `name` | `string` | Calendar name |
| `isBase` | `bool` | `true` if base calendar (no parent) |
| `baseCalendarUid` | `int` | Parent calendar UID (-1 if base) |
| `workWeek` | `array` | Working days with time ranges |
| `exceptions` | `array` | Non-working / special day overrides |

</details>

<details>
<summary><strong>Resource Fields</strong></summary>

| Field | Type | Description |
|---|---|---|
| `uid` | `int` | Unique resource ID |
| `name` | `string` | Resource name |
| `type` | `string` | Resource type (`Work`, `Material`, `Cost`) |
| `stdRate` | `double?` | Standard rate |
| `calendarUid` | `int` | Associated calendar UID (-1 if none) |

</details>

---

## Integration with Virtuart4D Plugin

This converter is designed to work with the [Virtuart4D](https://github.com/Virtuart4D/Virtuart4D) Unreal Engine plugin. When installed:

1. Place `Virtuart4DConvert.exe` and the `ikvm/` folder in:
   ```
   <PluginDir>/Binaries/ThirdPartyTools/Virtuart4DConvert/
   ```
2. The plugin will **auto-detect** the converter on startup — no manual configuration needed.
3. Use the **Import (Pro)** button in the plugin to import project schedules.

Alternatively, point the plugin to your converter via **Browse for EXE…** in the MPXJ Activation dialog.

---

## Updating When the Repository is Updated

If the upstream MPXJ library or this converter receives an update, follow these steps to rebuild:

### Quick Update (same machine)

```bash
# 1. Pull latest changes
cd Virtuart4DConvert
git pull origin main

# 2. Update dependencies (picks up any MPXJ.Net version bumps)
dotnet restore

# 3. Rebuild the release
dotnet publish -c Release -r win-x64 --self-contained

# 4. (Optional) Repackage the zip
Compress-Archive -Path publish\* -DestinationPath Virtuart4DConvert-win-x64.zip -Force
```

### Updating the MPXJ.Net dependency

To upgrade to a newer version of MPXJ:

```bash
# Check available versions
dotnet list package --outdated

# Update to a specific version
dotnet add package MPXJ.Net --version <new-version>

# Or update to latest
dotnet add package MPXJ.Net

# Rebuild
dotnet publish -c Release -r win-x64 --self-contained
```

> **Note:** After updating MPXJ.Net, test with your project files to ensure compatibility. The JSON schema (`schemaVersion: 1`) should remain stable across MPXJ updates.

---

## Project Structure

```
Virtuart4DConvert/
├── Program.cs                    # Main converter logic + DTOs
├── Virtuart4DConvert.csproj      # Project config (.NET 8, MPXJ.Net 16.2.0)
├── .gitignore
├── publish/                      # Build output (git-ignored)
│   ├── Virtuart4DConvert.exe
│   └── ikvm/win-x64/
└── README.md
```

---

## Tech Stack

| Component | Version | Purpose |
|---|---|---|
| .NET | 8.0 | Runtime (self-contained, no install needed for end users) |
| [MPXJ.Net](https://www.nuget.org/packages/MPXJ.Net/) | 16.2.0 | Project file reader (C#/.NET wrapper) |
| [IKVM](https://github.com/ikvmnet/ikvm) | (bundled) | Java-to-.NET bridge used by MPXJ |

---

## License

This converter uses [MPXJ](https://github.com/joniles/mpxj), which is licensed under the [LGPL 2.1](https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html) (or later).

You are free to build and distribute your own version of this converter. The converter is **not bundled** with the Virtuart4D plugin — users must download or build it separately to comply with LGPL requirements.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| `Error reading file: ...` | Ensure the input file is a supported format and not corrupted |
| Converter crashes silently | Verify the `ikvm/win-x64/` folder is next to the `.exe` |
| `dotnet` command not found | Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Old MPXJ version after update | Run `dotnet restore` then republish |
| Plugin doesn't detect converter | Place files in `<PluginDir>/Binaries/ThirdPartyTools/Virtuart4DConvert/` |
