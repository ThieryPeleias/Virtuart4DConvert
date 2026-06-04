# Virtuart4DConvert — Build & Update Guide

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Building the Executable

```bash
git clone https://github.com/Virtuart4D/Virtuart4DConvert.git
cd Virtuart4DConvert

dotnet restore
dotnet publish -c Release -r win-x64 --self-contained
```

The output will be in the `publish/` folder.

---

## Creating the Distribution ZIP

The ZIP **must** contain `Virtuart4DConvert.exe` and the `ikvm/win-x64/` folder at the root level.

```powershell
Compress-Archive -Path publish\* -DestinationPath Virtuart4DConvert-win-x64.zip
```

---

## Updating (when the repository is updated)

```bash
cd Virtuart4DConvert

git pull origin main
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained
```

Repackage the ZIP if needed:

```powershell
Compress-Archive -Path publish\* -DestinationPath Virtuart4DConvert-win-x64.zip -Force
```

---

## Updating the MPXJ.Net Dependency

```bash
# Check for newer versions
dotnet list package --outdated

# Update to latest
dotnet add package MPXJ.Net

# Or a specific version
dotnet add package MPXJ.Net --version <new-version>

# Rebuild
dotnet publish -c Release -r win-x64 --self-contained
```
