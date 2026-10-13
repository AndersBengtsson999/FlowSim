"""Statistical summaries and publication-style figures; run after the C# harness."""
from pathlib import Path
import json, math, statistics as st
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from scipy.stats import t
P=Path(__file__).resolve().parent
raw=json.loads((P/'raw-results.json').read_text()); runs=raw['Runs']
assert len(runs)==360 and not raw['Failures']
assert {(r['Scenario'],r['Seed']) for r in runs}=={(s,k) for s in [f'T{t}-R{r}' for t in [2,3,4] for r in ['5','7','10','U']] for k in range(12345,12375)}
assert len(raw['Repeats'])==36 and all(r['ExactState'] and r['ExactResult'] and r['ExactMetrics'] for r in raw['Repeats'])
variants=[f'T{t}-R{r}' for t in [2,3,4] for r in ['5','7','10','U']]
# Every request is identical except the two factors and the paired random seed.
configs=json.loads((P/'configurations.json').read_text())
reference=json.loads(json.dumps(configs[0]['Request']))
def fixed_request(q):
    q=json.loads(json.dumps(q));q.pop('TesterCount');q.pop('RandomSeed');q['Release'].pop('Capacity');q['Release'].pop('Description');return q
assert all(fixed_request(r['Request'])==fixed_request(reference) for r in runs)
assert all(r['Request']['SimulationDays']==500 and not r['Changes'] for r in runs)
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
summary={s:{m:stats([r['Metrics'][m] for r in runs if r['Scenario']==s]) for m in metrics} for s in [f'T{t}-R{r}' for t in [2,3,4] for r in ['5','7','10','U']]}
by={(r['Scenario'],r['Seed']):r['Metrics'] for r in runs}; paired={}
from itertools import combinations
pairs=[(f'T{hi}-R{r}',f'T{lo}-R{r}') for r in ['5','7','10','U'] for lo,hi in combinations([2,3,4],2)]+[(f'T{testers}-R{hi}',f'T{testers}-R{lo}') for testers in [2,3,4] for lo,hi in combinations(['5','7','10','U'],2)]
for hi,lo in pairs:
    label=hi+' − '+lo; paired[label]={}
    for m in metrics:
        ds=[by[hi,seed][m]-by[lo,seed][m] for seed in range(12345,12375) if by[hi,seed][m] is not None and by[lo,seed][m] is not None]
        paired[label][m]={**stats(ds),'Increase':sum(v>1e-9 for v in ds),'Decrease':sum(v< -1e-9 for v in ds),'Unchanged':sum(abs(v)<=1e-9 for v in ds),'Unavailable':30-len(ds),'Differences':ds}
(P/'summary.json').write_text(json.dumps(summary,indent=2));(P/'paired.json').write_text(json.dumps(paired,indent=2))
def fmt(v): return 'N/A' if v is None else f'{v:.4f}' if isinstance(v,float) else str(v)
def table(headers,rows): return '| '+' | '.join(headers)+' |\n| '+' | '.join(['---']*len(headers))+' |\n'+'\n'.join('| '+' | '.join(map(fmt,row))+' |' for row in rows)+'\n'
(P/'scenario-summary.md').write_text('# Scenario statistics\n\n'+table(['Scenario','Metric','n','Mean','Median','Sample SD','Min','Max','P05','P95','Mean CI low','Mean CI high'],[[s,m]+[v.get(k) for k in ['N','Mean','Median','SD','Min','Max','P05','P95','CI95Low','CI95High']] for s,ms in summary.items() for m,v in ms.items()]))
(P/'paired-comparison.md').write_text('# Paired differences\n\n'+table(['Pair','Metric','n','Unavailable','Mean Δ','Median Δ','Sample SD Δ','Mean Δ CI low','Mean Δ CI high','Increase','Decrease','Unchanged'],[[p,m]+[v.get(k) for k in ['N','Unavailable','Mean','Median','SD','CI95Low','CI95High','Increase','Decrease','Unchanged']] for p,ms in paired.items() for m,v in ms.items()]))
import gzip
series=json.loads((P/'time-series.json').read_text()) if (P/'time-series.json').exists() else json.loads(gzip.decompress((P/'time-series.json.gz').read_bytes())); bands=[]
# Extending the horizon must preserve the archived baseline's first 300 projected days.
old_series=json.loads((P.parent/'specialist-capacity-v1/time-series.json').read_text())
current_baseline={r['Seed']:r['Series'] for r in series if r['Scenario']=='T2-R5'}
points_checked=0
for old in old_series:
    if old['Scenario']!='S2':continue
    for metric,points in old['Series'].items():
        assert current_baseline[old['Seed']][metric][:300]==points,(old['Seed'],metric)
        points_checked+=len(points)
