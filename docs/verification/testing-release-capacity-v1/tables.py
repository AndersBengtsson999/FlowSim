"""Generate compact report tables from completed analysis artifacts."""
from pathlib import Path
import json
P=Path(__file__).resolve().parent
s=json.loads((P/'summary.json').read_text());p=json.loads((P/'paired.json').read_text());g=json.loads((P/'regimes.json').read_text())
variants=[f'T{t}-R{r}' for t in [2,3,4] for r in ['5','7','10','U']]
def f(x):return 'N/A' if x is None else f'{x:.3f}' if isinstance(x,float) else str(x)
def table(headers,rows):return '| '+' | '.join(headers)+' |\n| '+' | '.join(['---']*len(headers))+' |\n'+'\n'.join('| '+' | '.join(map(f,row))+' |' for row in rows)+'\n'
def metric_table(title,metrics):return '## '+title+'\n\n'+table(['Configuration']+metrics,[[v]+[s[v][m].get('Mean') for m in metrics] for v in variants])+'\n'
text='# Report tables\n\nAll values are means across 30 seeds unless labeled otherwise. Units and cohort definitions are in README.md.\n\n'
text+=metric_table('Delivery and cost',['Throughput','CompletionRate','DevelopmentCycleTime','DeliveryCycleTime','ReleaseWaitTime','DeliveryWorkCost','SystemCost','ReleasedCumulative'])
text+=metric_table('Queues and utilization',['AverageWip','ReviewQueue','TestingQueue','ReleaseQueue','SpecialistQueue','DependencyQueue','DeveloperUtilization','TesterUtilization','DebtRatio'])
text+=metric_table('Final queues on Day 500',['FinalBacklog','FinalDevelopmentCount','FinalReviewQueue','FinalTestingQueue','FinalReleaseQueue','FinalSpecialistQueue','FinalDependencyQueue','FinalReworkQueue','FinalTotalWip'])
text+=metric_table('Final-window flow and consumed capacity (counts and raw units over 50 days)',['DevelopmentCompletedWindow','ReviewCompletedWindow','DevelopmentCapacityWindow','ReviewCapacityWindow','RepaymentCapacityWindow','CollaborationCapacityWindow','TesterCapacityWindow','ReleaseOpportunitiesFullWindow'])
text+='## Key paired differences\n\nHigher minus lower capacity, with the other factor held fixed. ↑/↓/= count same-seed differences. These are exploratory mean intervals, not real-world uncertainty.\n\n'
selected=[(f'T3-R{r}',f'T2-R{r}') for r in ['5','7']]+[(f'T4-R{r}',f'T3-R{r}') for r in ['5','7']]+[(f'T{t}-R{hi}',f'T{t}-R{lo}') for t in [2,3,4] for hi,lo in [('7','5'),('10','7'),('U','10')]]
rows=[]
for hi,lo in selected:
 for m in ['Throughput','DeliveryCycleTime','TestingQueue','ReleaseQueue','SystemCost']:
  v=p[hi+' − '+lo][m];rows.append([hi+' − '+lo,m,v['Mean'],v['CI95Low'],v['CI95High'],v['Increase'],v['Decrease'],v['Unchanged']])
text+=table(['Pair','Metric','Mean Δ','95% CI low','95% CI high','↑','↓','='],rows)
(P/'report-tables.md').write_text(text)
text='# Consecutive 50-day blocks\n\nMeans across 30 seed-local block means and within-block OLS slopes. WIP and queues are raw end-of-day states; throughput and delivery cycle are rolling 50-day series. Slope units are metric units per simulated day. These are descriptive finite-horizon diagnostics.\n\n'
for m in ['TestingQueue','ReadyForRelease','TotalWip','CycleTime','Throughput']:
 text+='## '+m+'\n\n'
 rows=[]
 for v in variants:
  for b,vals in g[v][m].items():
   mean=vals['MeanAcrossSeedBlockMeans'];sl=vals['DailySlopeAcrossSeeds'];rows.append([v,b,mean['Mean'],sl['Mean'],sl['CI95Low'],sl['CI95High'],sl['Min'],sl['Max']])
 text+=table(['Configuration','Days','Block mean','Mean daily slope','Slope mean CI low','CI high','Min seed slope','Max seed slope'],rows)+'\n'
(P/'block-comparison.md').write_text(text)
print('PASS compact report tables and all ten block comparisons.')

bf=json.loads((P/'block-flow.json').read_text())['Summary']
text='# Non-overlapping block rates\n\nActual releases and work completions within each 50-day block, normalized to five days. These are distinct from the average of rolling throughput endpoints in block-comparison.md.\n\n'+table(['Configuration','Days','Released / 5 days','Completion / 5 days'],[[v,b,d['ReleasedThroughput']['Mean'],d['CompletionRate']['Mean']] for v in variants for b,d in bf[v].items()])
(P/'block-rates.md').write_text(text)
