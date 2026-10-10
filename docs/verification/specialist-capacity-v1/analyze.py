"""Statistical summaries and publication-style figures; run after the C# harness."""
from pathlib import Path
import json, math, statistics as st
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from scipy.stats import t
P=Path(__file__).resolve().parent
raw=json.loads((P/'raw-results.json').read_text()); runs=raw['Runs']
assert len(runs)==210 and not raw['Failures']
assert {(r['Scenario'],r['Seed']) for r in runs}=={(s,k) for s in ['S0','S1','S2','S3','S4','S5','G6'] for k in range(12345,12375)}
assert len(raw['Repeats'])==21 and all(r['ExactState'] and r['ExactResult'] and r['ExactMetrics'] for r in raw['Repeats'])
# S1 must reproduce Scenario C for every seed, not just one representative run.
previous=json.loads((P.parent/'multi-seed-v1/raw-results.json').read_text())['Runs']
for old in previous:
    if old['Scenario']!='C':continue
    current=next(r for r in runs if r['Scenario']=='S1' and r['Seed']==old['Seed'])
    assert current['Fingerprint']==old['Fingerprint'] and current['ResultFingerprint']==old['ResultFingerprint']
    for m,v in old['Metrics'].items():
        assert current['Metrics'][m]==v,(old['Seed'],m)
variants=['S0','S1','S2','S3','S4','S5','G6']
metrics=list(runs[0]['Metrics'])
def quantile(x,p):
    x=sorted(x); h=(len(x)-1)*p; i=int(h); j=min(i+1,len(x)-1)
    return x[i]+(h-i)*(x[j]-x[i])
def stats(values):
    x=[v for v in values if v is not None]; n=len(x)
    assert all(math.isfinite(v) for v in x)
    if not n: return dict(N=0,Missing=len(values))
    mean=st.mean(x); sd=st.stdev(x) if n>1 else None; margin=float(t.ppf(.975,n-1))*sd/math.sqrt(n) if n>1 else None
    return dict(N=n,Missing=len(values)-n,Mean=mean,Median=st.median(x),SD=sd,Min=min(x),Max=max(x),P05=quantile(x,.05),P95=quantile(x,.95),CI95Low=mean-margin if margin is not None else None,CI95High=mean+margin if margin is not None else None)
# Independent known-value checks, including the percentile convention and paired SD.
assert quantile([0,10],.05)==.5 and quantile([0,10],.95)==9.5
assert stats([1,2,3])['SD']==1 and stats([2,2,2])['CI95Low']==2
assert abs(float(t.ppf(.975,29))-2.045229642)<1e-8
summary={s:{m:stats([r['Metrics'][m] for r in runs if r['Scenario']==s]) for m in metrics} for s in ['S0','S1','S2','S3','S4','S5','G6']}
by={(r['Scenario'],r['Seed']):r['Metrics'] for r in runs}; paired={}
for hi,lo in [(s,'S1') for s in variants if s!='S1']+[('G6','S2'),('S5','S2')]+[(f'S{i}',f'S{i-1}') for i in range(2,6)]:
    label=hi+' − '+lo; paired[label]={}
    for m in metrics:
        ds=[by[hi,seed][m]-by[lo,seed][m] for seed in range(12345,12375) if by[hi,seed][m] is not None and by[lo,seed][m] is not None]
        paired[label][m]={**stats(ds),'Increase':sum(v>1e-9 for v in ds),'Decrease':sum(v< -1e-9 for v in ds),'Unchanged':sum(abs(v)<=1e-9 for v in ds),'Unavailable':30-len(ds),'Differences':ds}
(P/'summary.json').write_text(json.dumps(summary,indent=2));(P/'paired.json').write_text(json.dumps(paired,indent=2))
def fmt(v): return 'N/A' if v is None else f'{v:.4f}' if isinstance(v,float) else str(v)
def table(headers,rows): return '| '+' | '.join(headers)+' |\n| '+' | '.join(['---']*len(headers))+' |\n'+'\n'.join('| '+' | '.join(map(fmt,row))+' |' for row in rows)+'\n'
(P/'scenario-summary.md').write_text('# Scenario statistics\n\n'+table(['Scenario','Metric','n','Mean','Median','Sample SD','Min','Max','P05','P95','Mean CI low','Mean CI high'],[[s,m]+[v.get(k) for k in ['N','Mean','Median','SD','Min','Max','P05','P95','CI95Low','CI95High']] for s,ms in summary.items() for m,v in ms.items()]))
(P/'paired-comparison.md').write_text('# Paired differences\n\n'+table(['Pair','Metric','n','Unavailable','Mean Δ','Median Δ','Sample SD Δ','Mean Δ CI low','Mean Δ CI high','Increase','Decrease','Unchanged'],[[p,m]+[v.get(k) for k in ['N','Unavailable','Mean','Median','SD','CI95Low','CI95High','Increase','Decrease','Unchanged']] for p,ms in paired.items() for m,v in ms.items()]))
series=json.loads((P/'time-series.json').read_text()); bands=[]
assert len(series)==210
for run in series:
    for points in run['Series'].values():assert [p['Day'] for p in points]==list(range(1,301))
