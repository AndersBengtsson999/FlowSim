#!/usr/bin/env python3
"""Build a self-contained FLOW .app and drag-to-Applications DMG from an explicit git revision.
Does not modify the working tree, upload, notarize, or require an installed runtime on customer Macs.
"""
import argparse, datetime, hashlib, json, os, pathlib, plistlib, re, shutil, subprocess, tempfile, xml.etree.ElementTree as ET
ROOT=pathlib.Path(__file__).resolve().parents[2]
def run(*args,**kwargs):
    subprocess.run([str(a) for a in args],check=True,**kwargs)
p=argparse.ArgumentParser()
p.add_argument('--ref',required=True);p.add_argument('--version',required=True)
p.add_argument('--release',default='',help='Release designation, e.g. RC1; separate from public version')
p.add_argument('--rid',choices=['osx-arm64','osx-x64'],required=True)
p.add_argument('--identity',default='-',help='Developer ID Application identity; default is ad-hoc test signing')
a=p.parse_args()
revision=subprocess.check_output(['git','rev-parse',a.ref+'^{commit}'],cwd=ROOT,text=True).strip()
if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+',a.version):p.error('Use a numeric public version, e.g. 1.0.0')
if a.release and not re.fullmatch(r'[A-Za-z0-9]+',a.release):p.error('Release designation must be alphanumeric')
version_xml=subprocess.check_output(['git','show',revision+':ProductVersion.props'],cwd=ROOT)
if ET.fromstring(version_xml).findtext('.//FlowProductVersion')!=a.version:p.error('Version must match selected revision ProductVersion.props')
label='-'+a.release if a.release else ''
out=ROOT/'dist'/'macos'/f'FLOW-{a.version}{label}-{a.rid}';out.mkdir(parents=True,exist_ok=False)
build_utc=datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')
sdk=subprocess.check_output(['dotnet','--version'],cwd=ROOT,text=True).strip()
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
with tempfile.TemporaryDirectory(prefix='flow-macos-') as tmp:
    work=pathlib.Path(tmp).resolve();source=work/'source';source.mkdir()
    archive=work/'source.tar'
    with archive.open('wb') as stream:run('git','archive',revision,cwd=ROOT,stdout=stream)
    run('tar','-xf',archive,'-C',source)
    if (source/'packaging/macos/build.py').read_bytes()!=pathlib.Path(__file__).read_bytes():
        raise RuntimeError('Run the packaging script from the selected revision; refusing mixed provenance')
    resource_hashes={str(path):sha(source/path) for path in map(pathlib.Path,
        ['packaging/macos/FLOW.icns','packaging/macos/entitlements.plist','packaging/macos/build.py','ProductVersion.props'])}
    stage=work/'image';stage.mkdir();app=stage/'FLOW.app';mac=app/'Contents'/'MacOS';mac.mkdir(parents=True)
    resources=app/'Contents'/'Resources';resources.mkdir()
    with (out/'build.log').open('w') as log:
        run('dotnet','publish',source/'src/Simulation.UI/Simulation.UI.csproj','-c','Release','-r',a.rid,
            '--self-contained','true','-p:PublishTrimmed=false','-p:PublishSingleFile=false',
            '-p:BuildInParallel=false','-p:Version='+a.version,'-p:DebugType=None',
            '-p:FlowArchiveCommit='+revision,'-p:SourceRevisionId='+revision,
            '-p:FlowReleaseDesignation='+(a.release or 'Release'),'-p:FlowBuildUtc='+build_utc,'-m:1','-nr:false','-o',mac,
            env={**os.environ,'DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER':'1'},stdout=log,stderr=subprocess.STDOUT)
    shutil.copyfile(source/'packaging/macos/FLOW.icns',resources/'FLOW.icns')
    info={'CFBundleName':'FLOW','CFBundleDisplayName':'FLOW','CFBundleExecutable':'Simulation.UI',
          'CFBundleIdentifier':'com.andersbengtsson.flowsim','CFBundlePackageType':'APPL',
          'CFBundleShortVersionString':a.version,'CFBundleVersion':a.version,'FlowReleaseDesignation':a.release or 'Release',
          'FlowSourceRevision':revision,'FlowBuildUtc':build_utc,
          'CFBundleIconFile':'FLOW.icns','NSHighResolutionCapable':True,'LSMinimumSystemVersion':'15.0',
          'NSPrincipalClass':'NSApplication'}
    with (app/'Contents/Info.plist').open('wb') as f:plistlib.dump(info,f)
    # Preserve redistributable runtime notices; add package provenance for libraries and original artwork.
    notices=resources/'ThirdPartyNotices';notices.mkdir()
    assets=json.loads((source/'src/Simulation.UI/obj/project.assets.json').read_text())
    packages=[]
    cache=pathlib.Path.home()/'.nuget/packages'
    for name,meta in assets['libraries'].items():
        if meta.get('type')!='package':continue
        path=cache/meta['path'];packages.append(name)
        target=notices/name.replace('/','-');target.mkdir()
        for f in path.rglob('*'):
            if f.is_file() and (f.name.lower().startswith(('license','notice','thirdparty')) or f.suffix=='.nuspec'):
                relative=f.relative_to(path);dest=target/relative;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(f,dest)
    (notices/'packages.txt').write_text('\n'.join(sorted(packages))+'\n')
    # Include the repository's original sprite provenance in the distributable app.
    for doc in (source/'docs').rglob('*.md'):
        if doc.name=='README.md' and doc.parent.name in ['game-view-b1-1','game-view-b1-2','game-view-b1-3']:
            shutil.copyfile(doc,notices/(doc.parent.name+'.md'))
    runtime_config=json.loads((mac/'Simulation.UI.runtimeconfig.json').read_text())
    # Runtime packs are SDK download dependencies, not necessarily project.assets libraries.
    runtime_notices = notices/'DotNetRuntime';runtime_notices.mkdir()
    runtime_version=runtime_config['runtimeOptions']['includedFrameworks'][0]['version']
    runtime_pack=cache/('microsoft.netcore.app.runtime.'+a.rid)/runtime_version
    runtime_notice_hashes={}
    for name in ['LICENSE.TXT','THIRD-PARTY-NOTICES.TXT']:
        origin=runtime_pack/name
        if not origin.is_file():raise RuntimeError('Missing runtime redistribution notice: '+str(origin))
        shutil.copyfile(origin,runtime_notices/name)
        runtime_notice_hashes[name]=sha(origin)
    stamp={'product':'FLOW','version':a.version,'releaseDesignation':a.release or 'Release',
           'revision':revision,'sourceState':'clean archive','buildUtc':build_utc,'rid':a.rid,
           'sdkVersion':sdk,'runtimeFrameworks':runtime_config['runtimeOptions']['includedFrameworks'],
           'targetFramework':runtime_config['runtimeOptions']['tfm'],'minimumMacOS':'15.0',
           'packagingMethod':'git archive; dotnet publish self-contained; signed app bundle; hdiutil UDZO DMG',
           'runtimeNoticeSHA256':runtime_notice_hashes,'sourceArchiveSHA256':sha(archive),'packagingResourceSHA256':resource_hashes,'selfContained':True,
           'signature':'ad-hoc testing only' if a.identity=='-' else 'Developer ID Application',
           'notarized':False,'distribution':'controlled internal testing'}
    (resources/'release.json').write_text(json.dumps(stamp,indent=2)+'\n')
    # Keep managed assemblies/data in Resources and native libraries in Frameworks.
    # Relative links preserve .NET's app-local probing without treating PE DLLs as macOS nested code.
    runtime=resources/'runtime';runtime.mkdir()
    frameworks=app/'Contents'/'Frameworks';frameworks.mkdir()
    for item in list(mac.iterdir()):
        if item.name=='Simulation.UI':continue
        native=item.is_file() and 'Mach-O' in subprocess.check_output(['file','-b',str(item)],text=True)
        destination=(frameworks if native else runtime)/item.name
        shutil.move(str(item),str(destination))
        item.symlink_to(os.path.relpath(destination,mac),target_is_directory=destination.is_dir())
    # The apphost resolves managed entry-point symlinks before locating its runtime.
    for native in frameworks.iterdir():
        (runtime/native.name).symlink_to(os.path.relpath(native,runtime))
    sign=['codesign','--force','--sign',a.identity]
    if a.identity!='-':sign+=['--timestamp','--options=runtime']
    entitlements=source/'packaging/macos/entitlements.plist'
    # Sign each Mach-O object, then the outer bundle; never use --deep for signing.
    for binary in sorted(frameworks.rglob('*')):
        if binary.is_file() and 'Mach-O' in subprocess.check_output(['file','-b',str(binary)],text=True):
            run(*sign,'--entitlements',entitlements,binary)
    run(*sign,'--entitlements',entitlements,app)
    run('codesign','--verify','--deep','--strict','--verbose=2',app)
    (stage/'Applications').symlink_to('/Applications')
    instructions='FLOW '+a.version+' '+a.release+' — '+a.rid+'\n\nDra FLOW.app till Applications (Program). .NET ingår; ingen separat installation behövs.\nKrav: macOS 15 eller senare. Välj arm64 för Apple Silicon, x64 för Intel.\n'
    if a.identity=='-':instructions+='\nTESTPAKET: saknar Developer ID-signering och Apple-notariering. Inte redo för allmän publicering. macOS kan blockera nedladdade kopior.\n'
    (stage/'Läs mig.txt').write_text(instructions)
    shutil.copytree(app,out/'FLOW.app',symlinks=True)
    dmg=out.parent/(out.name+('-TEST' if a.identity=='-' else '')+'.dmg')
    run('hdiutil','create','-volname','FLOW '+a.version+(' '+a.release if a.release else ''),'-srcfolder',stage,'-format','UDZO','-ov',dmg)
    run('hdiutil','verify',dmg)
    digest=hashlib.sha256(dmg.read_bytes()).hexdigest()
    (out.parent/(dmg.name+'.sha256')).write_text(digest+'  '+dmg.name+'\n')
    (out/'release.json').write_text(json.dumps(stamp,indent=2)+'\n')
    final_manifest={**stamp,'artifact':dmg.name,'artifactSHA256':digest,'artifactBytes':dmg.stat().st_size,
                    'appExecutableSHA256':sha(out/'FLOW.app/Contents/MacOS/Simulation.UI'),
                    'uiAssemblySHA256':sha(out/'FLOW.app/Contents/Resources/runtime/Simulation.UI.dll')}
    (out.parent/(dmg.stem+'.manifest.json')).write_text(json.dumps(final_manifest,indent=2)+'\n')
    print(dmg)
