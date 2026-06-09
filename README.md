# Virtuart4DConvert

CLI tool that converts MPP/XER/P6/XPP construction schedules to the `v4d.json` format
consumed by the [Virtuart4D](https://github.com/ThieryPeleias/Virtuart4D) UE plugin.
Uses [MPXJ.Net](https://mpxj.org) internally.

---

## Prerequisites

- [.NET 8+ SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Build

```bat
build.bat
```

That's it. The script:

1. Checks .NET SDK version
2. Checks for MPXJ.Net updates on NuGet — upgrades automatically if found
3. Runs `dotnet publish` (downloads all dependencies from NuGet on first run)
4. Removes `.pdb` debug symbols
5. Copies `licenses/` into the package
6. Creates `zip\Virtuart4DConvert.zip`
7. Deletes staging files — only the ZIP remains

Output: `zip\Virtuart4DConvert.zip`

---

## Release

1. Run `build.bat`
2. Create a GitHub release with tag `v0.x.0` (with `v` prefix)
3. Upload `zip\Virtuart4DConvert.zip` as release asset — name must be exactly `Virtuart4DConvert.zip`

The Virtuart4D plugin downloads from `.../releases/latest/download/Virtuart4DConvert.zip`.
Fixed name = stable URL, always points to latest release.

---

## Licenses

`licenses/` contains the license texts for all bundled libraries:

| Library | License |
|---|---|
| [MPXJ](https://mpxj.org) by Jon Iles | GNU LGPL 2.1 or later |
| Apache POI | Apache 2.0 |
| jsoup | MIT |
| SQLite JDBC | Apache 2.0 |
| RTF Parser Kit | Apache 2.0 |

Required by LGPL and Apache 2.0 terms when distributing the compiled binary.

---

## Custom build

You can replace `Virtuart4DConvert.exe` in the plugin's
`Binaries/ThirdPartyTools/Virtuart4DConvert/` folder with your own build,
provided it accepts `<input-file> <output.json>` and writes a valid `v4d.json` schema v1.