(P/'baseline-equivalence.json').write_text(json.dumps(dict(Exact=True,Seeds=30,DaysPerSeed=300,ComparedMetricPoints=points_checked,Reference='specialist-capacity-v1 S2',Current='T2-R5'),indent=2))
del old_series,current_baseline

# Authoritative end-of-day WIP, without smoothing; retain compact daily flow evidence too.
import gzip,lzma
flow=[]
print('PASS final-window statistics and paired comparisons',flush=True)
for history_index,run in enumerate(series,1):
    with lzma.open(P/'histories'/f"{run['Scenario']}-{run['Seed']}.json.xz",'rt') as f: days=json.load(f)
    assert len(days)==500 and [d['Day'] for d in days]==list(range(500))
    if history_index%30==0:print(f'Read {history_index}/360 complete daily histories',flush=True)
    run['Series']['TotalWip']=[dict(Day=d['Day']+1,Value=d['TotalWip']) for d in days]
    flow.append(dict(Scenario=run['Scenario'],Seed=run['Seed'],Days=[{k:v for k,v in d.items() if k!='Items'} for d in days]))
with gzip.open(P/'daily-flow.json.gz','wt') as f: json.dump(flow,f,separators=(',',':'))
with gzip.open(P/'time-series.json.gz','wt') as f: json.dump(series,f,separators=(',',':'))
(P/'time-series.json').unlink(missing_ok=True)
flow_by={(x['Scenario'],x['Seed']):x['Days'] for x in flow}
upstream_keys=['DevelopmentWork','ReviewWork','UsedDevelopmentCapacity','UsedReviewCapacity','UsedDebtRepaymentCapacity','Debt','WaitingForCodeReviewCount','DevelopmentCount','SpecialistWorkWaiting']
upstream_equal=all(all(all(d[k]==base[k] for k in upstream_keys) for d,base in zip(days,flow_by['T2-R5',seed])) for (scenario,seed),days in flow_by.items())
three_four_keys=['TestingWork','UsedTesterCapacity','WaitingForTestingCount','ReadyForReleaseCount','DoneCount','TotalWip']
three_four_equal=all(all(all(a[k]==b[k] for k in three_four_keys) for a,b in zip(flow_by[f'T3-R{r}',seed],flow_by[f'T4-R{r}',seed])) for r in ['5','7','10','U'] for seed in range(12345,12375))
(P/'factor-checks.json').write_text(json.dumps(dict(FixedRequestsIdentical=True,UpstreamDailyFieldsIdentical=upstream_equal,ThreeFourTesterDailyFlowIdentical=three_four_equal,UpstreamKeys=upstream_keys,ThreeFourKeys=three_four_keys),indent=2))
balances=[]
for r in runs:
    ds=flow_by[r['Scenario'],r['Seed']];a,b=ds[449],ds[499];m=r['Metrics']
    test_delta=b['WaitingForTestingCount']+b['TestingCount']-a['WaitingForTestingCount']-a['TestingCount']
    release_delta=b['ReadyForReleaseCount']-a['ReadyForReleaseCount']
    assert abs(test_delta-(m['ReviewCompletedWindow']-10*m['CompletionRate']))<1e-8
    assert abs(release_delta-10*(m['CompletionRate']-m['Throughput']))<1e-8
    balances.append(dict(Scenario=r['Scenario'],Seed=r['Seed'],TestingInventoryChange=test_delta,ReleaseQueueChange=release_delta,TotalWipChange=b['TotalWip']-a['TotalWip']))
(P/'flow-balance.json').write_text(json.dumps(balances,indent=2))
capacity_records=[]
for r in runs:
    ds=flow_by[r['Scenario'],r['Seed']]
    releases=[d['DoneCount']-(ds[i-1]['DoneCount'] if i else 0) for i,d in enumerate(ds)]
    capacity_records.append(dict(Scenario=r['Scenario'],Seed=r['Seed'],MaxReleaseBatch500=max(releases),MaxTestingQueue500=max(d['WaitingForTestingCount'] for d in ds),MaxReleaseQueue500=max(d['ReadyForReleaseCount'] for d in ds),MaxWip500=max(d['TotalWip'] for d in ds),MaxReviewQueue500=max(d['WaitingForCodeReviewCount'] for d in ds),MaxTestingCapacityUsed500=max(d['UsedTesterCapacity'] for d in ds),MaxReviewCapacityUsed500=max(d['UsedReviewCapacity'] for d in ds),ReleaseBatchesFinalWindow=[releases[i] for i in range(450,500) if (i+1)%5==0]))
