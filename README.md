# Virtuart4DConvert — Build & Distribution Guide

CLI tool wrapping [MPXJ.Net](https://mpxj.org) to convert MPP/XER/P6/XPP schedules
to the `v4d.json` format consumed by the Virtuart4D UE plugin.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Building

```powershell
git clone https://github.com/ThieryPeleias/Virtuart4DConvert.git
cd Virtuart4DConvert
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained
```

Output: `publish/` folder.

---

## Distribution ZIP

### What to include

| Path in ZIP | Required |
|---|---|
| `Virtuart4DConvert.exe` | Yes |
| `ikvm/win-x64/` | Yes (native IKVM runtime, cannot be single-filed) |
| `licenses/` | Yes (license texts — see below) |
| `*.pdb` | **No** — omit debug symbols from release |

### Licenses folder (required for Apache 2.0 + LGPL compliance)

Already present in the repo root at `licenses/`:
`mpxj-lgpl2.1.txt`, `apache-poi-apache2.txt`, `jsoup-mit.txt`,
`sqlite-jdbc-apache2.txt`, `rtfparserkit-apache2.txt`.

The build script copies this folder into `publish\licenses\` before zipping.

### Build script (PowerShell)

The ZIP is always named `Virtuart4DConvert.zip` (fixed URL — GitHub `/releases/latest/download/`
always resolves to the most recent published release). The exe inside is always
`Virtuart4DConvert.exe`; the version is embedded in its Windows file properties.

```powershell
# Remove .pdb files (not needed at runtime)
Get-Item publish\*.pdb -ErrorAction SilentlyContinue | Remove-Item -Force

# Copy licenses folder (tracked in repo root) into publish/
Copy-Item -Recurse -Force licenses publish\licenses

# Pack — fixed name, no version in filename
Compress-Archive -Path publish\* -DestinationPath Virtuart4DConvert.zip -Force
Write-Host "Created: Virtuart4DConvert.zip"
```

Upload `Virtuart4DConvert.zip` as a release asset named exactly `Virtuart4DConvert.zip`
on the GitHub release. The plugin downloads from `.../releases/latest/download/Virtuart4DConvert.zip`.

---

## Updating MPXJ.Net

```powershell
# Check for newer versions
dotnet list package --outdated

# Update
dotnet add package MPXJ.Net --version <new-version>

# Bump <Version> in Virtuart4DConvert.csproj to new release version (e.g. 0.2.0)

# Rebuild + repack
dotnet publish -c Release -r win-x64 --self-contained
# Run the build script above
```

---

## Release checklist

- [ ] Bump `<Version>` in `Virtuart4DConvert.csproj`
- [ ] Run `dotnet publish -c Release -r win-x64 --self-contained`
- [ ] Copy licence files into `publish\licenses\`
- [ ] Run the build script → `Virtuart4DConvert_vX.Y.Z.zip` created
- [ ] Upload `Virtuart4DConvert.zip` as asset on the GitHub release (exact name required)
- [ ] Git tag format: `v0.2.0` (with `v` prefix — GitHub uses it as `tag_name` for version check)
