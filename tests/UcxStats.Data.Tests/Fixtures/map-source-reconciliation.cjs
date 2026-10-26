'use strict';
const data=JSON.parse(require('node:fs').readFileSync(0,'utf8'));
const {run}=require('./map-runtime.cjs').createRuntime(data);
setImmediate(()=>run(String.raw`
map.setView([52,9],6);showLong=true;showPaths=true;window.ucxSetDxccVisible(false);
getBandLayer(16,'Microwave').enabled=false;
statisticsSettings.timelinePanelPosition={x:.2,y:.3};statisticsSettings.liveTimelineHeightPx=39;
statisticsSettings.liveTimelineMinutes=120;statisticsSettings.liveTimelineBucketMinutes=2;
window.ucxApplyPanelSettings();window.ucxApplyTimelineSettings();
const view=JSON.stringify([map.center,map.zoom,map.viewCalls,map.fitCalls]);
const settings=JSON.stringify(statisticsSettings),bands=JSON.stringify([...bandLayers].map(([id,b])=>[id,b.enabled]));
const dxcc=dxccLayer;
for(const state of states){
 eval(state.script);
 assert.equal(selectedStatistics().totalQsos,state.total);assert.equal(selectedStatistics().mappableQsos,state.total);
 assert.equal(timelineModel(Date.now(),120,2).count,state.total);assert.equal(liveActivity?.callSign??null,state.latest);
 const markers=[...bandLayers.values()].reduce((n,b)=>n+b.points.layers.size,0);
 assert.equal(markers,state.total*3);
 if(state.total===0){
  assert.equal(liveActivityState().status,'unknown');assert.equal(selectedStatistics().odx,null);
  assert.equal([...bandLayers.values()].reduce((n,b)=>n+b.paths.layers.size+b.longPaths.layers.size,0),0);
  assert.ok(timelineElement('ucx-timeline-activity').innerHTML.includes('Noch kein QSO'));
 }
 assert.equal(JSON.stringify([map.center,map.zoom,map.viewCalls,map.fitCalls]),view);
 assert.equal(JSON.stringify(statisticsSettings),settings);assert.equal(JSON.stringify([...bandLayers].map(([id,b])=>[id,b.enabled])),bands);
 assert.equal(showDxcc,false);assert.equal(dxccLayer,dxcc);assert.equal(showLong,true);assert.equal(showPaths,true);
 assert.equal(timelineHost.dataset.presentationMode,'miniature');
}
const tick=timers.find(t=>t.ms===1000).fn;advance(1);tick();assert.equal(liveActivityState().status,'unknown');
assert.equal(messages.filter(m=>m.type==='view').length,0);
console.log('Actual UCX import → SQLite → Live contracts: 2 → 1 → 0; state preserved');
`));
