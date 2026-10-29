# FLOW Windows RC1 packaging

Portable Windows x64 ZIP; no installer, upload or runtime code changes. The application must come from the exact source archive recorded by the existing Apple Silicon RC1 manifest. The new packaging tools are separate release tooling and are identified by SHA-256 in the release manifest. They do not rewrite the archived application source.

Build from the repository root (Python 3 and .NET 10 SDK, Git and tar):

```sh
python3 packaging/windows/build.py --mac-manifest dist/macos/FLOW-1.0.0-RC1-osx-arm64-TEST.manifest.json
```

The build refuses a differing source archive and existing output directory. It publishes self-contained win-x64, supplies the archived FLOW artwork as a PNG-backed ICO via the ApplicationIcon build property, and renames only the native apphost to FLOW.exe. The apphost still loads Simulation.UI.dll; do not rename the managed DLL, deps or runtimeconfig files. Runtime license/notices, package provenance and original sprite provenance are included. Final ZIP checksums cover the completed, unsigned artifact. Build timestamps mean repeated builds are not byte-identical; source provenance and final file hashes are recorded.

Static verification on an Apple Silicon development Mac uses an x64 managed resource probe under Rosetta, not Windows execution:

```sh
dotnet publish packaging/windows/Probe/Probe.csproj -c Release -r osx-x64 --self-contained true -o /tmp/flow-windows-resource-probe
python3 packaging/windows/verify.py dist/windows/FLOW-1.0.0-RC1-win-x64.zip --probe /tmp/flow-windows-resource-probe/Probe --mac-manifest dist/macos/FLOW-1.0.0-RC1-osx-arm64-TEST.manifest.json
```

On Windows publish the probe for win-x64 and pass its Probe.exe instead. The probe inspects actual packaged assemblies, About metadata, guide links/images and sprite resources. It never starts the application. The verifier checks ZIP CRC/hash and every payload hash, case collisions, excluded development artifacts, x64 GUI PE apphost, icon, absent Authenticode certificate, app-local runtime configuration and required native libraries.

Outputs are ignored under dist/windows. INSTALL.txt is included as READ-ME.txt. A real Windows PC/VM must complete the acceptance checklist supplied with the release verification report before controlled distribution. No Windows runtime acceptance was performed on the macOS build host.

Application source for this RC1: e1c36de0790405c68e6fa8a9e047727335517018, matching macOS. Packaging development initially hit an Avalonia file sharing exception due to /var versus /private/var aliases; canonicalizing the temporary directory, as the macOS workflow already does, corrected the build. Failed logs remain in local release evidence.
