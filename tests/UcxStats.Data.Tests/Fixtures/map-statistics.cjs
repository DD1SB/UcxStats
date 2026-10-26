'use strict';
const fs=require('node:fs');
const vm=require('node:vm');
const data=JSON.parse(fs.readFileSync(0,'utf8'));
const {sandbox}=require('./map-runtime.cjs').createRuntime(data);
setImmediate(() => {
 vm.runInContext(`
assert.equal(statisticsHost.style.display,'none');
assert.equal(statElement('ucx-contest-section').style.display,'none');
assert.equal(timelineHost!==null,isLive);
assert.ok(!statisticsHost.innerHTML.includes('ucx-live-activity'));
assert.equal(timers.filter(x=>x.ms===1000).length,isLive?1:0);
// Snapshot includes a 10-GHz band with no QSO marker and locator-less totals.
const initialBandCount=bandLayers.size;
eval(updateScript);
assert.equal(bandLayers.size,initialBandCount+1);
assert.equal(selectedStatistics().totalQsos,5);
assert.equal(selectedStatistics().mappableQsos,2);
assert.equal(selectedStatistics().dxccs,2);
assert.equal(selectedStatistics().majorFields,2);
assert.equal(selectedStatistics().grids,2);
assert.equal(selectedStatistics().odx.callSign,'ODX');
assert.ok(statElement('ucx-stat-summary').innerHTML.includes('Nicht kartierbar'));
// Actual band change handler gives an immediate exact union.
function toggleBand(code,enabled){
 const cb=bandControlHost.querySelectorAll('.ctl-band').find(x=>Number(x.dataset.bandCode)===code);
 assert.ok(cb);assert.ok(cb.listeners.has('change'));
 cb.checked=enabled;cb.listeners.get('change')({target:cb});
}
const shared=statisticsSnapshot.dxccMemberships.find(x=>x.bandCodes.length===2);
assert.equal(shared.count,1);
toggleBand(16,false);
assert.equal(selectedStatistics().totalQsos,4);
assert.equal(selectedStatistics().dxccs,1);
assert.equal(selectedStatistics().majorFields,1);
assert.equal(selectedStatistics().grids,1);
const contestBefore=JSON.stringify(statisticsSnapshot.contest);
const contestHtml=statElement('ucx-stat-contest').innerHTML;
for(const b of bandLayers.values())toggleBand(b.code,false);
assert.equal(selectedStatistics().noBands,true);
assert.equal(selectedStatistics().totalQsos,0);
assert.equal(selectedStatistics().dxccs,0);
assert.equal(selectedStatistics().odx,null);
assert.ok(statElement('ucx-stat-summary').innerHTML.includes('Keine Bänder ausgewählt'));
assert.equal(JSON.stringify(statisticsSnapshot.contest),contestBefore);
assert.equal(statElement('ucx-stat-contest').innerHTML,contestHtml);
for(const b of bandLayers.values())toggleBand(b.code,true);

window.ucxSetStatisticsVisible(true);
assert.equal(statisticsHost.style.display,'block');assert.equal(statisticsCheckbox.checked,true);
statisticsCheckbox.listeners.get('change')({target:{checked:false}});
assert.equal(statisticsHost.style.display,'none');
assert.equal(messages.at(-1).type,'statisticsVisibility');
// Contest preference editing remains in the statistics panel.
statElement('ucx-contest-visible').checked=true;
statElement('ucx-allowance').value='2160';statElement('ucx-pause-threshold').value='31';
saveStatisticsPreferences();
assert.equal(messages.at(-1).type,'statisticsPreferences');
assert.equal(messages.at(-1).settings.operatingTimeAllowanceMinutes,2160);
assert.equal(statElement('ucx-contest-section').style.display,'block');

// Resident geometry, selected bands, layer styles, view, and synchronization survive updates.
map.setView([15,165],5);bandLayers.get('2').enabled=false;
showPaths=false;showLong=true;showNight=true;refreshAll();
const paths=[...bandLayers.values()].flatMap(b=>[...b.paths.layers,...b.longPaths.layers]);
const before=JSON.stringify({center:map.center,zoom:map.zoom,viewCalls:map.viewCalls,fitCalls:map.fitCalls,selection:[...bandLayers.values()].map(b=>b.enabled),showPaths,showLong,showNight,dxccStatus});
const visible=[...map.layers],options=paths.map(x=>({...x.options}));
const updated=JSON.parse(JSON.stringify(statisticsSnapshot));updated.bands[1].summary.totalQsos=10;
window.ucxSetMapStatistics(updated);
assert.equal(selectedStatistics().totalQsos,11);
assert.equal(JSON.stringify({center:map.center,zoom:map.zoom,viewCalls:map.viewCalls,fitCalls:map.fitCalls,selection:[...bandLayers.values()].map(b=>b.enabled),showPaths,showLong,showNight,dxccStatus}),before);
assert.deepEqual([...map.layers],visible);
assert.deepEqual(paths,[...bandLayers.values()].flatMap(b=>[...b.paths.layers,...b.longPaths.layers]));
assert.deepEqual(paths.map(x=>({...x.options})),options);
assert.ok(map.events.has('moveend'));postView();assert.equal(messages.at(-1).type,'view');
window.ucxApplyRemoteView(30,40,6);assert.equal(map.zoom,6);

if(isLive){
 eval(activityScript);
 const tick=timers.find(x=>x.ms===1000).fn;
 assert.equal(liveActivityState().seconds,0);
 assert.equal(liveActivityState().status,'normal');
 advance(599);tick();assert.equal(liveActivityState().status,'normal');
 advance(1);tick();assert.equal(liveActivityState().status,'warning');
 assert.ok(timelineElement('ucx-timeline-activity').innerHTML.includes('10:00'));
 advance(300);tick();assert.equal(liveActivityState().status,'prolonged');
 assert.ok(timelineElement('ucx-timeline-activity').innerHTML.includes('Sehr lange QSO-Lücke'));
 const last={qsoId:50,callSign:'NEW',bandCode:16,timestampUtc:new Date(Date.now()).toISOString()};
 window.ucxSetLiveActivity(last);assert.equal(liveActivityState().seconds,0);
 assert.ok(timelineElement('ucx-timeline-activity').innerHTML.includes('Band des letzten QSOs: 10 GHz'));
 // Database-derived latest QSO reconstructs elapsed time after reconnect.
 window.ucxSetLiveActivity(null);advance(60);window.ucxSetLiveActivity(last);
 assert.equal(liveActivityState().seconds,60);
 window.ucxSetStatisticsSettings({...statisticsSettings,liveWarningThresholdMinutes:1,liveProlongedGapThresholdMinutes:2});
 assert.equal(liveActivityState().status,'warning');advance(60);tick();assert.equal(liveActivityState().status,'prolonged');
 window.ucxSetLiveActivity({...last,timestampUtc:new Date(Date.now()+3600000).toISOString()});
 assert.equal(liveActivityState().seconds,0);assert.equal(liveActivityState().future,true);
 assert.ok(timelineElement('ucx-timeline-activity').innerHTML.includes('Zukunft'));
 window.ucxSetLiveActivity(null);tick();assert.equal(liveActivityState().status,'unknown');
 window.ucxSetLiveActivity({...last,timestampUtc:'bad'});tick();assert.equal(liveActivityState().status,'unknown');
 assert.ok(!timelineElement('ucx-timeline-activity').innerHTML.includes('operator inactive'));
 assert.ok(!timelineElement('ucx-timeline-activity').innerHTML.includes('station paused'));
}
console.log('Passed statistics map runtime, isLive='+isLive);
`,sandbox,{timeout:5000});
});
