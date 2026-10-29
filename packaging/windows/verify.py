#!/usr/bin/env python3
"""Static package/resource verification only. Never claims Windows runtime acceptance."""
import argparse,hashlib,json,pathlib,struct,subprocess,tempfile,zipfile
p=argparse.ArgumentParser();p.add_argument('zip',type=pathlib.Path);p.add_argument('--probe',required=True);p.add_argument('--mac-manifest',required=True,type=pathlib.Path);a=p.parse_args()
def sha(b):return hashlib.sha256(b).hexdigest()
manifest=json.loads(a.zip.with_suffix('.manifest.json').read_text());mac=json.loads(a.mac_manifest.read_text())
assert manifest['revision']==mac['revision'] and manifest['sourceArchiveSHA256']==mac['sourceArchiveSHA256']
assert manifest['artifactSHA256']==sha(a.zip.read_bytes())==a.zip.with_suffix('.zip.sha256').read_text().split()[0]
assert (manifest['version'],manifest['releaseDesignation'],manifest['rid'])==('1.0.0','RC1','win-x64')
out=a.zip.parent/(a.zip.stem+'-verification');out.mkdir(exist_ok=True)
with tempfile.TemporaryDirectory(prefix='flow-win-verify-') as temp:
    with zipfile.ZipFile(a.zip) as z:
        assert z.testzip() is None
        names=z.namelist();assert len(names)==len(set(n.casefold() for n in names)), 'Case collision'
        assert all(n.startswith('FLOW/') and '..' not in pathlib.PurePosixPath(n).parts and '\\' not in n for n in names)
        assert not any(n.endswith(('.pdb','.cs','.log','.dylib','.so')) for n in names)
        assert set(names)=={'FLOW/'+f for f in manifest['files']}|{'FLOW/release.json'}
        z.extractall(temp)
    app=pathlib.Path(temp)/'FLOW';inside=json.loads((app/'release.json').read_text())
    assert all(manifest[k]==v for k,v in inside.items())
    for name,digest in manifest['files'].items():assert sha((app/name).read_bytes())==digest,name
    for name in ['FLOW.exe','Simulation.UI.dll','Simulation.Sprites.dll','Avalonia.Win32.dll','Avalonia.Fonts.Inter.dll','coreclr.dll','hostfxr.dll','hostpolicy.dll','System.Private.CoreLib.dll','libSkiaSharp.dll','libHarfBuzzSharp.dll','ThirdPartyNotices/DotNetRuntime/LICENSE.TXT','ThirdPartyNotices/DotNetRuntime/THIRD-PARTY-NOTICES.TXT']:
        assert (app/name).stat().st_size>0,name
    exe=(app/'FLOW.exe').read_bytes();pe=struct.unpack_from('<I',exe,60)[0];assert exe[pe:pe+4]==b'PE\0\0'
    assert struct.unpack_from('<H',exe,pe+4)[0]==0x8664,'Not x64'
    opt=pe+24;assert struct.unpack_from('<H',exe,opt)[0]==0x20b
    assert struct.unpack_from('<H',exe,opt+68)[0]==2,'Not Windows GUI app'
    assert struct.unpack_from('<II',exe,opt+112+4*8)==(0,0),'Unexpected Authenticode certificate'
    assert b'Simulation.UI.dll' in exe,'Apphost target missing'
    assert b'\x89PNG\r\n\x1a\n' in exe,'Application icon missing'
    assert 'FLOW'.encode('utf-16-le') in exe,'Product metadata missing'
    runtime=json.loads((app/'Simulation.UI.runtimeconfig.json').read_text())['runtimeOptions']
    assert runtime['includedFrameworks']==manifest['runtimeFrameworks'] and 'frameworks' not in runtime
    with (out/'packaged-resources.json').open('w') as log:subprocess.run([a.probe,str(app)],check=True,stdout=log)
    resources=json.loads((out/'packaged-resources.json').read_text())
    assert resources['metadata']['FlowArchitecture']=='win-x64'
    (out/'verification.json').write_text(json.dumps({'zipIntegrity':True,'allFileHashes':True,'sourceMatchesMacOS':True,'x64GuiApphost':True,'iconEmbedded':True,'authenticodeSigned':False,'selfContainedConfiguration':True,'resourceProbe':True,'nativeWindowsExecuted':False,'status':'READY AFTER WINDOWS ACCEPTANCE'},indent=2)+'\n')
print('PASS: static ZIP, identity, resources and self-contained checks. Windows execution NOT tested.')
