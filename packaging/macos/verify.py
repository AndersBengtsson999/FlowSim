#!/usr/bin/env python3
"""Verify a local DMG and smoke-launch its app for eight seconds, then close only that process."""
import pathlib,plistlib,subprocess,os,sys,json,hashlib
image=pathlib.Path(sys.argv[1]).resolve()
logdir=image.parent/(image.stem+'-verification');logdir.mkdir(exist_ok=True)
expected=image.with_suffix(image.suffix+'.sha256').read_text().split()[0]
assert hashlib.sha256(image.read_bytes()).hexdigest()==expected,'SHA-256 sidecar mismatch'
subprocess.run(['hdiutil','verify',str(image)],check=True,stdout=subprocess.DEVNULL)
mounts=plistlib.loads(subprocess.check_output(['hdiutil','attach','-readonly','-nobrowse','-plist',str(image)]))
mount=pathlib.Path(next(e['mount-point'] for e in mounts['system-entities'] if 'mount-point' in e))
try:
 app=mount/'FLOW.app';executable=app/'Contents/MacOS/Simulation.UI'
 subprocess.run(['codesign','--verify','--deep','--strict',str(app)],check=True)
 assert (mount/'Applications').readlink()==pathlib.Path('/Applications')
 assert all(p.exists() for p in app.rglob('*') if p.is_symlink())
 config=json.loads((app/'Contents/MacOS/Simulation.UI.runtimeconfig.json').read_text())
 assert 'includedFrameworks' in config['runtimeOptions'] and 'frameworks' not in config['runtimeOptions']
 metadata=json.loads((app/'Contents/Resources/release.json').read_text())
 info=plistlib.loads((app/'Contents/Info.plist').read_bytes())
 assert info['CFBundleName']=='FLOW' and info['CFBundleShortVersionString']==metadata['version']
 assert not list(app.rglob('*.pdb')) and not list(app.rglob('*.cs')),'Unexpected debug/source artefacts'
 assert (app/'Contents/Resources/FLOW.icns').stat().st_size>0
 notices=app/'Contents/Resources/ThirdPartyNotices'
 assert (notices/'packages.txt').stat().st_size>0
 for name in ['LICENSE.TXT','THIRD-PARTY-NOTICES.TXT']:
  notice=notices/'DotNetRuntime'/name
  assert notice.stat().st_size>0,'Runtime notice absent: '+name
  assert hashlib.sha256(notice.read_bytes()).hexdigest()==metadata['runtimeNoticeSHA256'][name]
 if 'releaseDesignation' in metadata:
  assert info['FlowReleaseDesignation']==metadata['releaseDesignation']
  assert info['FlowSourceRevision']==metadata['revision'] and info['FlowBuildUtc']==metadata['buildUtc']
  assert config['runtimeOptions']['includedFrameworks']==metadata['runtimeFrameworks']
  external=json.loads(image.with_suffix('.manifest.json').read_text())
  assert external['artifactSHA256']==expected and external['revision']==metadata['revision']
  assert hashlib.sha256((app/'Contents/Resources/FLOW.icns').read_bytes()).hexdigest()==metadata['packagingResourceSHA256']['packaging/macos/FLOW.icns']
  architecture=subprocess.check_output(['lipo','-archs',str(executable)],text=True).strip()
  assert architecture==('arm64' if metadata['rid']=='osx-arm64' else 'x86_64')
  probe=pathlib.Path(__file__).resolve().parent/'Probe/bin/Release/net10.0/Probe.dll'
  with (logdir/'packaged-resources.json').open('w') as log:
   probe_command=[os.environ['FLOW_PROBE_EXECUTABLE']] if os.environ.get('FLOW_PROBE_EXECUTABLE') else ['dotnet',str(probe)]
   subprocess.run([*probe_command,str(app)],stdout=log,check=True)
  metadata['packagedResourcesAndIdentityVerified']=True
  metadata['executionMode']='native arm64' if metadata['rid']=='osx-arm64' else 'x64 via Rosetta on arm64'

 env={**os.environ,'DOTNET_ROOT':'/tmp/flow-no-installed-runtime','DOTNET_ROOT_ARM64':'/tmp/flow-no-installed-runtime','DOTNET_ROOT_X64':'/tmp/flow-no-installed-runtime',
      'COREHOST_TRACE':'1','COREHOST_TRACEFILE':str(logdir/'runtime.log')}
 with (logdir/'startup.log').open('w') as log:
  process=subprocess.Popen([str(executable)],cwd='/tmp',env=env,stdout=log,stderr=log)
  try:
   code=process.wait(timeout=8)
   raise RuntimeError(f'App exited early: {code}; inspect startup.log')
  except subprocess.TimeoutExpired:
   process.terminate();process.wait(timeout=10)
 metadata.update({'dmgSHA256':hashlib.sha256(image.read_bytes()).hexdigest(),'bundleVerified':True,'selfContained':True,'startupSeconds':8,
                  'nativeIntelHardwareTested':False,'gatekeeperNotarizationTested':False})
 (logdir/'verification.json').write_text(json.dumps(metadata,indent=2)+'\n')
 print('PASS:',image.name,'— mounted app, bundle signature, runtime, Applications link, and eight-second startup.')
finally:
 subprocess.run(['hdiutil','detach',str(mount)],check=True,stdout=subprocess.DEVNULL)
