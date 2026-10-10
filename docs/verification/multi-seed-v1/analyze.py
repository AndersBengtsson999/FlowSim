"""Statistical summaries and publication-style figures; run after the C# harness."""
from pathlib import Path
import json, math, statistics as st
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from scipy.stats import t
P=Path(__file__).resolve().parent
raw=json.loads((P/'raw-results.json').read_text()); runs=raw['Runs']
assert len(runs)==120 and not raw['Failures']
assert {(r['Scenario'],r['Seed']) for r in runs}=={(s,k) for s in 'ABCD' for k in range(12345,12375)}
assert len(raw['Repeats'])==12 and all(r['ExactState'] and r['ExactResult'] and r['ExactMetrics'] for r in raw['Repeats'])
# Protect scenario equivalence against the archived Model Validation v2 evidence.
previous=json.loads((P.parent/'model-validation-v2/results.json').read_text())['Reports']
for old in previous:
    current=next(r['Metrics'] for r in runs if r['Scenario']==old['Scenario'] and r['Seed']==12345)
    mapping={'Throughput':'Throughput','CompletionRate':'CompletionRate','DevelopmentCycleTime':'DevelopmentCycleTime','DeliveryCycleTime':'DeliveryCycleTime','ReleaseWaitTime':'ReleaseWaitTime','AverageWip':'AverageWip','DeveloperUtilization':'DeveloperUtilization','TesterUtilization':'TesterUtilization','DeliveryWorkCost':'DeliveryWorkCostPerItem','SystemCost':'SystemCostPerReleasedItem','DebtRatio':'DebtRatio','ReleasedCumulative':'Released'}
    for new,key in mapping.items():assert math.isclose(current[new],old[key],rel_tol=1e-12,abs_tol=1e-12),(old['Scenario'],new)
    for new,key in [('ReviewQueue','Review'),('TestingQueue','Testing'),('DependencyQueue','Dependency'),('SpecialistQueue','Specialist'),('ReleaseQueue','Release')]:assert math.isclose(current[new],old[key]['Average'],rel_tol=1e-12,abs_tol=1e-12)
metrics=list(runs[0]['Metrics'])
def quantile(x,p):
    x=sorted(x); h=(len(x)-1)*p; i=int(h); j=min(i+1,len(x)-1)
    return x[i]+(h-i)*(x[j]-x[i])
def stats(values):
    x=[v for v in values if v is not None]; n=len(x)
    assert all(math.isfinite(v) for v in x)
    if not n: return dict(N=0,Missing=len(values))
    mean=st.mean(x); sd=st.stdev(x) if n>1 else 0; margin=float(t.ppf(.975,n-1))*sd/math.sqrt(n) if n>1 else 0
    return dict(N=n,Missing=len(values)-n,Mean=mean,Median=st.median(x),SD=sd,Min=min(x),Max=max(x),P05=quantile(x,.05),P95=quantile(x,.95),CI95Low=mean-margin,CI95High=mean+margin)
# Independent known-value checks, including the percentile convention and paired SD.
assert quantile([0,10],.05)==.5 and quantile([0,10],.95)==9.5
assert stats([1,2,3])['SD']==1 and stats([2,2,2])['CI95Low']==2
assert abs(float(t.ppf(.975,29))-2.045229642)<1e-8
summary={s:{m:stats([r['Metrics'][m] for r in runs if r['Scenario']==s]) for m in metrics} for s in 'ABCD'}
by={(r['Scenario'],r['Seed']):r['Metrics'] for r in runs}; paired={}
for hi,lo in [('B','A'),('C','A'),('D','C')]:
    label=hi+' − '+lo; paired[label]={}
    for m in metrics:
        ds=[by[hi,seed][m]-by[lo,seed][m] for seed in range(12345,12375) if by[hi,seed][m] is not None and by[lo,seed][m] is not None]
        paired[label][m]={**stats(ds),'Increase':sum(v>1e-9 for v in ds),'Decrease':sum(v< -1e-9 for v in ds),'Unchanged':sum(abs(v)<=1e-9 for v in ds),'Differences':ds}
(P/'summary.json').write_text(json.dumps(summary,indent=2));(P/'paired.json').write_text(json.dumps(paired,indent=2))
def fmt(v): return f'{v:.4f}' if isinstance(v,float) else str(v)
def table(headers,rows): return '| '+' | '.join(headers)+' |\n| '+' | '.join(['---']*len(headers))+' |\n'+'\n'.join('| '+' | '.join(map(fmt,row))+' |' for row in rows)+'\n'
(P/'scenario-summary.md').write_text('# Scenario statistics\n\n'+table(['Scenario','Metric','n','Mean','Median','Sample SD','Min','Max','P05','P95','Mean CI low','Mean CI high'],[[s,m]+[v[k] for k in ['N','Mean','Median','SD','Min','Max','P05','P95','CI95Low','CI95High']] for s,ms in summary.items() for m,v in ms.items()]))
(P/'paired-comparison.md').write_text('# Paired differences\n\n'+table(['Pair','Metric','n','Mean Δ','Median Δ','Sample SD Δ','Mean Δ CI low','Mean Δ CI high','Increase','Decrease','Unchanged'],[[p,m]+[v[k] for k in ['N','Mean','Median','SD','CI95Low','CI95High','Increase','Decrease','Unchanged']] for p,ms in paired.items() for m,v in ms.items()]))
series=json.loads((P/'time-series.json').read_text()); bands=[]
assert len(series)==120
for run in series:
    for points in run['Series'].values():assert [p['Day'] for p in points]==list(range(1,301))