(P/'capacity-evidence.json').write_text(json.dumps(capacity_records,indent=2))
block_runs=[]
for (scenario,seed),ds in flow_by.items():
    for start in range(0,500,50):
        end=ds[start+49];before=ds[start-1] if start else {'DoneCount':0,'ReadyForReleaseCount':0}
        block_runs.append(dict(Scenario=scenario,Seed=seed,Block=f'{start+1}-{start+50}',ReleasedThroughput=(end['DoneCount']-before['DoneCount'])/10,CompletionRate=(end['DoneCount']+end['ReadyForReleaseCount']-before['DoneCount']-before['ReadyForReleaseCount'])/10))
block_summary={scenario:{f'{start+1}-{start+50}':{m:stats([r[m] for r in block_runs if r['Scenario']==scenario and r['Block']==f'{start+1}-{start+50}']) for m in ['ReleasedThroughput','CompletionRate']} for start in range(0,500,50)} for scenario in variants}
(P/'block-flow.json').write_text(json.dumps(dict(Runs=block_runs,Summary=block_summary),indent=2))
for r in block_runs:
    if r['Block']=='451-500':
        assert abs(r['ReleasedThroughput']-by[r['Scenario'],r['Seed']]['Throughput'])<1e-8
        assert abs(r['CompletionRate']-by[r['Scenario'],r['Seed']]['CompletionRate'])<1e-8
dominance=[]
for testers in [2,3,4]:
    for lower,higher in [('5','7'),('7','10'),('10','U')]:
        for seed in range(12345,12375):
            a,b=flow_by[f'T{testers}-R{lower}',seed],flow_by[f'T{testers}-R{higher}',seed]
            gaps=[y['DoneCount']-x['DoneCount'] for x,y in zip(a,b)]
            assert min(gaps)>=0,'Higher release capacity delays cumulative delivery'
            dominance.append(dict(Lower=f'T{testers}-R{lower}',Higher=f'T{testers}-R{higher}',Seed=seed,MinimumCumulativeGain=min(gaps),MaximumCumulativeGain=max(gaps),GainAtDay450=gaps[449],GainAtDay500=gaps[499],FinalWindowReleaseDifference=gaps[499]-gaps[449]))
(P/'boundary-effects.json').write_text(json.dumps(dominance,indent=2))
del flow,flow_by,days






assert len(series)==360
for run in series:
    for points in run['Series'].values():assert [p['Day'] for p in points]==list(range(1,501))
for s in [f'T{t}-R{r}' for t in [2,3,4] for r in ['5','7','10','U']]:
    ss=[x for x in series if x['Scenario']==s]
    for m in ss[0]['Series']:
        for day in range(1,501):
            vals=[next((p['Value'] for p in x['Series'][m] if p['Day']==day),None) for x in ss]
            v=stats(vals);bands.append(dict(Scenario=s,Metric=m,Day=day,**v))
with gzip.open(P/'time-series-bands.json.gz','wt') as f: json.dump(bands,f,separators=(',',':'))
colors={s:plt.get_cmap('tab20')(i) for i,s in enumerate(variants)}
plt.rcParams.update({'font.size':9,'axes.spines.top':False,'axes.spines.right':False})
fig,axs=plt.subplots(4,4,figsize=(17,14),layout='constrained')
for ax,m in zip(axs.flat,metrics[:16]):
    for i,s in enumerate(variants,1):
        vals=[r['Metrics'][m] for r in runs if r['Scenario']==s and r['Metrics'][m] is not None]
        if vals:
            ax.boxplot([vals],positions=[i],widths=.5,whis=(5,95),showfliers=False)
            ax.scatter([i+((j%7)-3)*.025 for j in range(len(vals))],vals,s=9,alpha=.6,color=colors[s])
        else:ax.text(i,.05,'N/A',transform=ax.get_xaxis_transform(),ha='center',fontsize=8)
    ax.set_xticks(range(1,13),variants,rotation=60);ax.set_xlim(.4,12.6);ax.set_title(m);ax.grid(axis='y',alpha=.2)
