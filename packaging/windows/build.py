#!/usr/bin/env python3
"""Build portable Windows RC from the exact macOS application source revision. No upload."""
import argparse, datetime, hashlib, json, os, pathlib, shutil, struct, subprocess, tempfile, zipfile
ROOT = pathlib.Path(__file__).resolve().parents[2]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def run(*args, **kw): subprocess.run(list(map(str,args)), check=True, **kw)
p=argparse.ArgumentParser()
p.add_argument('--mac-manifest',required=True,type=pathlib.Path)
a=p.parse_args()
mac=json.loads(a.mac_manifest.read_text())
assert (mac['product'],mac['version'],mac['releaseDesignation'],mac['rid'])==('FLOW','1.0.0','RC1','osx-arm64')
revision=subprocess.check_output(['git','rev-parse',mac['revision']+'^{commit}'],cwd=ROOT,text=True).strip()
assert revision==mac['revision']
name='FLOW-1.0.0-RC1-win-x64'
out=ROOT/'dist/windows'/name;out.mkdir(parents=True,exist_ok=False)
stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')
script_hash=sha(pathlib.Path(__file__))
with tempfile.TemporaryDirectory(prefix='flow-windows-') as temp:
    temp=pathlib.Path(temp).resolve();source=temp/'source';source.mkdir()
    archive=temp/'source.tar'
    with archive.open('wb') as f:run('git','archive',revision,cwd=ROOT,stdout=f)
    assert sha(archive)==mac['sourceArchiveSHA256'],'Source differs from macOS RC1; stop before packaging'
    run('tar','-xf',archive,'-C',source)
    # Wrap the existing original PNG in ICO format, without changing artwork.
    png=source/'packaging/macos/FLOW.iconset/icon_256x256.png'
    data=png.read_bytes();icon=temp/'FLOW.ico'
    icon.write_bytes(struct.pack('<HHH',0,1,1)+struct.pack('<BBBBHHII',0,0,0,0,1,32,len(data),22)+data)
    app=out/'FLOW';app.mkdir()
    with (out/'publish.log').open('w') as log:
        run('dotnet','publish',source/'src/Simulation.UI/Simulation.UI.csproj','-c','Release','-r','win-x64',
            '--self-contained','true','-p:PublishSingleFile=false','-p:PublishTrimmed=false','-p:DebugType=None',
            '-p:Version=1.0.0','-p:FlowReleaseDesignation=RC1','-p:FlowArchiveCommit='+revision,
            '-p:SourceRevisionId='+revision,'-p:FlowBuildUtc='+stamp,'-p:ApplicationIcon='+str(icon),
            '-p:BuildInParallel=false','-m:1','-nr:false','-o',app,
            env={**os.environ,'DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER':'1'},stdout=log,stderr=subprocess.STDOUT)
    # Apphost embeds Simulation.UI.dll. Preserve managed/runtime filenames and rename only the launcher.
    (app/'Simulation.UI.exe').rename(app/'FLOW.exe')
    assert b'Simulation.UI.dll' in (app/'FLOW.exe').read_bytes()
    runtime=json.loads((app/'Simulation.UI.runtimeconfig.json').read_text())['runtimeOptions']
    assert 'includedFrameworks' in runtime and 'frameworks' not in runtime
    assets=json.loads((source/'src/Simulation.UI/obj/project.assets.json').read_text())
    cache=pathlib.Path(next(iter(assets['packageFolders'])))
    notices=app/'ThirdPartyNotices';notices.mkdir();packages=[]
    for name_,meta in assets['libraries'].items():
        if meta.get('type')!='package':continue
        packages.append(name_);path=cache/meta['path'];target=notices/name_.replace('/','-')
        for f in path.rglob('*'):
            if f.is_file() and (f.name.lower().startswith(('license','notice','thirdparty')) or f.suffix=='.nuspec'):
                dest=target/f.relative_to(path);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(f,dest)
    (notices/'packages.txt').write_text('\n'.join(sorted(packages))+'\n')
    pack=cache/'microsoft.netcore.app.runtime.win-x64'/runtime['includedFrameworks'][0]['version']
    for filename in ['LICENSE.TXT','THIRD-PARTY-NOTICES.TXT']:
        dest=notices/'DotNetRuntime'/filename;dest.parent.mkdir(exist_ok=True);shutil.copyfile(pack/filename,dest)
    for doc in (source/'docs').rglob('README.md'):
        if doc.parent.name in ['game-view-b1-1','game-view-b1-2','game-view-b1-3']:shutil.copyfile(doc,notices/(doc.parent.name+'.md'))
    instructions=pathlib.Path(__file__).with_name('INSTALL.txt')
    shutil.copyfile(instructions,app/'READ-ME.txt')
    manifest={'product':'FLOW','version':'1.0.0','releaseDesignation':'RC1','revision':revision,'sourceState':'clean archive',
        'buildUtc':stamp,'rid':'win-x64','sdkVersion':subprocess.check_output(['dotnet','--version'],text=True).strip(),
        'runtimeFrameworks':runtime['includedFrameworks'],'selfContained':True,'publishSingleFile':False,'publishTrimmed':False,
        'signature':'unsigned (no Authenticode certificate)','nativeWindowsAcceptance':'not executed; required',
        'sourceArchiveSHA256':sha(archive),'macOSManifestSHA256':sha(a.mac_manifest),
        'packagingScriptSHA256':script_hash,'installationInstructionsSHA256':sha(instructions),
        'iconSource':'packaging/macos/FLOW.iconset/icon_256x256.png','iconSourceSHA256':sha(png),'generatedIconSHA256':sha(icon),
        'packagingMethod':'git archive of macOS RC1 revision; self-contained dotnet publish; apphost renamed to FLOW.exe; ZIP',
        'files':{str(f.relative_to(app)).replace(os.sep,'/'):sha(f) for f in sorted(app.rglob('*')) if f.is_file()}}
    (app/'release.json').write_text(json.dumps(manifest,indent=2)+'\n')
    artifact=out.parent/(name+'.zip')
    with zipfile.ZipFile(artifact,'x',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for f in sorted(app.rglob('*')):
            if f.is_file():z.write(f,'FLOW/'+f.relative_to(app).as_posix())
    manifest.update(artifact=artifact.name,artifactSHA256=sha(artifact),artifactBytes=artifact.stat().st_size)
    artifact.with_suffix('.manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    artifact.with_suffix('.zip.sha256').write_text(sha(artifact)+'  '+artifact.name+'\n')
    print(artifact)
