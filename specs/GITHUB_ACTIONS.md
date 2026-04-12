# GitHub Actions CI/CD Plan

## Overview

Three workflow files covering continuous integration, releases, and optional security scanning.

```text
.github/
  workflows/
    ci.yml        # Build + tests on every push and PR
    release.yml   # Publish and GitHub Release on version tags
    codeql.yml    # (optional) Weekly security scanning
```

---

## Workflow 1: `ci.yml` — Continuous Integration

**Triggers:** Every push to `master`, every PR targeting `master`.

**Runner:** `windows-latest` (Windows Server 2022) — required because `VidPare.App` targets `net8.0-windows10.0.22000.0` and needs Windows SDK reference assemblies.

**Jobs run in parallel:**
- `build` — restore, build `Release|x64`, upload artifact (7-day retention)
- `test-core` — build and run `VidPare.Core.Tests` unit tests, upload `.trx` results

```yaml
name: CI

on:
  push:
    branches: [master]
  pull_request:
    branches: [master]

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  build:
    name: Build (Release x64)
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/VidPare.Core/VidPare.Core.csproj', '**/VidPare.App/VidPare.App.csproj', 'global.json') }}
          restore-keys: |
            nuget-${{ runner.os }}-

      - name: Restore
        run: dotnet restore VidPare.sln

      - name: Build
        run: dotnet build VidPare.sln --configuration Release --no-restore -p:Platform=x64

      - name: Upload build artifact
        uses: actions/upload-artifact@v4
        with:
          name: vidpare-build-${{ github.run_number }}
          path: VidPare.App/bin/x64/Release/
          retention-days: 7

  test-core:
    name: Unit Tests (VidPare.Core)
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/VidPare.Core/VidPare.Core.csproj', '**/VidPare.Core.Tests/VidPare.Core.Tests.csproj', 'global.json') }}
          restore-keys: |
            nuget-${{ runner.os }}-

      - name: Restore
        run: dotnet restore VidPare.Core.Tests/VidPare.Core.Tests.csproj

      - name: Test
        run: >
          dotnet test VidPare.Core.Tests/VidPare.Core.Tests.csproj
          --configuration Release
          --logger "trx;LogFileName=core-tests.trx"
          --results-directory TestResults

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results-${{ github.run_number }}
          path: TestResults/*.trx
          retention-days: 14
```

---

## Workflow 2: `release.yml` — Publish and GitHub Release

**Trigger:** Push of a tag matching `v*.*.*` (e.g., `v1.0.0`).

```bash
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin v1.0.0
```

This triggers `release.yml` only — it does not re-run `ci.yml`.

**Publish flags:** `--runtime win-x64 --self-contained false`

`--self-contained false` is preferred because the Windows App SDK runtime must be installed separately regardless, so bundling the .NET runtime adds ~70 MB with no practical benefit for a Windows 11-only app.

**`PublishSingleFile=false`** is required. WinUI 3 has native side-by-side DLLs (`Microsoft.WindowsAppRuntime.Bootstrap.dll`, etc.) that break when bundled into a single file.

```yaml
name: Release

on:
  push:
    tags:
      - 'v*.*.*'

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  publish:
    name: Publish & Release
    runs-on: windows-latest

    permissions:
      contents: write

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/VidPare.App/VidPare.App.csproj', '**/VidPare.Core/VidPare.Core.csproj', 'global.json') }}
          restore-keys: |
            nuget-${{ runner.os }}-

      - name: Restore
        run: dotnet restore VidPare.sln

      - name: Publish (framework-dependent, win-x64)
        run: >
          dotnet publish VidPare.App/VidPare.App.csproj
          --configuration Release
          --runtime win-x64
          --self-contained false
          -p:Platform=x64
          -p:PublishSingleFile=false
          --output publish/VidPare

      - name: Get tag name
        id: tag
        run: echo "TAG=${GITHUB_REF#refs/tags/}" >> $GITHUB_OUTPUT
        shell: bash

      - name: Zip artifact
        run: Compress-Archive -Path publish/VidPare/* -DestinationPath VidPare-${{ steps.tag.outputs.TAG }}-win-x64.zip
        shell: pwsh

      - name: Create GitHub Release
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: >
          gh release create ${{ steps.tag.outputs.TAG }}
          --title "VidPare ${{ steps.tag.outputs.TAG }}"
          --generate-notes
          VidPare-${{ steps.tag.outputs.TAG }}-win-x64.zip

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: VidPare-${{ steps.tag.outputs.TAG }}-win-x64
          path: publish/VidPare/
          retention-days: 30
```