fig.suptitle('30 seeds per variant · Days 451–500; DebtRatio at Day 500\nBoxes Q1–Q3; whiskers within P05–P95; dots all runs; N/A means no completion/release cohort')
fig.savefig(P/'distributions.png',dpi=150);fig.savefig(P/'distributions.svg');plt.close(fig)
labels={'ReadyForRelease':'Ready for Release (end of day)','TotalWip':'Total WIP (end of day)','Throughput':'Released / 5 days (rolling 50 days)','CycleTime':'Delivery cycle days (rolling release cohort)','SpecialistWorkWaiting':'Specialist waiting (end of day)','TestingQueue':'Testing queue (end of day)'}
for number,group in enumerate([variants[:4],variants[4:8],variants[8:]],1):
    fig,axs=plt.subplots(len(labels),len(group),figsize=(5*len(group),19),layout='constrained',sharex=True,squeeze=False)
    for row,(m,label) in enumerate(labels.items()):
        all_bs=[b for b in bands if b['Metric']==m and b['N']]
        ymax=max(b['P95'] for b in all_bs);ymin=min(b['P05'] for b in all_bs)
        for col,s in enumerate(group):
            ax=axs[row,col];bs=[b for b in all_bs if b['Scenario']==s]
            ax.plot([b['Day'] for b in bs],[b['Median'] for b in bs],color=colors[s],lw=1.3)
            ax.fill_between([b['Day'] for b in bs],[b['P05'] for b in bs],[b['P95'] for b in bs],color=colors[s],alpha=.2)
            ax.set_ylim(min(0,ymin),ymax*1.08 if ymax else 1)
            ax.axvspan(451,500,color='grey',alpha=.08);ax.grid(alpha=.2);ax.set_title(f'{s} · {label}',fontsize=9)
            if row==len(labels)-1:ax.set_xlabel('Simulated day')
    fig.suptitle('Cross-seed median and empirical P05–P95 band (not mean CI)\nEarly rolling windows use available days; no-release cycle time remains missing; axes shared across all three figures')
    fig.savefig(P/f'trajectories-{number}.png',dpi=150);fig.savefig(P/f'trajectories-{number}.svg');plt.close(fig)
# Quantify accumulation throughout the run using raw daily queues, not cumulative release counts.
regimes={}
for s in [f'T{t}-R{r}' for t in [2,3,4] for r in ['5','7','10','U']]:
    regimes[s]={}
    for m in series[0]['Series']:
        regimes[s][m]={}
        for first,last in [(d,d+49) for d in range(1,501,50)]:
            vs=[];slopes=[]
            for x in series:
                if x['Scenario']!=s:continue
                pts=[(p['Day'],p['Value']) for p in x['Series'][m] if first<=p['Day']<=last and p['Value'] is not None]
                if pts:
                    vs.append(st.mean(v for _,v in pts));mx=st.mean(d for d,_ in pts);my=st.mean(v for _,v in pts)
                    slopes.append(sum((d-mx)*(v-my) for d,v in pts)/sum((d-mx)**2 for d,_ in pts) if len(pts)>1 else 0)
            regimes[s][m][f'{first}-{last}']={'MeanAcrossSeedBlockMeans':stats(vs),'DailySlopeAcrossSeeds':stats(slopes)}
(P/'regimes.json').write_text(json.dumps(regimes,indent=2))
print('PASS 360 records, 36 repeats, statistics and graphs.')
for pair in paired:
    print(pair)
    for m in ['Throughput','DeliveryCycleTime','SpecialistQueue','TestingQueue','DeveloperUtilization','DeliveryWorkCost','SystemCost']:
        print(m, {k:v for k,v in paired[pair][m].items() if k not in ['Differences','Min','Max','P05','P95']})

for svg in P.glob('*.svg'):svg.write_text('\n'.join(line.rstrip() for line in svg.read_text().splitlines())+'\n')
# Horizon-level drift complements within-block slopes; exclude startup explicitly.
drift={}
for scenario in variants:
    drift[scenario]={}
    for metric in ['TestingQueue','ReadyForRelease','TotalWip','CycleTime','Throughput']:
        drift[scenario][metric]={}
        for first,last in [(51,500),(201,500)]:
            slopes=[]
            for run in series:
                if run['Scenario']!=scenario:continue
                pts=[(p['Day'],p['Value']) for p in run['Series'][metric] if first<=p['Day']<=last and p['Value'] is not None]
                mx=st.mean(d for d,v in pts);my=st.mean(v for d,v in pts)
                slopes.append(sum((d-mx)*(v-my) for d,v in pts)/sum((d-mx)**2 for d,v in pts))
            drift[scenario][metric][f'{first}-{last}']={**stats(slopes),'Positive':sum(v>1e-9 for v in slopes),'Negative':sum(v< -1e-9 for v in slopes),'Zero':sum(abs(v)<=1e-9 for v in slopes),'Slopes':slopes}
(P/'long-horizon-slopes.json').write_text(json.dumps(drift,indent=2))
