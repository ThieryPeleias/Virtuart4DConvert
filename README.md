# Virtuart4DConvert

CLI tool that converts MPP/XER/P6/XPP construction schedules to the `v4d.json` format
consumed by the [Virtuart4D](https://github.com/ThieryPeleias/Virtuart4D) UE plugin.
Uses [MPXJ.Net](https://mpxj.org) internally.

---

## Build from source

### 1. Install .NET SDK

Check if already installed:

```
dotnet --version
```

If missing or below 8.x, download from: https://dotnet.microsoft.com/download/dotnet/8.0

### 2. Clone the repo

```
git clone https://github.com/ThieryPeleias/Virtuart4DConvert.git
cd Virtuart4DConvert
```

### 3. Run the build script

```
build.bat
```

The script handles everything:

- Checks .NET SDK version
- Checks for MPXJ.Net updates on NuGet and upgrades automatically if found
- Downloads all dependencies from NuGet (first run takes a few minutes)
- Compiles and publishes a self-contained Windows exe
- Removes `.pdb` debug symbols
- Bundles the `licenses/` folder
- Produces `zip\Virtuart4DConvert.zip`
- Deletes staging files — only the ZIP remains

Output: `zip\Virtuart4DConvert.zip`

---

## Install into the plugin

Two options:

**Option A — via the plugin dialog (recommended)**

In the Unreal Editor, open the Virtuart4D panel and click **New (Pro)**.
When the *Pro Import Mode* dialog appears, click **Install from ZIP**
and select `zip\Virtuart4DConvert.zip`.
The plugin extracts it automatically and enables Pro mode.

**Option B — manual extraction**

Extract `zip\Virtuart4DConvert.zip` directly into:

```
<UE project>\Plugins\Virtuart4DPlugin\Binaries\ThirdPartyTools\Virtuart4DConvert\
```

After extraction that folder must contain `Virtuart4DConvert.exe`.

---

## Licenses

`licenses/` contains license texts for all bundled libraries, required for LGPL and Apache 2.0 compliance:

| Library | License |
|---|---|
| [MPXJ](https://mpxj.org) by Jon Iles | GNU LGPL 2.1 or later |
| Apache POI | Apache 2.0 |
| jsoup | MIT |
| SQLite JDBC | Apache 2.0 |
| RTF Parser Kit | Apache 2.0 |

---

## Custom exe

You can replace `Virtuart4DConvert.exe` in the plugin's
`Binaries/ThirdPartyTools/Virtuart4DConvert/` folder with your own build,
provided it accepts `<input-file> <output.json>` as arguments and writes a valid `v4d.json` schema v1.
