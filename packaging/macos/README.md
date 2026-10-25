# FLOW for macOS

Build a self-contained `.app` and a drag-to-Applications DMG from a specific committed revision. The source is exported into a separate temporary directory; uncommitted changes are never included. The runtime, offline help and sprites are bundled. No administrator installer or separate .NET installation is required.

```sh
python3 packaging/macos/build.py --ref a5105ca --version 6.6.1 --rid osx-arm64
python3 packaging/macos/build.py --ref a5105ca --version 6.6.1 --rid osx-x64
```

Outputs are in `dist/macos/`. An existing output folder is not overwritten. `osx-arm64` is Apple Silicon; `osx-x64` is Intel. Minimum declared macOS version: 15.0. The packaging version `6.6.1` identifies milestone B6.6.1, not the simulation model/save-format version. Build scripts require macOS, Python 3, .NET 10 SDK and the standard Apple command-line utilities. FLOW.icns is committed/generated artwork and does not require Pillow to build an app.

## Signing and website release

The default is **ad-hoc test signing**, explicitly named `-TEST.dmg`. It verifies bundle integrity locally but does not establish a Developer ID identity or Apple notarization. Do not describe it as signed/notarized for public website distribution. Nothing is uploaded automatically.

With a valid Developer ID Application certificate installed, build a fresh output with:

```sh
python3 packaging/macos/build.py --ref COMMIT --version VERSION --rid osx-arm64 \
  --identity 'Developer ID Application: YOUR NAME (TEAMID)'
```

The script signs native components individually, then the app bundle, with hardened runtime and the JIT entitlement when using Developer ID. It does not grant Apple Events, unsigned executable memory or disabled library-validation permissions.

Before publishing, notarize the resulting DMG using an existing authorized Keychain credential profile, staple the ticket, verify Gatekeeper acceptance and recalculate the SHA-256 after stapling:

```sh
xcrun notarytool submit '/path/to/FLOW.dmg' --keychain-profile 'YOUR_PROFILE' --wait
xcrun stapler staple '/path/to/FLOW.dmg'
xcrun stapler validate '/path/to/FLOW.dmg'
spctl --assess --type open --context context:primary-signature --verbose '/path/to/FLOW.dmg'
shasum -a 256 '/path/to/FLOW.dmg'
```

Use the Apple notarization result as the authority for release status; `release.json` records build-time status and is intentionally `notarized: false`. It is inside the signed app and must not be edited after signing. Test a downloaded, quarantined copy on a clean Mac before public launch. Intel binaries can be built on Apple Silicon, but native Intel acceptance needs an Intel Mac.

References: [Avalonia macOS packaging](https://docs.avaloniaui.net/docs/deployment/macos), [Apple Developer ID](https://developer.apple.com/developer-id/), [.NET 10 supported OS versions](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

## Included material

`Contents/Resources/release.json` records exact source commit, milestone version, architecture and build-time signing status. ThirdPartyNotices collects shipped package metadata and available license/notice files, runtime notices remain in the runtime folder, and original sprite provenance is retained. The icon is an original typographic FLOW mark using the existing dark/blue palette; no external image asset was introduced. The source icon PNGs are in `FLOW.iconset/`.

## Verified build — 2026-10-25

B6.6.1 from commit `a5105caf4cba8ffd1f5cf314097af1fc874c1e2d`, .NET runtime 10.0.12, separate arm64/x64 DMGs. Source milestone already passed 945 tests; no simulation code was changed for packaging. Offline guide loading from the packaged assembly was verified (20 embedded resources).

`verify.py PATH_TO_DMG` mounts the image read-only, verifies checksum and bundle signature, checks relative links and the self-contained runtime configuration, then starts the packaged executable for eight seconds with invalid external DOTNET_ROOT paths. It stops only its own test process and unmounts the image. Runtime traces and a verification manifest are retained beside each DMG.

Apple Silicon startup was verified on this Mac; the Intel build was tested through installed Rosetta, not on native Intel hardware. This is a launch smoke test, not full UI regression or notarization acceptance. No valid Developer ID identity was available, so both artifacts remain explicitly labelled TEST and unnotarized. No package was uploaded to a website or Apple.
