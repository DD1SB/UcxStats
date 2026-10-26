'use strict';
const fs=require('node:fs');
const data=JSON.parse(fs.readFileSync(0,'utf8'));
const scenario=process.argv[2];
const {createRuntime}=require('./map-runtime.cjs');
const {run}=createRuntime(data,{scenario});
if(scenario==='multiple-windows'){
 const second=createRuntime(data,{scenario});
 run("statisticsSettings.liveTimelineMinutes=137;statisticsSettings.timelinePanelPosition={x:.1,y:.2};statisticsSettings.liveTimelineVisible=false;window.ucxApplyTimelineSettings()");
 second.run("assert.equal(statisticsSettings.liveTimelineMinutes,60);assert.equal(statisticsSettings.timelinePanelPosition,null);assert.equal(statisticsSettings.liveTimelineVisible,true);window.ucxApplyRemoteView(30,40,6);assert.equal(statisticsSettings.liveTimelineMinutes,60)");
}

setImmediate(()=>run(`
const now=Date.now(),minute=60000;
function setSeries(rows,latest=null){const start=Math.floor((Date.now()-250*minute)/1000);window.ucxSetLiveTimeline({generatedAtUnixSeconds:Date.now()/1000,coverageStartUnixSeconds:start,coverageEndUnixSeconds:Date.now()/1000+240*60,samples:rows.flatMap(([ms,n])=>[Math.floor(ms/1000)-start,n]),latestQso:latest,futureBeyondCoverageCount:0})}
function last(ms=now,call='LAST'){return {qsoId:1,callSign:call,bandCode:4,timestampUtc:new Date(ms).toISOString()}}
function event(target,extra={}){return {target,pointerId:1,button:0,isPrimary:true,clientX:100,clientY:100,preventDefault(){this.prevented=true},stopPropagation(){this.stopped=true},...extra}}
function fire(element,name,e={}){const fn=element.listeners.get(name);assert.ok(fn,name+' handler missing');const value=event(element,e);fn(value);return value}
function movePanel(id,dx,dy){const s=mapPanels.get(id);fire(s.handle,'pointerdown');fire(s.handle,'pointermove',{clientX:100+dx,clientY:100+dy});fire(s.handle,'pointerup',{clientX:100+dx,clientY:100+dy})}
const fingerprint=()=>JSON.stringify({center:map.center,zoom:map.zoom,bands:[...bandLayers.values()].map(b=>b.enabled),showPaths,showLong,showQsos,showNight,dxccStatus,statisticsSnapshot});
eval(updateScript);
map.setView([15,165],5);showLong=true;showNight=true;refreshAll();
const baseline=fingerprint(),layers=[...map.layers],paths=[...bandLayers.values()].flatMap(b=>[...b.paths.layers,...b.longPaths.layers]),styles=paths.map(x=>({...x.options}));
if(scenario==='normal'){
 assert.equal(isLiveMap,false);assert.equal(timelineHost,null);assert.equal(timelineSettingsHost,null);assert.equal(mapPanels.size,2);assert.equal(timers.filter(x=>x.ms===1000).length,0);
 assert.ok(!statisticsHost.innerHTML.includes('ucx-live-activity'));
}
if(scenario==='empty-single'){
 setSeries([]);let model=timelineModel(now,60,5);assert.equal(model.count,0);assert.equal(model.gaps.length,1);assert.equal(model.gaps[0].from,now-60*minute);assert.equal(model.gaps[0].to,now);
 setSeries([[now-15000,1]],last(now-15000));model=timelineModel(now,60,5);assert.equal(model.count,1);assert.equal(model.buckets.reduce((n,b)=>n+b.count,0),1);assert.equal(liveActivityState().seconds,15);
 assert.ok(timelineElement('ucx-timeline-chart').innerHTML.includes('1 QSOs'));
}
if(scenario==='bucket-boundaries'){
 setSeries([[now,2],[now-5*minute,3],[now-5*minute-1000,4],[now-60*minute,5],[now-60*minute-1000,6]]);
 for(const size of [1,2,5,10]){const m=timelineModel(now,60,size);assert.equal(m.count,14);assert.equal(m.buckets.reduce((n,b)=>n+b.count,0),14);for(const b of m.buckets)assert.equal(b.start%(size*minute),0)}
 const m=timelineModel(now,60,5);assert.equal(m.buckets.at(-1).count,2);assert.equal(m.buckets.at(-2).count,3);
}
if(scenario==='partial-buckets'){
 const t=now+90*1000;setSeries([[now-60*minute+89*1000,2],[now-60*minute+90*1000,3],[now+90*1000,4],[now+91*1000,5]]);
 const m=timelineModel(t,60,5);assert.equal(m.count,7);assert.equal(m.buckets.reduce((n,b)=>n+b.count,0),7);assert.ok(m.buckets[0].from>m.buckets[0].start);assert.ok(m.buckets.at(-1).to<m.buckets.at(-1).end);
}
if(scenario==='rolling-duration'){
 setSeries([[now-240*minute,1],[now-239*minute,2],[now-60*minute,4],[now-15*minute,8],[now,16]],last());
 assert.equal(timelineModel(now,15,5).count,24);assert.equal(timelineModel(now,60,5).count,28);assert.equal(timelineModel(now,240,10).count,31);
 assert.equal(timelineModel(now+1000,240,1).count,30);assert.equal(timelineModel(now+1000,60,2).count,24);
}
if(scenario==='corrections-deletions'){
 setSeries([[now,3],[now-2*minute,2]],last(now,'FIRST'));assert.equal(timelineModel(now,60,5).count,5);
 setSeries([[now-5*minute,1]],last(now-5*minute,'CORRECTED'));assert.equal(timelineModel(now,60,5).count,1);assert.equal(liveActivity.callSign,'CORRECTED');
 setSeries([]);assert.equal(timelineModel(now,60,5).count,0);assert.equal(liveActivityState().status,'unknown');
}
if(scenario==='future-skew'){
 setSeries([[now+minute,2],[now-1000,1]],last(now+minute));assert.equal(timelineModel(now,60,5).count,1);assert.equal(liveActivityState().seconds,0);assert.equal(liveActivityState().future,true);
 advance(60);timers.find(x=>x.ms===1000).fn();assert.equal(timelineModel(Date.now(),60,5).count,3);assert.equal(liveActivityState().future,false);
 setNow(now-300*minute);updateLiveActivity();assert.ok(timelineModel(Date.now(),240,5).coverageLimited);assert.ok(timelineElement('ucx-timeline-chart').innerHTML.includes('außerhalb'));assert.ok(timelineElement('ucx-timeline-count').textContent.includes('Mindestens'));
}
if(scenario==='midnight-dst'){
 const midnight=Date.parse('2026-10-25T00:01:00Z'),a=Date.parse('2026-10-25T01:59:00+02:00'),b=Date.parse('2026-10-25T01:00:00+01:00');
 setNow(midnight);setSeries([[a,2],[b,3]],last(b));const m=timelineModel(midnight,15,5);assert.equal(m.count,5);assert.equal(m.buckets.reduce((n,b)=>n+b.count,0),5);assert.equal(liveActivityState().seconds,60);
}
if(scenario==='gap-classes'){
 setSeries([[now-20*minute,1],[now-18*minute,1]],last(now-18*minute));
 const m=timelineModel(now,60,1);assert.ok(m.gaps.length<60);assert.ok(m.gaps.some(g=>g.to-g.from===minute));assert.equal(m.ongoing.from,now-18*minute);assert.equal(timelineGapClass(60),'short');assert.equal(timelineGapClass(600),'warning');assert.equal(timelineGapClass(900),'prolonged');
 assert.ok(timelineElement('ucx-timeline-chart').innerHTML.includes('ucx-gap-ongoing'));assert.ok(!timelineElement('ucx-timeline-activity').innerHTML.includes('operator inactive'));
}
if(scenario==='timer-no-events'){
 setSeries([[now,1]],last());const tick=timers.find(x=>x.ms===1000).fn,n=timelineRenderCount;advance(1);tick();assert.equal(liveActivityState().seconds,1);assert.equal(timelineRenderCount,n);
 advance(599);tick();assert.equal(liveActivityState().status,'warning');advance(300);tick();assert.equal(liveActivityState().status,'prolonged');
 window.ucxSetLiveActivity(last(Date.now(),'NEW'));assert.equal(liveActivityState().seconds,0);assert.ok(timelineElement('ucx-timeline-activity').innerHTML.includes('Band des letzten QSOs'));
}
if(scenario==='panel-independent'){
 const before=mapPanels.get('contents').element.style.left;movePanel('statistics',100,-100);
 assert.equal(mapPanels.get('contents').element.style.left,before);assert.ok(statisticsSettings.statisticsPanelPosition.x>0);assert.ok(statisticsSettings.statisticsPanelPosition.y<1);
 assert.equal(messages.at(-1).type,'mapPresentation');assert.equal(JSON.stringify(messages.at(-1).settings.statisticsPanelPosition),JSON.stringify(statisticsSettings.statisticsPanelPosition));
 const held=statisticsSettings.statisticsPanelPosition;window.ucxApplyRemoteView(50,60,7);assert.deepEqual(statisticsSettings.statisticsPanelPosition,held);
 map.setView([15,165],5);
}
if(scenario==='panel-resize'){
 statisticsSettings.statisticsPanelPosition={x:999,y:-100};window.ucxApplyPanelSettings();const s=mapPanels.get('statistics');assert.ok(s.left<1000);assert.equal(s.top,8);
 statisticsSettings.statisticsPanelPosition={x:1,y:1};map.getContainer().clientWidth=320;map.getContainer().clientHeight=220;windowEvents.get('resize')();
 let rect=s.element.getBoundingClientRect();assert.ok(s.left+rect.width<=312);assert.ok(s.top+rect.height<=212);
 // Fullscreen and DPI transitions use the same CSS-viewport observer/reclamp path.
 map.getContainer().clientWidth=1920;map.getContainer().clientHeight=1080;windowEvents.get('resize')();rect=s.element.getBoundingClientRect();assert.equal(s.left+rect.width,1912);assert.equal(s.top+rect.height,map.getContainer().clientHeight-8);
 statisticsSettings.statisticsPanelPosition={x:NaN,y:Infinity};window.ucxApplyPanelSettings();assert.equal(s.left,8);
}
if(scenario==='panel-reset-collapse'){
 const s=mapPanels.get('statistics');movePanel('statistics',150,-100);fire(s.collapse,'click');assert.equal(s.body.hidden,true);const collapsed=statisticsSettings.statisticsPanelCollapsed;
 window.ucxResetPanelPositions();assert.equal(statisticsSettings.statisticsPanelPosition,null);assert.equal(statisticsSettings.contentsPanelPosition,null);assert.equal(s.body.hidden,collapsed);
 const timeline=mapPanels.get('timeline');const histogram=timelineHost.hidden;fire(timeline.collapse,'click');assert.equal(timelineHost.hidden,histogram);
}
if(scenario==='control-interactions'){
 const s=mapPanels.get('contents'),input=new Element('input');
 const oldStack=Number(s.element.style.zIndex||800);fire(s.element,'focusin');assert.ok(Number(s.element.style.zIndex)>oldStack);fire(s.handle,'pointerdown',{target:input});assert.equal(s.drag,null);
 assert.equal(s.element.clickBlocked,true);assert.equal(s.element.scrollBlocked,true);
 const slider=timelineSettingsHost.querySelector('#ucx-live-warning');fire(slider,'input',{target:Object.assign(slider,{value:20})});assert.equal(s.drag,null);assert.equal(mapPanels.get('timeline').drag,null);
 const key=fire(mapPanels.get('timeline').element,'keydown',{target:slider,key:'ArrowLeft'});assert.equal(key.stopped,true);assert.ok(!key.prevented);
 fire(s.handle,'pointerdown');assert.ok(s.drag);fire(s.handle,'pointercancel');assert.equal(s.drag,null);
 fire(s.handle,'keydown',{key:'ArrowLeft'});assert.ok(statisticsSettings.contentsPanelPosition.x<1);fire(s.handle,'keydown',{key:'Home'});assert.equal(statisticsSettings.contentsPanelPosition.x,1);
}
if(scenario==='slider-ordering'){
 const el=id=>timelineSettingsHost.querySelector('#'+id);
 fire(el('ucx-live-warning'),'input',{target:Object.assign(el('ucx-live-warning'),{value:20})});assert.equal(statisticsSettings.liveWarningThresholdMinutes,20);assert.equal(statisticsSettings.liveProlongedGapThresholdMinutes,21);
 fire(el('ucx-live-prolonged'),'input',{target:Object.assign(el('ucx-live-prolonged'),{value:5})});assert.equal(statisticsSettings.liveWarningThresholdMinutes,4);assert.equal(statisticsSettings.liveProlongedGapThresholdMinutes,5);
 assert.equal(messages.at(-1).type,'liveThresholdPreferences');assert.ok(messages.at(-1).warningMinutes<messages.at(-1).prolongedMinutes);
 const pause=statElement('ucx-pause-threshold');assert.equal(pause.type,'range');assert.equal(pause.min,'5');assert.equal(pause.max,'180');assert.equal(pause.step,'1');pause.value='61';fire(pause,'input');assert.equal(statisticsSettings.contestPauseThresholdMinutes,61);assert.equal(statElement('ucx-pause-value').textContent,'61 min');
}
if(scenario.startsWith('threshold-')){
 const warning=timelineSettingsHost.querySelector('#ucx-live-warning'),prolonged=timelineSettingsHost.querySelector('#ucx-live-prolonged');
 const pair=()=>[statisticsSettings.liveWarningThresholdMinutes,statisticsSettings.liveProlongedGapThresholdMinutes];
 const saved=()=>messages.filter(m=>m.type==='liveThresholdPreferences');
 const positions=JSON.stringify(Object.fromEntries(Object.values(panelKeys).map(k=>[k[0],statisticsSettings[k[0]]])));
 const duration=statisticsSettings.liveTimelineMinutes,bucket=statisticsSettings.liveTimelineBucketMinutes,invalidations=map.invalidations;
 for(const slider of [warning,prolonged])slider.getBoundingClientRect=()=>({left:100,width:200});
 function down(slider=warning,x=200,extra={}){return fire(slider,'pointerdown',{clientX:x,buttons:1,...extra})}
 function move(slider,x,extra={}){return fire(slider,'pointermove',{clientX:x,buttons:1,...extra})}
 function input(slider,value){slider.value=value;return fire(slider,'input')}
 function verify(){
  const [w,p]=pair();assert.ok(w>=1&&w<=179);assert.ok(p>=2&&p<=180);assert.ok(w<p);
  assert.equal(Number(warning.min),1);assert.equal(Number(warning.max),179);assert.equal(Number(prolonged.min),2);assert.equal(Number(prolonged.max),180);
 }
 if(scenario==='threshold-fixed-limits'){
  assert.deepEqual(pair(),[10,15]);verify();assert.equal(saved().length,0);
  for(const [slider,value,expected] of [[warning,-1000,[1,15]],[prolonged,-1000,[1,2]],[warning,1000,[179,180]],[prolonged,1000,[179,180]],[warning,20,[20,180]],[prolonged,5,[4,5]]]){
   input(slider,value);assert.deepEqual(pair(),expected);verify();
  }
  assert.equal(warning.step,'1');assert.equal(prolonged.step,'1');
  assert.equal(warning.getAttribute('aria-describedby'),'ucx-live-threshold-note');
 }
 if(scenario==='threshold-edge-overflow'){
  const start=down();assert.ok(start.prevented&&start.stopped);assert.equal(warning.capturedPointer,1);assert.equal(document.activeElement,warning);
  for(let i=0;i<1300;i++){
   const e=move(warning,i%2?100000:301);assert.ok(e.prevented&&e.stopped);assert.deepEqual(pair(),[179,180]);verify();
  }
  assert.equal(saved().length,0);fire(warning,'pointerup');assert.equal(saved().length,1);assert.equal(warning.capturedPointer,null);assert.equal(liveThresholdDrag,null);
  for(let i=0;i<20;i++){
   down(prolonged);for(const x of [-10000,100000,-2,100000,101,299]){move(prolonged,x);verify()}
   move(prolonged,-10000);fire(prolonged,'pointerup');assert.deepEqual(pair(),[1,2]);
   down(warning);move(warning,100000);fire(warning,'pointerup');assert.deepEqual(pair(),[179,180]);
  }
 }
 if(scenario==='threshold-outside-release'){
  down();move(warning,100000);const other=event(warning,{pointerId:2});windowEvents.get('pointerup')(other);assert.ok(liveThresholdDrag);
  const up=event(new Element(),{clientX:100000,clientY:-2000});windowEvents.get('pointerup')(up);
  assert.ok(up.stopped);assert.equal(liveThresholdDrag,null);assert.equal(warning.capturedPointer,null);assert.equal(saved().length,1);
  move(warning,-100000);assert.deepEqual(pair(),[179,180]);assert.equal(saved().length,1);
  down(prolonged);move(prolonged,-10000);windowEvents.get('pointerup')(event(new Element(),{clientX:-10000,clientY:3000}));assert.deepEqual(pair(),[1,2]);verify();
 }
 if(['threshold-pointercancel','threshold-lostpointercapture','threshold-blur','threshold-pagehide','threshold-hidden','threshold-escape'].includes(scenario)){
  down();move(warning,10000);assert.deepEqual(pair(),[179,180]);assert.equal(saved().length,0);
  if(scenario==='threshold-pointercancel')windowEvents.get('pointercancel')(event(warning));
  if(scenario==='threshold-lostpointercapture')warning.releasePointerCapture(1);
  if(scenario==='threshold-blur')windowEvents.get('blur')();
  if(scenario==='threshold-pagehide')windowEvents.get('pagehide')();
  if(scenario==='threshold-hidden'){document.hidden=true;documentEvents.get('visibilitychange')();document.hidden=false}
  if(scenario==='threshold-escape'){const key=fire(warning,'keydown',{key:'Escape'});assert.ok(key.prevented)}
  assert.deepEqual(pair(),[10,15]);assert.equal(saved().length,0);assert.equal(liveThresholdDrag,null);assert.equal(warning.capturedPointer,null);
  move(warning,10000);assert.deepEqual(pair(),[10,15]);
  down();move(warning,10000);fire(warning,'pointerup');assert.deepEqual(pair(),[179,180]);assert.equal(saved().length,1);
 }
 if(scenario==='threshold-missed-release'){
  down();move(warning,10000);move(warning,-10000,{buttons:0});assert.equal(liveThresholdDrag,null);assert.deepEqual(pair(),[179,180]);assert.equal(saved().length,1);
  move(warning,-10000);assert.deepEqual(pair(),[179,180]);down(prolonged,100);fire(prolonged,'pointerup');assert.deepEqual(pair(),[1,2]);
 }
 if(scenario==='threshold-invalid-input'){
  for(const invalid of [NaN,Infinity,-Infinity,undefined,null,'',' ','abc','1.5','NaN','Infinity',{},[],true,false,1.5]){
   assert.equal(applyLiveThreshold('warning',invalid),false);assert.equal(applyLiveThreshold('prolonged',invalid),false);
   input(warning,invalid);input(prolonged,invalid);assert.deepEqual(pair(),[10,15]);assert.equal(saved().length,0);verify();
  }
  assert.equal(applyLiveThreshold('unknown',20),false);
  down();const preview=pair();input(warning,1439);assert.deepEqual(pair(),preview);assert.equal(saved().length,0);fire(warning,'pointercancel');
  input(warning,20);assert.deepEqual(pair(),[20,21]);assert.equal(saved().length,1);
 }
 if(scenario==='threshold-invalid-pointer'){
  for(const extra of [{clientX:NaN},{clientX:Infinity},{clientX:'9999'},{button:2},{isPrimary:false}]){down(warning,200,extra);assert.equal(liveThresholdDrag,null);assert.deepEqual(pair(),[10,15])}
  for(const rect of [{left:NaN,width:200},{left:100,width:Infinity},{left:100,width:0},{left:100,width:-1}]){
   warning.getBoundingClientRect=()=>rect;down();assert.equal(liveThresholdDrag,null);
  }
  warning.getBoundingClientRect=()=>({left:100,width:200});
  const capture=warning.setPointerCapture;warning.setPointerCapture=()=>{throw Error('capture unavailable')};down();assert.equal(liveThresholdDrag,null);warning.setPointerCapture=capture;
  down();const before=pair();for(const x of [NaN,Infinity,-Infinity,'10000'])move(warning,x);assert.deepEqual(pair(),before);
  move(warning,10000,{pointerId:2});fire(warning,'pointerup',{pointerId:2});assert.deepEqual(pair(),before);assert.ok(liveThresholdDrag);
  fire(warning,'pointercancel');assert.deepEqual(pair(),[10,15]);assert.equal(saved().length,0);
 }
 if(scenario==='threshold-wide-restart'){
  const original={...statisticsSettings};assert.deepEqual(pair(),[1439,1440]);
  assert.equal(Number(warning.value),179);assert.equal(Number(prolonged.value),180);assert.equal(Number(warning.max),179);assert.equal(Number(prolonged.max),180);
  const note=timelineSettingsHost.querySelector('#ucx-live-threshold-note');assert.ok(note.textContent.includes('1439'));assert.ok(note.textContent.includes('1440'));assert.equal(saved().length,0);
  window.ucxSetStatisticsSettings(original);assert.deepEqual(pair(),[1439,1440]);assert.equal(saved().length,0);
  down();move(warning,10000);fire(warning,'pointercancel');assert.deepEqual(pair(),[1439,1440]);assert.equal(saved().length,0);
  input(prolonged,120);assert.deepEqual(pair(),[119,120]);assert.equal(saved().length,1);verify();
  window.ucxSetStatisticsSettings({...statisticsSettings});assert.deepEqual(pair(),[119,120]);assert.equal(Number(warning.max),179);assert.ok(!note.textContent.includes('1439'));
 }
 if(scenario==='threshold-settings-update'){
  const incoming={...statisticsSettings,liveWarningThresholdMinutes:7,liveProlongedGapThresholdMinutes:12,shortpathWidth:3.5};
  down();move(warning,10000);window.ucxSetStatisticsSettings(incoming);assert.deepEqual(pair(),[179,180]);assert.equal(statisticsSettings.shortpathWidth,3.5);
  assert.equal(incoming.liveWarningThresholdMinutes,7);fire(warning,'pointercancel');assert.deepEqual(pair(),[7,12]);assert.equal(saved().length,0);
  down();move(warning,10000);window.ucxSetStatisticsSettings(incoming);fire(warning,'pointerup');assert.deepEqual(pair(),[179,180]);assert.equal(saved().length,1);
  const message=saved()[0];assert.deepEqual(Object.keys(message).sort(),['prolongedMinutes','type','warningMinutes']);
 }
 for(const message of saved()){assert.ok(message.warningMinutes>=1&&message.warningMinutes<=179);assert.ok(message.prolongedMinutes>=2&&message.prolongedMinutes<=180);assert.ok(message.warningMinutes<message.prolongedMinutes)}
 assert.equal(JSON.stringify(Object.fromEntries(Object.values(panelKeys).map(k=>[k[0],statisticsSettings[k[0]]]))),positions);
 assert.equal(statisticsSettings.liveTimelineMinutes,duration);assert.equal(statisticsSettings.liveTimelineBucketMinutes,bucket);assert.equal(map.invalidations,invalidations);
 assert.equal(messages.filter(m=>m.type==='view').length,0);assert.equal(messages.filter(m=>m.type==='statisticsPreferences').length,0);
 for(const panel of mapPanels.values())assert.equal(panel.drag,null);
}
if(scenario==='snap-escape'){
 const slider=timelineSettingsHost.querySelector('#ucx-timeline-duration');fire(slider,'pointerdown');
 for(const [raw,expected] of [[56,56],[57,60],[60,60],[63,60],[64,64],[117,120],[123,120],[124,124],[177,180],[183,180],[184,184],[237,240]]){slider.value=raw;fire(slider,'input');assert.equal(statisticsSettings.liveTimelineMinutes,expected);assert.equal(slider.value,raw)}
 slider.value=64;fire(slider,'pointerup');assert.equal(statisticsSettings.liveTimelineMinutes,64);assert.equal(messages.at(-1).settings.liveTimelineMinutes,64);
 assert.equal(magneticTimelineDuration(61,true,false),61);assert.equal(magneticTimelineDuration(61,false,true),61);
}
if(scenario==='visibility-runtime'){
 eval(timelineScript);assert.equal(timelineHost.hidden,false);assert.equal(timelineModel(now,60,5).count,3);
 setTimelinePresentation('liveTimelineVisible',false);assert.equal(timelineHost.hidden,true);setTimelinePresentation('liveTimelineVisible',true);assert.equal(timelineHost.hidden,false);
 setTimelinePresentation('liveTimelineSettingsVisible',false);assert.equal(timelineSettingsHost.style.display,'none');fire(timelineSettingsCheckbox,'change',{target:{checked:true}});assert.equal(timelineSettingsHost.style.display,'block');fire(timelineElement('ucx-timeline-resize'),'keydown',{key:'F2'});assert.equal(statisticsSettings.liveTimelineSettingsCollapsed,false);
 const position=statisticsSettings.timelinePanelPosition={x:.2,y:.3};window.ucxSetLiveTimeline({...timelineSnapshot,samples:[]});assert.deepEqual(statisticsSettings.timelinePanelPosition,position);assert.equal(statisticsSettings.liveTimelineMinutes,60);
}
if(scenario.startsWith('height-')){
 const handle=timelineElement('ucx-timeline-resize'),presentations=()=>messages.filter(m=>m.type==='mapPresentation');
 const initialPositions=JSON.stringify(Object.fromEntries(Object.values(panelKeys).map(k=>[k[0],statisticsSettings[k[0]]])));
 const initialDuration=statisticsSettings.liveTimelineMinutes,initialBucket=statisticsSettings.liveTimelineBucketMinutes;
 const setHeight=(n)=>{statisticsSettings.liveTimelineHeightPx=n;window.ucxApplyTimelineSettings()};
 if(scenario==='height-default'){
  assert.equal(timelineEffectiveHeight,148);assert.equal(statisticsSettings.liveTimelineHeightPx,null);assert.equal(handle.getAttribute('role'),'separator');
  setViewport(600,848);windowEvents.get('resize')();assert.equal(timelineEffectiveHeight,174);assert.equal(statisticsSettings.liveTimelineHeightPx,null);
  assert.equal(presentations().length,0);
 }
 if(scenario==='height-restart'){
  assert.equal(timelineEffectiveHeight,260);assert.equal(statisticsSettings.liveTimelineHeightPx,260);
  assert.equal(handle.getAttribute('aria-valuenow'),'260');assert.equal(presentations().length,0);
 }
 if(scenario==='height-pointer'){
  const begin=fire(handle,'pointerdown');assert.equal(begin.prevented,true);assert.equal(begin.stopped,true);assert.equal(handle.capturedPointer,1);
  fire(handle,'pointermove',{clientY:48});assert.equal(timelineEffectiveHeight,200);assert.equal(presentations().length,0);
  fire(handle,'pointermove',{pointerId:2,clientY:0});assert.equal(timelineEffectiveHeight,200);
  fire(handle,'pointerup');assert.equal(presentations().at(-1).settings.liveTimelineHeightPx,200);assert.equal(timelineHeightDrag,null);
  const before=presentations().length;fire(handle,'pointerdown');fire(handle,'pointerup');assert.equal(presentations().length,before);
  fire(handle,'pointerdown');fire(handle,'pointermove',{clientY:60});fire(handle,'pointercancel');assert.equal(timelineEffectiveHeight,200);assert.equal(presentations().length,before);
  fire(handle,'pointerdown');fire(handle,'pointermove',{clientY:40});fire(handle,'lostpointercapture');assert.equal(timelineEffectiveHeight,200);assert.equal(presentations().length,before);
  for(const panel of mapPanels.values())assert.equal(panel.drag,null);
 }
 if(scenario==='height-keyboard'){
  let e=fire(handle,'keydown',{key:'ArrowUp'});assert.ok(e.stopped&&e.prevented);assert.equal(timelineEffectiveHeight,158);
  fire(handle,'keydown',{key:'ArrowDown'});assert.equal(timelineEffectiveHeight,148);
  fire(handle,'keydown',{key:'ArrowUp',shiftKey:true});assert.equal(timelineEffectiveHeight,149);
  const horizontal=fire(handle,'keydown',{key:'ArrowRight'});assert.ok(horizontal.stopped&&horizontal.prevented);assert.equal(timelineEffectiveHeight,149);
  fire(handle,'keydown',{key:'Home'});assert.equal(timelineEffectiveHeight,20);fire(handle,'keydown',{key:'End'});assert.equal(timelineEffectiveHeight,320);
  assert.equal(presentations().at(-1).settings.liveTimelineHeightPx,320);
 }
 if(scenario==='height-limits'){
  fire(handle,'pointerdown');fire(handle,'pointermove',{clientY:-1000});fire(handle,'pointerup');assert.equal(timelineEffectiveHeight,320);
  fire(handle,'pointerdown');fire(handle,'pointermove',{clientY:1000});fire(handle,'pointerup');assert.equal(timelineEffectiveHeight,20);
  assert.equal(handle.getAttribute('aria-valuemin'),'20');assert.equal(handle.getAttribute('aria-valuemax'),'320');
 }
 if(scenario==='height-viewport'){
  setHeight(300);assert.equal(timelineEffectiveHeight,300);
  setViewport(1000,200);windowEvents.get('resize')();assert.equal(timelineEffectiveHeight,120);assert.equal(statisticsSettings.liveTimelineHeightPx,300);
  assert.equal(map.getContainer().clientHeight,80);assert.ok(handle.getAttribute('aria-valuetext').includes('300'));
  setViewport(600,120);windowEvents.get('resize')();assert.equal(timelineEffectiveHeight,40);assert.equal(statisticsSettings.liveTimelineHeightPx,300);
  assert.equal(handle.getAttribute('aria-valuemax'),'40');
  setViewport(1920,1080);windowEvents.get('resize')();assert.equal(timelineEffectiveHeight,300);assert.equal(statisticsSettings.liveTimelineHeightPx,300);
  assert.equal(presentations().length,0);
 }
 if(scenario==='height-scaling'){
  setSeries([[now-5*minute,2],[now-10*minute,4],[now-15*minute,1]],last(now-5*minute));
  function geometry(){const chart=timelineElement('ucx-timeline-chart');return {viewBox:chart.getAttribute('viewBox'),bars:[...chart.innerHTML.matchAll(/<rect x="([^"]*)" y="([^"]*)" width="([^"]*)" height="([^"]*)" fill="#287f78"/g)].map(m=>({x:Number(m[1]),width:Number(m[3]),height:Number(m[4])})),labels:[...chart.innerHTML.matchAll(/font-size="([^"]*)"/g)].map(m=>m[1])}}
  setHeight(80);const small=geometry();setHeight(148);const original=geometry();setHeight(320);const large=geometry();
  assert.equal(small.bars.length,3);assert.equal(large.bars.length,3);
  for(let i=0;i<3;i++){assert.equal(original.bars[i].x,large.bars[i].x);assert.equal(original.bars[i].width,large.bars[i].width);assert.ok(small.bars[i].height<original.bars[i].height);assert.ok(original.bars[i].height<large.bars[i].height)}
  for(const g of [small,original,large]){assert.equal(g.bars[0].height/g.bars[1].height,.25);assert.equal(g.bars[2].height/g.bars[1].height,.5);assert.ok(g.labels.every(x=>x==='11'))}
  assert.equal(Number(large.viewBox.split(' ')[3]),timelineElement('ucx-timeline-chart').clientHeight);
 }
 if(scenario==='height-settings-update'){
  const old=JSON.parse(JSON.stringify(statisticsSettings));fire(handle,'pointerdown');fire(handle,'pointermove',{clientY:28});assert.equal(timelineEffectiveHeight,220);
  window.ucxSetStatisticsSettings(old);assert.equal(statisticsSettings.liveTimelineHeightPx,220);assert.equal(timelineEffectiveHeight,220);
  fire(handle,'pointerup');eval(timelineScript);assert.equal(timelineEffectiveHeight,220);assert.equal(presentations().at(-1).settings.liveTimelineHeightPx,220);
 }
 if(scenario==='height-reset'){
  setHeight(280);fire(handle,'dblclick');assert.equal(timelineEffectiveHeight,148);assert.equal(statisticsSettings.liveTimelineHeightPx,null);assert.equal(presentations().at(-1).settings.liveTimelineHeightPx,null);
 }
 assert.equal(JSON.stringify(Object.fromEntries(Object.values(panelKeys).map(k=>[k[0],statisticsSettings[k[0]]]))),initialPositions);
 assert.equal(statisticsSettings.liveTimelineMinutes,initialDuration);assert.equal(statisticsSettings.liveTimelineBucketMinutes,initialBucket);
 assert.equal(messages.filter(m=>m.type==='view').length,0);assert.equal(map.sizeOptions?.pan,true);assert.equal(map.sizeOptions?.animate,false);
}
if(scenario.startsWith('mode-')){
 const resize=timelineElement('ucx-timeline-resize'),heading=timelineElement('ucx-timeline-heading'),legend=timelineElement('ucx-timeline-footnote'),chart=timelineElement('ucx-timeline-chart');
 function setHeight(height){statisticsSettings.liveTimelineHeightPx=height;window.ucxApplyTimelineSettings()}
 if(scenario==='mode-startup'){assert.equal(heading.hidden,true);assert.equal(chart.innerHTML.includes('<text '),false)}
 setSeries([[now-5*minute,2],[now-10*minute,4],[now-15*minute,1]],last(now-5*minute));
 const expectedCount=timelineModel(now,60,5).count;
 function verify(height,mode){
  setHeight(height);assert.equal(timelineEffectiveHeight,height);assert.equal(timelineHost.dataset.presentationMode,mode);
  assert.equal(heading.hidden,mode!=='full');assert.equal(legend.hidden,mode!=='full');assert.equal(timelineElement('ucx-timeline-config'),null);
  assert.equal(resize.hidden,false);assert.equal(resize.getAttribute('role'),'separator');assert.equal(resize.getAttribute('tabindex'),'0');
  assert.equal(resize.getAttribute('aria-valuemin'),'20');assert.equal(resize.getAttribute('aria-valuemax'),'320');
  const texts=[...chart.innerHTML.matchAll(/<text /g)].length;
  if(mode==='full')assert.ok(texts>=3);else assert.equal(texts,mode==='miniature'?0:2);
  assert.equal(timelineModel(now,60,5).count,expectedCount);
 }
 if(scenario==='mode-boundaries')for(const [height,mode] of [[20,'miniature'],[39,'miniature'],[40,'compact'],[99,'compact'],[100,'full'],[320,'full']])verify(height,mode);
 if(scenario==='mode-transitions'){
  for(const height of [148,20,40,100,39,99,320,20])verify(height,timelinePresentationMode(height));
  const positions=JSON.stringify(statisticsSettings.timelinePanelPosition);fire(mapPanels.get('timeline').collapse,'click');assert.equal(timelineHost.dataset.presentationMode,'miniature');
  movePanel('timeline',-50,-50);assert.notEqual(JSON.stringify(statisticsSettings.timelinePanelPosition),positions);
  setTimelinePresentation('liveTimelineMinutes',120);setTimelinePresentation('liveTimelineBucketMinutes',2);assert.equal(timelineHost.dataset.presentationMode,'miniature');
  const saved=statisticsSettings.liveTimelineHeightPx;eval(timelineScript);assert.equal(statisticsSettings.liveTimelineHeightPx,saved);assert.equal(timelineHost.dataset.presentationMode,'miniature');
 }
 if(scenario==='mode-scaling'){
  for(const height of [20,39,40,99,100,320]){
   setHeight(height);
   const bars=[...chart.innerHTML.matchAll(/<rect x="([^"]*)" y="([^"]*)" width="([^"]*)" height="([^"]*)" fill="#287f78"/g)];
   assert.equal(bars.length,3);const heights=bars.map(x=>Number(x[4]));assert.equal(heights[0]/heights[1],.25);assert.equal(heights[2]/heights[1],.5);
   assert.equal(Number(chart.getAttribute('viewBox').split(' ')[3]),chart.clientHeight);
   if(height<40){assert.equal(Math.max(...heights),chart.clientHeight);assert.ok(chart.clientHeight>=height*.95);assert.equal(chart.innerHTML.includes('<text '),false)}
  }
 }
 if(scenario==='mode-settings-access'){
  verify(20,'miniature');setTimelinePresentation('liveTimelineSettingsVisible',false);statisticsSettings.liveTimelineSettingsCollapsed=true;window.ucxApplyPanelSettings();
  const keyboard=fire(resize,'keydown',{key:'F2'});assert.ok(keyboard.stopped&&keyboard.prevented);
  assert.equal(statisticsSettings.liveTimelineSettingsVisible,true);assert.equal(statisticsSettings.liveTimelineSettingsCollapsed,false);
  assert.equal(document.activeElement,timelineSettingsHost.querySelector('#ucx-timeline-duration'));assert.equal(timelineHost.dataset.presentationMode,'miniature');
  verify(100,'full');resize.focus();verify(20,'miniature');assert.equal(document.activeElement,resize);
  setNow(now-300*minute);updateLiveActivity();assert.ok(chart.getAttribute('aria-label').startsWith('Mindestens'));assert.ok(chart.innerHTML.includes('unvollständig'));
 }
 if(scenario==='mode-visibility'){
  assert.ok(!timelineSettingsHost.innerHTML.includes('ucx-timeline-visible'));assert.ok(!timelineSettingsHost.innerHTML.includes('QSO-Verlauf anzeigen'));
  for(const height of [20,80,148]){
   setHeight(height);fire(timelineVisibilityCheckbox,'change',{target:{checked:false}});assert.equal(timelineHost.hidden,true);
   fire(timelineSettingsHost.querySelector('#ucx-timeline-defaults'),'click');assert.equal(timelineHost.hidden,true);assert.equal(statisticsSettings.liveTimelineVisible,false);
   fire(timelineVisibilityCheckbox,'change',{target:{checked:true}});assert.equal(timelineHost.hidden,false);
  }
 }
 if(scenario==='mode-viewport'){
  setHeight(300);const before=messages.filter(x=>x.type==='mapPresentation').length;
  for(const [area,height,mode] of [[119,39,'miniature'],[120,40,'compact'],[179,99,'compact'],[180,100,'full'],[1080,300,'full']]){
   setViewport(1920,area);windowEvents.get('resize')();assert.equal(timelineEffectiveHeight,height);assert.equal(timelineHost.dataset.presentationMode,mode);assert.equal(statisticsSettings.liveTimelineHeightPx,300);
  }
  assert.equal(messages.filter(x=>x.type==='mapPresentation').length,before);
 }
 if(scenario==='mode-keyboard'){
  verify(40,'compact');fire(resize,'keydown',{key:'ArrowDown',shiftKey:true});assert.equal(timelineEffectiveHeight,39);assert.equal(timelineHost.dataset.presentationMode,'miniature');
  fire(resize,'keydown',{key:'ArrowUp',shiftKey:true});assert.equal(timelineEffectiveHeight,40);verify(100,'full');fire(resize,'keydown',{key:'ArrowDown',shiftKey:true});assert.equal(timelineEffectiveHeight,99);assert.equal(timelineHost.dataset.presentationMode,'compact');
  fire(resize,'keydown',{key:'Home'});assert.equal(timelineEffectiveHeight,20);fire(resize,'keydown',{key:'End'});assert.equal(timelineEffectiveHeight,320);fire(resize,'dblclick');assert.equal(timelineEffectiveHeight,148);
 }
 if(scenario==='mode-quiet-timer'){
  setHeight(20);const tick=timers.find(x=>x.ms===1000).fn;advance(60);tick();assert.equal(timelineHost.dataset.presentationMode,'miniature');assert.equal(chart.innerHTML.includes('<text '),false);
  assert.equal(liveActivityState().seconds,6*60);setSeries([[Date.now(),1]],last(Date.now(),'NEW'));assert.equal(liveActivityState().seconds,0);assert.equal(timelineHost.dataset.presentationMode,'miniature');
 }
 if(scenario==='mode-startup'){
  assert.ok(html.includes('data-presentation-mode="miniature"'));assert.ok(html.includes('--ucx-timeline-height:20px'));
 }
 // UI preferences may change; map view, statistics, geometry and style must not.
}
if(scenario==='settings-panel-control'){
 const footer=html.split('<section id="ucx-live-timeline"')[1].split('</section>')[0];
 assert.ok(!footer.includes('<button'));assert.ok(!html.includes('.ucx-timeline-heading button'));
 const pane=mapPanels.get('timeline'),duration=timelineSettingsHost.querySelector('#ucx-timeline-duration');
 for(const height of [20,80,148,320]){
  statisticsSettings.liveTimelineHeightPx=height;window.ucxApplyTimelineSettings();
  fire(timelineSettingsCheckbox,'change',{target:{checked:false}});assert.equal(timelineSettingsHost.style.display,'none');
  const timelineVisible=timelineHost.hidden,collapsed=statisticsSettings.liveTimelineSettingsCollapsed;
  fire(timelineSettingsCheckbox,'change',{target:{checked:true}});assert.equal(timelineSettingsHost.style.display,'block');
  assert.equal(statisticsSettings.liveTimelineSettingsCollapsed,collapsed);assert.equal(timelineHost.hidden,timelineVisible);
  const before=statisticsSettings.liveTimelineSettingsCollapsed;fire(pane.collapse,'click');assert.equal(statisticsSettings.liveTimelineSettingsCollapsed,!before);
  fire(timelineElement('ucx-timeline-resize'),'keydown',{key:'F2'});assert.equal(statisticsSettings.liveTimelineSettingsCollapsed,false);assert.equal(document.activeElement,duration);
  movePanel('timeline',-20,-20);const position=JSON.stringify(statisticsSettings.timelinePanelPosition);
  fire(timelineSettingsCheckbox,'change',{target:{checked:false}});fire(timelineSettingsCheckbox,'change',{target:{checked:true}});
  assert.equal(JSON.stringify(statisticsSettings.timelinePanelPosition),position);
  assert.equal(messages.at(-1).type,'mapPresentation');assert.equal(messages.at(-1).settings.liveTimelineSettingsVisible,true);
 }
 assert.equal(timelineElement('ucx-timeline-config'),null);
}
if(scenario==='render-performance'){
 const rows=[];for(let i=0;i<14400;i++)rows.push([now-i*1000,7]);setSeries(rows,last());
 statisticsSettings.liveTimelineMinutes=240;statisticsSettings.liveTimelineBucketMinutes=1;
 const started=performance.now();for(let i=0;i<100;i++)renderLiveTimeline(now+i*1000);const ms=performance.now()-started;
 console.log('Timeline rendering: '+(ms/100).toFixed(3)+' ms/frame; 14,400 samples / 100,800 QSOs, 100 renders');
 assert.equal(timelineModel(now,240,1).count,100800);
}
assert.equal(fingerprint(),baseline);assert.deepEqual([...map.layers],layers);assert.deepEqual(paths.map(x=>({...x.options})),styles);
assert.equal(map.getPane('ucxDxccPane').style.zIndex,'320');assert.equal(map.getPane('ucxPathPane').style.zIndex,'410');assert.equal(map.getPane('ucxMarkerPane').style.zIndex,'430');
console.log('Passed map UI scenario: '+scenario);
`));
