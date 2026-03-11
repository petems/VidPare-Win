# MSIX Packaging Plan for VidPare

## Part 1: Changes to `VidPare.App.csproj`

**Remove** `<WindowsPackageType>None</WindowsPackageType>` — that's the only thing blocking MSIX.

**Add** these properties to the main `PropertyGroup`:
```xml
<ApplicationId>com.vidpare.app</ApplicationId>
<ApplicationDisplayVersion>1.0.0</ApplicationDisplayVersion>
<ApplicationVersion>1.0.0.0</ApplicationVersion>
<AppxPackageSigningEnabled>true</AppxPackageSigningEnabled>
<PackageCertificateThumbprint>YOUR_CERT_THUMBPRINT</PackageCertificateThumbprint>
```

> `ApplicationId` is permanent — changing it breaks updates and Store identity. Choose it now.

**Important:** `dotnet build` does NOT produce a `.msix` file. You need `msbuild /t:Pack` for a distributable package:
```batch
msbuild VidPare.App\VidPare.App.csproj /t:Pack /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64
```
Update `build.bat` to support a `/pack` mode alongside the existing dev build.

---

## Part 2: `Package.appxmanifest` (new file in `VidPare.App\`)

Key sections you need:

**Identity** — the `Publisher` value must be a character-for-character match with your certificate's Subject DN:
```xml
<Identity Name="com.vidpare.app"
  Publisher="CN=YourName, O=YourOrg, C=US"
  Version="1.0.0.0"
  ProcessorArchitecture="x64" />
```

**Dependencies** — lock to Windows 11:
```xml
<TargetDeviceFamily Name="Windows.Desktop"
  MinVersion="10.0.22000.0" MaxVersionTested="10.0.22631.0" />
```

**Capabilities** — `runFullTrust` is mandatory for all WinUI 3 / Windows App SDK apps. Without it the app installs but refuses to launch:
```xml
<Capabilities>
  <rescap:Capability Name="runFullTrust" />
</Capabilities>
```

**Assets** — the manifest requires image assets at specific sizes. Visual Studio generates placeholder PNGs automatically when you add a packaging project. Minimum set:

| File | Size |
|---|---|
| `Assets\Square44x44Logo.png` | 44×44 |
| `Assets\Square150x150Logo.png` | 150×150 |
| `Assets\Wide310x150Logo.png` | 310×150 |
| `Assets\StoreLogo.png` | 50×50 |
| `Assets\SplashScreen.png` | 620×300 |

---

## Part 3: Code Signing — Options & Tradeoffs

### Option A: Self-Signed Certificate (dev/testing only)

Free and instant. Not trusted by other machines without manual setup — not suitable for public distribution.

```powershell
# Create the cert (Subject must match manifest Publisher exactly)
$cert = New-SelfSignedCertificate `
  -Type Custom `
  -Subject "CN=VidPare Dev, O=VidPare, C=US" `
  -KeyUsage DigitalSignature `
  -FriendlyName "VidPare Dev Signing" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Export to PFX for build tooling
Export-PfxCertificate -cert $cert -FilePath VidPare.pfx `
  -Password (ConvertTo-SecureString "yourpassword" -Force -AsPlainText)

# Get thumbprint for csproj
$cert.Thumbprint
```

To install on a test machine: right-click the `.msix` > Properties > Digital Signatures > View Certificate > Install to **Local Machine > Trusted Root Certification Authorities** (requires admin), then double-click the `.msix`.

> Never commit the `.pfx` to git. Add `*.pfx` to `.gitignore`.

### Option B: Commercial Code Signing Certificate (public distribution)

Since June 2023, all OV and EV certificates must be stored on a hardware token or cloud HSM — no software PFX for public certs anymore. EV certs are trusted by Windows SmartScreen immediately; OV certs build reputation over time.

| Vendor | OV (~annual) | EV (~annual) |
|---|---|---|
| SSL.com | ~$170 | ~$300 |
| Sectigo | ~$200 | ~$350 |
| DigiCert | ~$500 | ~$700 |

For CI/CD with a commercial cert, use **Azure Key Vault** (store the cert there, sign with `AzureSignTool` in the pipeline — no physical USB token in CI).

### Option C: Microsoft Store (Microsoft signs for you)

- One-time $19 developer registration.
- Microsoft assigns you a `Publisher` identity — you must update the manifest to match it.
- You sign your upload with any cert (even self-signed), Microsoft re-signs before delivery.
- Users see a Microsoft-signed package; no SmartScreen issues.
- Downside: 1–5 day certification review, Store policies apply, 15% revenue share on paid apps.

---

## Part 4: GitHub Actions CI

A `windows-latest` runner has VS2022 and MSBuild available. Key steps:

```yaml
- name: Import signing cert
  shell: pwsh
  run: |
    $bytes = [Convert]::FromBase64String("${{ secrets.SIGNING_CERT_BASE64 }}")
    $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($bytes, "${{ secrets.CERT_PASSWORD }}")
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new("My", "CurrentUser")
    $store.Open("ReadWrite"); $store.Add($cert); $store.Close()

- name: Build MSIX
  shell: pwsh
  run: |
    msbuild VidPare.App\VidPare.App.csproj /t:Pack `
      /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 `
      /p:AppxPackageSigningEnabled=true `
      /p:PackageCertificateThumbprint=${{ secrets.CERT_THUMBPRINT }}
```

Required GitHub secrets: `SIGNING_CERT_BASE64`, `CERT_PASSWORD`, `CERT_THUMBPRINT`. Output lands in `VidPare.App\bin\x64\Release\AppPackages\`.

---

## Part 5: WinUI 3 MSIX Pitfalls

1. **`runFullTrust` is not optional** — app silently refuses to launch without it.
2. **Publisher string must be exact** — character-for-character match between cert Subject and manifest. Get it via `$cert.Subject` in PowerShell.
3. **`dotnet build` ≠ `msbuild /t:Pack`** — only the latter produces an installable `.msix`.
4. **Windows App SDK runtime dependency** — by default the runtime is NOT bundled. On a clean machine without the Store, install will succeed but launch will fail. Fix: add `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` to bundle it (adds ~50–100 MB), or distribute via the Store which handles this automatically.
5. **Test `VideoEngine.ExportAsync` under MSIX** — `MediaTranscoder` and `MediaComposition` work correctly under real package identity, but verify output file paths work since file system access rules change subtly under MSIX.

---

## Recommended Order

1. Add placeholder `Assets\` images + `Package.appxmanifest`
2. Remove `WindowsPackageType=None`, add packaging properties to csproj
3. Create self-signed cert locally, verify install + launch from `.msix`
4. Test the full export pipeline under MSIX
5. Update `build.bat` with a `/pack` mode
6. Add GitHub Actions CI workflow
7. Choose commercial cert vs Store when ready to distribute publicly