for s in ['S0','S1','S2','S3','S4','S5','G6']:
    ss=[x for x in series if x['Scenario']==s]
    for m in ss[0]['Series']:
        for day in range(1,301):
            vals=[next((p['Value'] for p in x['Series'][m] if p['Day']==day),None) for x in ss]
            v=stats(vals);bands.append(dict(Scenario=s,Metric=m,Day=day,**v))
(P/'time-series-bands.json').write_text(json.dumps(bands,indent=2))
colors=dict(zip(variants,['#777777','#3366aa','#dd7722','#238b69','#9a448e','#aa9944','#228caa']))
plt.rcParams.update({'font.size':9,'axes.spines.top':False,'axes.spines.right':False})
fig,axs=plt.subplots(4,4,figsize=(17,14),layout='constrained')
for ax,m in zip(axs.flat,metrics[:16]):
    for i,s in enumerate(variants,1):
        vals=[r['Metrics'][m] for r in runs if r['Scenario']==s and r['Metrics'][m] is not None]
        if vals:
            ax.boxplot([vals],positions=[i],widths=.5,whis=(5,95),showfliers=False)
            ax.scatter([i+((j%7)-3)*.025 for j in range(len(vals))],vals,s=9,alpha=.6,color=colors[s])
        else:ax.text(i,.05,'N/A',transform=ax.get_xaxis_transform(),ha='center',fontsize=8)
    ax.set_xticks(range(1,8),variants);ax.set_xlim(.4,7.6);ax.set_title(m);ax.grid(axis='y',alpha=.2)
fig.suptitle('30 seeds per variant · Days 251–300; DebtRatio at Day 300\nBoxes Q1–Q3; whiskers within P05–P95; dots all runs; N/A means no completion/release cohort')
fig.savefig(P/'distributions.png',dpi=150);fig.savefig(P/'distributions.svg');plt.close(fig)
labels={'Throughput':'Released / 5 days (rolling 50 days)','CycleTime':'Delivery cycle days (rolling release cohort)','SpecialistWorkWaiting':'Specialist waiting (end of day)','TestingQueue':'Testing queue (end of day)'}
for number,group in enumerate([variants[:4],variants[4:]],1):
    fig,axs=plt.subplots(4,len(group),figsize=(5*len(group),13),layout='constrained',sharex=True,squeeze=False)
    for row,(m,label) in enumerate(labels.items()):
        all_bs=[b for b in bands if b['Metric']==m and b['N']]
        ymax=max(b['P95'] for b in all_bs);ymin=min(b['P05'] for b in all_bs)
        for col,s in enumerate(group):
            ax=axs[row,col];bs=[b for b in all_bs if b['Scenario']==s]
            ax.plot([b['Day'] for b in bs],[b['Median'] for b in bs],color=colors[s],lw=1.3)
            ax.fill_between([b['Day'] for b in bs],[b['P05'] for b in bs],[b['P95'] for b in bs],color=colors[s],alpha=.2)
            ax.set_ylim(min(0,ymin),ymax*1.08 if ymax else 1)
            ax.axvspan(251,300,color='grey',alpha=.08);ax.grid(alpha=.2);ax.set_title(f'{s} · {label}',fontsize=9)
            if row==3:ax.set_xlabel('Simulated day')
    fig.suptitle('Cross-seed median and empirical P05–P95 band (not mean CI)\nEarly rolling windows use available days; no-release cycle time remains missing; axes shared across both figures')
    fig.savefig(P/f'trajectories-{number}.png',dpi=150);fig.savefig(P/f'trajectories-{number}.svg');plt.close(fig)
# Quantify accumulation throughout the run using raw daily queues, not cumulative release counts.
regimes={}
for s in ['S0','S1','S2','S3','S4','S5','G6']:
    regimes[s]={}
    for m in series[0]['Series']:
        regimes[s][m]={}
        for first,last in [(1,50),(51,100),(101,150),(151,200),(201,250),(251,300)]:
            vs=[];slopes=[]
            for x in series:
                if x['Scenario']!=s:continue
                pts=[(p['Day'],p['Value']) for p in x['Series'][m] if first<=p['Day']<=last and p['Value'] is not None]
                if pts:
                    vs.append(st.mean(v for _,v in pts));mx=st.mean(d for d,_ in pts);my=st.mean(v for _,v in pts)
                    slopes.append(sum((d-mx)*(v-my) for d,v in pts)/sum((d-mx)**2 for d,_ in pts) if len(pts)>1 else 0)
            regimes[s][m][f'{first}-{last}']={'MeanAcrossSeedBlockMeans':stats(vs),'DailySlopeAcrossSeeds':stats(slopes)}
(P/'regimes.json').write_text(json.dumps(regimes,indent=2))
print('PASS 210 records, 21 repeats, S1/C exact equivalence, statistics and graphs.')
for pair in paired:
    print(pair)
    for m in ['Throughput','DeliveryCycleTime','SpecialistQueue','TestingQueue','DeveloperUtilization','DeliveryWorkCost','SystemCost']:
        print(m, {k:v for k,v in paired[pair][m].items() if k not in ['Differences','Min','Max','P05','P95']})
