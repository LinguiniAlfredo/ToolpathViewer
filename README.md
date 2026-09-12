# Ablation Studio

A modern, high-performance Laser Ablation Studio & 3D Toolpath Viewer built with WPF, .NET 10, OpenTK, and WPF-UI.

## Requirements

- **Windows 10 / 11** (x64 or ARM64)
- **.NET 10 SDK** (required for building from source: [Download .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0))

*(Note: End users running the standalone executable do **not** need .NET 10 or any external runtimes installed.)*

---

## Quick Start: Building Standalone Executable

To compile the application and package it into a single standalone `.exe` file:

### Using the Build Script (PowerShell / Command Prompt)

```powershell
# Run the default publish target (Release win-x64 standalone with tests)
.\build.cmd

# Or using PowerShell directly:
.\build.ps1
```

The output executable will be placed in the `artifacts/` folder:
- `artifacts/AblationStudio.exe` (~75 MB, fully self-contained, single-file executable).

### Build Script Options

The build script supports several options:

| Parameter | Values | Default | Description |
|---|---|---|---|
| `-Target` | `Publish`, `Build`, `Test`, `Clean` | `Publish` | Action to execute |
| `-Configuration` | `Release`, `Debug` | `Release` | Build configuration |
| `-Runtime` | e.g. `win-x64`, `win-arm64` | `win-x64` | Target runtime architecture |
| `-OutputDir` | Path string | `./artifacts` | Directory to output the executable |
| `-SkipTests` | Switch | `false` | Skip test execution prior to publishing |
| `-NoCompression` | Switch | `false` | Disable single-file internal compression |
| `-NoReadyToRun` | Switch | `false` | Disable ReadyToRun ahead-of-time compilation |

#### Examples:

```powershell
# Run tests only:
.\build.ps1 -Target Test

# Fast publish skipping tests:
.\build.ps1 -SkipTests

# Clean build outputs and artifacts:
.\build.ps1 -Target Clean
```

---

## Building via `dotnet` CLI Directly

You can also use the standard `dotnet` CLI with the included publish profile:

```powershell
dotnet publish src/AblationStudio.App -p:PublishProfile=win-x64-standalone
```

Or manually specify publish arguments:

```powershell
dotnet publish src/AblationStudio.App/AblationStudio.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishReadyToRun=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./artifacts
```

---

## Running the Solution in Development

```powershell
# Build solution
dotnet build

# Run unit tests
dotnet test

# Run application in debug mode
dotnet run --project src/AblationStudio.App
```
