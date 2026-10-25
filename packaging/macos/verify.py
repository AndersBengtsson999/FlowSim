#!/usr/bin/env python3
"""Verify a local DMG and smoke-launch its app for eight seconds, then close only that process."""
import pathlib,plistlib,subprocess,os,sys,json,hashlib
image=pathlib.Path(sys.argv[1]).resolve()
logdir=image.parent/(image.stem+'-verification');logdir.mkdir(exist_ok=True)
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
