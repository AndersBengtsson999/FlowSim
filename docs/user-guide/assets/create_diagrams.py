"""Reproducible explanatory diagrams from audited model definitions; not UI screenshots."""
from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
root=Path(__file__).parent/'images'
fontpath='/System/Library/Fonts/Supplemental/Arial.ttf'
def make(name,title,boxes,footer):
 im=Image.new('RGB',(1200,430),'#172532');d=ImageDraw.Draw(im)
 titlefont=ImageFont.truetype(fontpath,34);font=ImageFont.truetype(fontpath,24);small=ImageFont.truetype(fontpath,21)
 d.text((36,25),title,font=titlefont,fill='#F0F4F7')
 n=len(boxes);gap=28;w=(1128-(n-1)*gap)//n
 for i,(label,color) in enumerate(boxes):
  x=36+i*(w+gap);d.rounded_rectangle((x,115,x+w,275),radius=12,fill='#20313F',outline=color,width=3)
  lines=label.split('\n');height=len(lines)*32
  for j,line in enumerate(lines):
   length=d.textlength(line,font=font);d.text((x+(w-length)/2,195-height/2+j*32),line,font=font,fill='#F0F4F7')
  if i<n-1:
   d.line((x+w+3,195,x+w+gap-5,195),fill='#68A9D3',width=3);d.polygon([(x+w+gap-3,195),(x+w+gap-11,189),(x+w+gap-11,201)],fill='#68A9D3')
 for i,line in enumerate(footer.split('\n')):d.text((36,320+30*i),line,font=small,fill='#A9BBC8')
 im.save(root/name)
make('workflow.png','Workflow: admission, work, release',[(x,'#68A9D3') for x in ['Backlog','Develop','Review','Test','Ready','Released']], 'Waiting queues separate active stages. Defects return through Rework and Review.\nInternal release ends this pipeline; customer launch is a separate version-level event.')
make('launch.png','Internal releases accumulate into a customer launch',[(x,'#68A9D3') for x in ['Internal\nrelease A','Internal\nrelease B','Active\nversion','Customer\nlaunch','Next active\nversion']], 'Only internally released items in the active version are included. WIP remains unfinished.\nRecurring next target = actual launch day + interval; internal release cadence is independent.')
make('economics.png','Incurred cost reconciles across four categories',[(x,c) for x,c in [('WIP\nallocation','#68A9D3'),('Awaiting\nlaunch','#E9B65D'),('Customer-\nlaunched','#55C995'),('Unallocated\noverhead','#A9BBC8')]], 'Read the categories with PLUS signs: Incurred = WIP + Awaiting + Launched + Overhead.\nUnlaunched = WIP + Awaiting. Launch reclassifies cost; it creates no expense or revenue.')
# Replace the category arrows with plus signs: these are components, not workflow transitions.
p=root/'economics.png';im=Image.open(p);d=ImageDraw.Draw(im);f=ImageFont.truetype(fontpath,28)
for i in range(3):
 x=36+(i+1)*261+i*28;d.rectangle((x+1,175,x+27,214),fill='#172532');d.text((x+5,178),'+',font=f,fill='#68A9D3')
im.save(p)
make('intervention.png','A Day 100 intervention: 20-day comparison',[(x,'#68A9D3') for x in ['Before\nDays 81–100','Change\nDay 100','Effective\nDay 101','After\nDays 101–120']], 'All boundaries are inclusive. At Day 108, After contains only Days 101–108.\nA staffing change is a hypothesis: inspect queues, throughput and cost before claiming an effect.')
