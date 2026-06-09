<div align="center">

# 🏗️ Virtuart4DConvert

**CLI tool that converts MPP / XER / P6 / XPP construction schedules to the `v4d.json` format.**

[![Website](https://img.shields.io/badge/🌐_Website-virtuart4d.com-007586?style=for-the-badge)](https://virtuart4d.com/)
[![Unreal Engine](https://img.shields.io/badge/Unreal_Engine-5.7-0E1128?style=for-the-badge&logo=unrealengine&logoColor=white)](https://www.unrealengine.com/)
[![.NET](https://img.shields.io/badge/.NET_8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Proprietary-red?style=for-the-badge)](https://github.com/ThieryPeleias/Virtuart4D)

![platform](https://img.shields.io/badge/platform-Windows-blue?style=flat-square)
![build](https://img.shields.io/badge/build-self--contained-brightgreen?style=flat-square)
![status](https://img.shields.io/badge/status-production-success?style=flat-square)

*Consumed by the [Virtuart4D](https://github.com/ThieryPeleias/Virtuart4D) Unreal Engine plugin for **BIM 4D planning & immersive visualization**. Uses [MPXJ.Net](https://mpxj.org) internally.*

</div>

---

> [!NOTE]
> **Virtuart** offers BIM 4D planning and immersive Unreal Engine visualization solutions to transform how you plan and showcase your projects. Learn more at [virtuart4d.com](https://virtuart4d.com/).

> [!IMPORTANT]
> This tool produces a **self-contained Windows executable** — no .NET runtime installation is required on the target machine.

## 📖 Table of Contents

<table><tr><td>

- [🔧 Build from Source](#-build-from-source)
  - [1. Install .NET SDK](#1-install-net-sdk)
  - [2. Clone the Repo](#2-clone-the-repo)
  - [3. Run the Build Script](#3-run-the-build-script)
- [📦 Install into the Plugin](#-install-into-the-plugin)
  - [Option A — Via the Plugin Dialog (Recommended)](#option-a--via-the-plugin-dialog-recommended)
  - [Option B — Manual Extraction](#option-b--manual-extraction)
- [📜 Licenses](#-licenses)

</td></tr></table>

## 🔧 Build from Source

### 1. Install .NET SDK

Check if already installed:

```bash
dotnet --version
```

> [!TIP]
> If missing or below **8.x**, download from: [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)

### 2. Clone the Repo

```bash
git clone https://github.com/ThieryPeleias/Virtuart4DConvert.git
cd Virtuart4DConvert
```

### 3. Run the Build Script

```bash
build.bat
```

<details open>
<summary><b>What the script does</b></summary>

| Step | Description |
|------|-------------|
| ✅ | Checks .NET SDK version |
| 🔄 | Checks for MPXJ.Net updates on NuGet and upgrades automatically if found |
| 📥 | Downloads all dependencies from NuGet (first run takes a few minutes) |
| ⚙️ | Compiles and publishes a self-contained Windows exe |
| 🧹 | Removes `.pdb` debug symbols |
| 📄 | Bundles the `licenses/` folder |
| 📦 | Produces `zip\Virtuart4DConvert.zip` |
| 🗑️ | Deletes staging files — only the ZIP remains |

</details>

**Output:** `zip\Virtuart4DConvert.zip`

---

## 📦 Install into the Plugin

Two options:

### Option A — Via the Plugin Dialog (Recommended)

In the Unreal Editor, open the Virtuart4D panel and click **New (Pro)**.
When the *Pro Import Mode* dialog appears, click **Install from ZIP**
and select `zip\Virtuart4DConvert.zip`.
The plugin extracts it automatically and enables Pro mode.

### Option B — Manual Extraction

Extract `zip\Virtuart4DConvert.zip` directly into:

```
<UE project>\Plugins\Virtuart4DPlugin\Binaries\ThirdPartyTools\Virtuart4DConvert\
```

> [!WARNING]
> After extraction, that folder **must** contain `Virtuart4DConvert.exe` at its root.

---

## 📜 Licenses

`licenses/` contains license texts for all bundled libraries, required for LGPL and Apache 2.0 compliance:

| Library | License |
|---------|---------|
| [MPXJ](https://mpxj.org) by Jon Iles | GNU LGPL 2.1 or later |
| Apache POI | Apache 2.0 |
| jsoup | MIT |
| SQLite JDBC | Apache 2.0 |
| RTF Parser Kit | Apache 2.0 |

---

<div align="center">

**[🌐 Website](https://virtuart4d.com/)** · **[🔌 Virtuart4D Plugin](https://github.com/ThieryPeleias/Virtuart4D)** · **[📦 MPXJ](https://mpxj.org)**

*Made with ❤️ by [Virtuart](https://virtuart4d.com/)*

</div>