---

## Workflow 3 (Optional): `codeql.yml` — Security Scanning

```yaml
name: CodeQL

on:
  push:
    branches: [master]
  pull_request:
    branches: [master]
  schedule:
    - cron: '0 3 * * 1'   # Weekly on Monday at 03:00 UTC

jobs:
  analyze:
    name: Analyze (csharp)
    runs-on: windows-latest
    permissions:
      actions: read
      contents: read
      security-events: write

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: csharp
          build-mode: manual

      - name: Restore
        run: dotnet restore VidPare.sln

      - name: Build for CodeQL
        run: dotnet build VidPare.sln --configuration Release --no-restore -p:Platform=x64

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3
        with:
          category: /language:csharp
```

---

## Test Project: `VidPare.Core.Tests`

No tests exist yet. `VidPare.Core` contains pure .NET logic with no WinRT dependencies, making it immediately testable.

**Project file:** `VidPare.Core.Tests/VidPare.Core.Tests.csproj`

Must target `net8.0` (not the Windows TFM) so it runs without the Windows Desktop SDK.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.*">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\VidPare.Core\VidPare.Core.csproj" />
  </ItemGroup>
</Project>
```

**Testable surface (all pure .NET, no WinRT):**

| Class | What to test |
|---|---|
| `TimeFormatter` | `Format`, `PreciseFormat`, `FormatDuration` — zero, sub-minute, hour-spanning boundaries |
| `VideoDocument` | `CanOpen` extension allow-list (`.mp4`, `.mov`, `.m4v`, case-insensitive, rejects `.avi`); `FormattedFileSize` byte/KB/MB/GB thresholds |
| `TrimState` | `Duration` computed property, `Reset()` behavior, `PropertyChanged` firing from `[ObservableProperty]` |
| `ExportCapabilities` | `AvailableFormats` filtering when `CanEncodeHEVC = false` |

The `.sln` file must be updated to include `VidPare.Core.Tests` as a third project entry.

---

## Windows-Specific Considerations

**Always use `windows-latest`** (or pin to `windows-2022` for stability). This runner ships with VS 2022 Build Tools, which satisfies the AppxPackage MSBuild targets needed by WinUI 3. `ubuntu-latest` or `macos-latest` cannot build `VidPare.App`.

**No `setup-dotnet` action needed.** `global.json` uses `rollForward: latestFeature` and `windows-latest` already carries a qualifying .NET 8 SDK.

**The `AppxMSBuildToolsPath` hard-code in `.csproj`** is harmless on CI. It has a `Condition="Exists(...)"` guard pointing to the Community edition path. That path doesn't exist on the runner (Build Tools edition is installed instead), so the condition is false and MSBuild finds the WinRT targets through its own VS-installation discovery. No workaround needed.

**Do not pass `-r win-x64` on the `build` step.** That flag triggers self-contained mode. Reserve it for `dotnet publish` in `release.yml`.

**Do not cache `obj/`** directories. WinUI 3 generates XAML code-behind, PRI resource tables, and WinMD files in `obj/` that are fast to regenerate and go stale when cached across SDK version changes.

**NuGet cache path on Windows runners:** `~/.nuget/packages` — GitHub Actions maps `~` correctly on Windows.

---

## Trigger Summary

| Workflow | Trigger |
|---|---|
| `ci.yml` | Push to `master`, PR targeting `master` |
| `release.yml` | Push tag matching `v*.*.*` |
| `codeql.yml` | Push to `master`, PR targeting `master`, weekly cron |