for s in 'ABCD':
    ss=[x for x in series if x['Scenario']==s]
    for m in ss[0]['Series']:
        for day in range(1,301):
            vals=[next((p['Value'] for p in x['Series'][m] if p['Day']==day),None) for x in ss]
            v=stats(vals);bands.append(dict(Scenario=s,Metric=m,Day=day,**v))
(P/'time-series-bands.json').write_text(json.dumps(bands,indent=2))
colors=dict(zip('ABCD',['#3366aa','#dd7722','#238b69','#9a448e']))
plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False})
fig,axs=plt.subplots(4,4,figsize=(16,14),layout='constrained')
for ax,m in zip(axs.flat,metrics[:16]):
    vals=[[r['Metrics'][m] for r in runs if r['Scenario']==s] for s in 'ABCD']
    ax.boxplot(vals,tick_labels=list('ABCD'),whis=(5,95),showfliers=True)
    for i,(s,x) in enumerate(zip('ABCD',vals),1):
        ax.scatter([i+((j%7)-3)*.025 for j in range(30)],x,s=10,alpha=.65,color=colors[s])
    ax.set_title(m);ax.grid(axis='y',alpha=.2)
fig.suptitle('30 seeds per scenario · Days 251–300; DebtRatio at Day 300\nBoxes: Q1–Q3; whiskers: observed values within P05–P95; dots: all runs')
fig.savefig(P/'distributions.png',dpi=150);fig.savefig(P/'distributions.svg');plt.close(fig)
labels={'Throughput':'Released / 5 days (rolling 50 days)','CycleTime':'Delivery cycle days (rolling release cohort)','TestingQueue':'Waiting for Testing (end of day)','ReadyForRelease':'Ready for Release (end of day)','TechnicalDebtRatio':'Technical Debt Ratio (%) (end of day)'}
fig,axs=plt.subplots(5,4,figsize=(17,16),layout='constrained',sharex=True)
for row,(m,label) in enumerate(labels.items()):
    for col,s in enumerate('ABCD'):
        ax=axs[row,col];bs=[b for b in bands if b['Scenario']==s and b['Metric']==m and b['N']]
        ax.plot([b['Day'] for b in bs],[b['Median'] for b in bs],color=colors[s],lw=1.3)
        ax.fill_between([b['Day'] for b in bs],[b['P05'] for b in bs],[b['P95'] for b in bs],color=colors[s],alpha=.2)
        if s=='D': ax.axvline(150.5,color='black',ls=':',alpha=.5)
        ax.axvspan(251,300,color='grey',alpha=.08);ax.grid(alpha=.2);ax.set_title(f'{s} · {label}',fontsize=9)
        if row==4: ax.set_xlabel('Simulated day')
    # Equal y scales across scenarios for each metric.
    low=min(a.get_ylim()[0] for a in axs[row]);high=max(a.get_ylim()[1] for a in axs[row])
    for a in axs[row]:a.set_ylim(low,high)
fig.suptitle('Cross-seed median and empirical P05–P95 band (not a mean confidence interval)\nEarly rolling windows use available days; missing pre-release cycle time remains missing')
fig.savefig(P/'trajectories.png',dpi=150);fig.savefig(P/'trajectories.svg');plt.close(fig)
# Quantify accumulation throughout the run using raw daily queues, not cumulative release counts.
regimes={}
for s in 'ABCD':
    regimes[s]={}
    for m in labels:
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
print('PASS statistics checks; summary, paired differences, bands, regimes and figures written.')
for pair in paired:
    print(pair)
    for m in ['Throughput','DeliveryWorkCost','DeliveryCycleTime','TestingQueue','SpecialistQueue','DebtRatio']:
        v=paired[pair][m];print(m, {k:round(v[k],6) if isinstance(v[k],float) else v[k] for k in ['Mean','CI95Low','CI95High','Increase','Decrease','Unchanged']})
# Detail panel avoids hiding C/D late dynamics behind B's large queue and the initial debt spike.
fig,axs=plt.subplots(3,2,figsize=(12,9),layout='constrained',sharex=True)
for row,m in enumerate(['CycleTime','TestingQueue','TechnicalDebtRatio']):
    for col,s in enumerate('CD'):
        ax=axs[row,col];bs=[b for b in bands if b['Scenario']==s and b['Metric']==m and b['Day']>=100 and b['N']]
        ax.plot([b['Day'] for b in bs],[b['Median'] for b in bs],color=colors[s])
        ax.fill_between([b['Day'] for b in bs],[b['P05'] for b in bs],[b['P95'] for b in bs],alpha=.2,color=colors[s])
        if s=='D':ax.axvline(150.5,color='black',ls=':')
        ax.set_title(f'{s} · {labels[m]}',fontsize=10);ax.grid(alpha=.2)
    low=min(a.get_ylim()[0] for a in axs[row]);high=max(a.get_ylim()[1] for a in axs[row])
    for a in axs[row]:a.set_ylim(low,high)
for a in axs[-1]:a.set_xlabel('Simulated day')
fig.suptitle('C and D detail · Days 100–300 · Median and empirical P05–P95 band')
fig.savefig(P/'trajectories-detail.png',dpi=150);fig.savefig(P/'trajectories-detail.svg');plt.close(fig)
