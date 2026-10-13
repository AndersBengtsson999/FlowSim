"""Losslessly compact item-level daily histories; optionally follow a running harness log."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import gzip,lzma,hashlib,json,sys,time,re
P=Path(__file__).resolve().parent
follow=Path(sys.argv[2]) if len(sys.argv)>2 and sys.argv[1]=='--follow' else None
records={}
def compact(key):
    src=P/'histories'/f'{key}.json.gz';dst=src.with_suffix('.xz')
    if not src.exists():return key,None
    raw=gzip.decompress(src.read_bytes());digest=hashlib.sha256(raw).hexdigest()
    compressed=lzma.compress(raw,preset=1)
    assert hashlib.sha256(lzma.decompress(compressed)).hexdigest()==digest
    temporary=dst.with_suffix(dst.suffix+'.tmp');temporary.write_bytes(compressed);temporary.replace(dst)
    result=dict(SHA256Uncompressed=digest,UncompressedBytes=len(raw),GzipBytes=src.stat().st_size,XzBytes=len(compressed))
    src.unlink()
    return key,result
while True:
    if follow:
        log=follow.read_text();keys=[f'{s}-{seed}' for s,seed in re.findall(r'^PASS (T\d-R\w+) seed (\d+)$',log,re.M)]
        if 'FAIL ' in log:raise RuntimeError('Harness failure; inspect log')
    else:keys=[p.name.removesuffix('.json.gz') for p in (P/'histories').glob('*.json.gz')]
    todo=[k for k in keys if (P/'histories'/f'{k}.json.gz').exists()]
    with ThreadPoolExecutor(max_workers=2) as pool:
        for key,result in pool.map(compact,todo):
            if result:records[key]=result
    if records:
        manifest=P/'history-archive.json'
        old=json.loads(manifest.read_text()) if manifest.exists() else {};old.update(records)
        manifest.write_text(json.dumps(old,indent=2));records={}
    count=len(list((P/'histories').glob('*.json.xz')))
    print(f'Archived {count}/360 histories',flush=True)
    if not follow or count==360:break
    time.sleep(5)
