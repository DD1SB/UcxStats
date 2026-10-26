namespace UcxStats.Map;

internal static class LiveTimelinePanel
{
    public const string Styles = """
body{display:flex;flex-direction:column;overflow:hidden}#map{height:auto;flex:1;min-height:0;position:relative}
#ucx-live-timeline{flex:none;box-sizing:border-box;background:#f7faf9;border-top:1px solid #bacbc8;color:#243d42;padding:8px 12px 3px;height:var(--ucx-timeline-height,148px);max-height:max(0px,calc(100% - 80px));position:relative;display:flex;flex-direction:column;overflow:hidden;font-size:13px;line-height:1.3}
#ucx-live-timeline[hidden]{display:none}.ucx-timeline-heading{flex:none;display:flex;align-items:center;gap:6px 16px;flex-wrap:wrap;font-variant-numeric:tabular-nums}
#ucx-timeline-activity{flex:1;min-width:200px}#ucx-timeline-activity.warning{border-left:3px solid #a87517;padding-left:7px}#ucx-timeline-activity.prolonged{border-left:3px dashed #935b12;padding-left:7px}
#ucx-timeline-chart{height:0;flex:1 1 0;min-height:0;display:block;width:100%}.ucx-timeline-footnote{flex:none;display:flex;gap:10px;justify-content:space-between;color:#455b60;font-size:11px}
.ucx-timeline-settings{background:#fff;padding:8px 10px;border-radius:4px;box-shadow:0 1px 5px #777;font-size:13px;max-width:350px}
.ucx-timeline-settings label{display:block;margin:6px 0}.ucx-timeline-settings .ucx-note{font-size:12px;color:#536068;white-space:normal;max-width:310px}
.ucx-timeline-settings select{font:inherit;border:1px solid #a9b9bb;border-radius:3px;background:#fff;color:#243d42;padding:2px 6px}
@media(max-width:750px){#ucx-live-timeline{height:var(--ucx-timeline-height,174px);padding:8px 8px 3px}.ucx-timeline-footnote{font-size:10px}.ucx-timeline-heading{gap:3px 8px;font-size:12px}}
#ucx-timeline-resize{position:absolute;top:0;left:0;right:0;height:6px;cursor:ns-resize;touch-action:none;user-select:none;z-index:1}
#ucx-timeline-resize:after{content:'';display:block;width:40px;height:2px;margin:2px auto;background:#779a98;border-radius:2px;opacity:0}
#ucx-live-timeline:hover #ucx-timeline-resize:after,#ucx-timeline-resize:focus-visible:after{opacity:1}
#ucx-timeline-resize:hover,#ucx-timeline-resize:focus-visible{background:#d5e8e4;outline:2px solid #287f78;outline-offset:-2px}
.ucx-visually-hidden{position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%);white-space:nowrap}
#ucx-live-timeline[data-presentation-mode="miniature"],#ucx-live-timeline[data-presentation-mode="compact"]{padding:0}
#ucx-live-timeline .ucx-timeline-heading[hidden],#ucx-live-timeline .ucx-timeline-footnote[hidden]{display:none}
#ucx-live-timeline.ucx-timeline-tight .ucx-timeline-heading{display:grid;grid-template-columns:auto minmax(0,1fr);gap:1px 8px;font-size:12px}
#ucx-live-timeline.ucx-timeline-tight #ucx-timeline-activity{grid-row:2;grid-column:1/-1;min-width:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
#ucx-live-timeline.ucx-timeline-tight #ucx-timeline-count{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
#ucx-live-timeline.ucx-timeline-tight .ucx-timeline-footnote{white-space:nowrap;overflow:hidden;font-size:10px}
#ucx-live-timeline.ucx-timeline-tight .ucx-timeline-footnote span{min-width:0;overflow:hidden;text-overflow:ellipsis}

""";

    public static string BuildHtml(bool visible, int? preferredHeight = null) => $$"""
<section id="ucx-live-timeline" aria-label="Live-QSO-Verlauf" data-presentation-mode="{{(preferredHeight is < 40 ? "miniature" : preferredHeight is < 100 ? "compact" : "full")}}" {{(visible ? "" : "hidden")}} {{(preferredHeight is { } height ? $"style=\"--ucx-timeline-height:{Math.Clamp(height, 20, 320)}px\"" : "")}}>
 <div id="ucx-timeline-resize" role="separator" aria-orientation="horizontal" aria-label="Höhe des Live-QSO-Verlaufs" aria-controls="ucx-timeline-chart" aria-describedby="ucx-timeline-resize-help" tabindex="0" title="Höhe ziehen · Pfeil oben/unten: 10 px · Umschalt: 1 px · Doppelklick: Standardhöhe · F2: Einstellungen"></div>
 <span id="ucx-timeline-resize-help" class="ucx-visually-hidden">Nach oben ziehen vergrößert den Verlauf. Pfeiltasten oben/unten ändern 10 Pixel, mit Umschalt 1 Pixel. Pos1: 20 Pixel, Ende: 320 Pixel. Doppelklick stellt die Standardhöhe wieder her. F2 öffnet die Verlauf-Einstellungen.</span>
 <div id="ucx-timeline-heading" class="ucx-timeline-heading" {{(preferredHeight is < 100 ? "hidden" : "")}}><strong>Live-QSO-Verlauf</strong><span id="ucx-timeline-count"></span><span id="ucx-timeline-activity" aria-live="off"></span></div>
 <svg id="ucx-timeline-chart" role="img" aria-label="QSO-Häufigkeit nach UTC-Zeit"></svg>
 <div id="ucx-timeline-footnote" class="ucx-timeline-footnote" {{(preferredHeight is < 100 ? "hidden" : "")}}><span>Höhe = QSOs · Grün: QSOs · Amber: QSO-Lücken · gestrichelt: laufende Lücke</span><span id="ucx-timeline-note">QSO-Abstände erlauben keinen Schluss auf Sendeaktivität.</span></div>
</section>
""";

    public const string Script = """
let liveActivity=null,timelineSnapshot=null,timelineSamples=[],timelinePrefix=[0],timelineNextRender=0,timelineLastRender=-Infinity,timelineRenderCount=0,timelineReportPerformance=true;
let timelineHost=null,timelineSettingsHost=null,timelineVisibilityCheckbox=null,timelineSettingsCheckbox=null,durationDragging=false;
let timelineHeightDrag=null,timelineEffectiveHeight=null;
function timelineElement(id){return document.getElementById(id)}
function liveActivityState(now=Date.now()){
 const timestamp=Date.parse(liveActivity?.timestampUtc),valid=Number.isFinite(timestamp);
 const seconds=valid?Math.max(0,Math.floor((now-timestamp)/1000)):null;
 return {seconds,future:valid&&timestamp>now,status:seconds===null?'unknown':seconds>=statisticsSettings.liveProlongedGapThresholdMinutes*60?'prolonged':seconds>=statisticsSettings.liveWarningThresholdMinutes*60?'warning':'normal'};
}
function magneticTimelineDuration(raw,enabled,pointerActive){
 raw=Math.max(15,Math.min(240,Math.round(raw)));
 if(enabled&&pointerActive){const point=[60,120,180,240].find(x=>Math.abs(x-raw)<=3);if(point!==undefined)return point}
 return raw;
}
function timelineIndex(time,after=false){let lo=0,hi=timelineSamples.length;while(lo<hi){const mid=(lo+hi)>>>1;if(timelineSamples[mid].time<time||(after&&timelineSamples[mid].time===time))lo=mid+1;else hi=mid}return lo}
function timelineModel(now,durationMinutes,bucketMinutes){
 const start=now-durationMinutes*60000,width=bucketMinutes*60000,buckets=[];
 for(let t=Math.floor(start/width)*width;t<=now;t+=width){const from=Math.max(start,t),to=Math.min(now,t+width),lo=timelineIndex(from),hi=timelineIndex(to,t+width>now);buckets.push({start:t,end:t+width,from,to,count:timelinePrefix[hi]-timelinePrefix[lo]})}
 const lo=timelineIndex(start),hi=timelineIndex(now,true),count=timelinePrefix[hi]-timelinePrefix[lo];
 let next=Math.floor(now/60000)*60000+60000;
 if(hi<timelineSamples.length)next=Math.min(next,timelineSamples[hi].time);
 if(lo<hi)next=Math.min(next,timelineSamples[lo].time+durationMinutes*60000+1);
 const gaps=[];
 for(const bucket of buckets){if(bucket.count!==0||bucket.to<=bucket.from)continue;const previous=gaps.at(-1);if(previous&&previous.to===bucket.from)previous.to=bucket.to;else gaps.push({from:bucket.from,to:bucket.to})}
 const last=Date.parse(liveActivity?.timestampUtc),ongoing=Number.isFinite(last)&&last<=now?{from:Math.max(start,last),to:now}:null;
 const missingPast=!!timelineSnapshot&&start<timelineSnapshot.coverageStartUnixSeconds*1000;
 const missingFuture=!!timelineSnapshot&&timelineSnapshot.futureBeyondCoverageCount>0&&now>timelineSnapshot.coverageEndUnixSeconds*1000;
 return {start,end:now,buckets,gaps,ongoing,count,next,coverageLimited:missingPast||missingFuture};
}
function timelineGapClass(seconds){return seconds>=statisticsSettings.liveProlongedGapThresholdMinutes*60?'prolonged':seconds>=statisticsSettings.liveWarningThresholdMinutes*60?'warning':'short'}
function timelineDefaultHeight(){return map.getContainer().clientWidth<=750?174:148}
function preferredTimelineHeight(){const height=statisticsSettings.liveTimelineHeightPx;return Number.isInteger(height)?Math.max(20,Math.min(320,height)):timelineDefaultHeight()}
function syncTimelineMapSize(){const old=suppressViewMessage;suppressViewMessage=true;try{map.invalidateSize({pan:true,animate:false})}finally{suppressViewMessage=old}repositionMapPanels()}
function timelinePresentationMode(height){return height<40?'miniature':height<100?'compact':'full'}
function applyTimelinePresentation(height){
 const mode=timelinePresentationMode(height),full=mode==='full';timelineHost.dataset.presentationMode=mode;
 timelineElement('ucx-timeline-heading').hidden=!full;timelineElement('ucx-timeline-footnote').hidden=!full;
 if(full&&height<128)timelineHost.classList.add('ucx-timeline-tight');else timelineHost.classList.remove('ucx-timeline-tight');
}
function applyTimelineHeight(){
 if(!isLiveMap||!timelineHost)return;
 const area=map.getContainer().clientHeight+(timelineHost.hidden?0:timelineHost.getBoundingClientRect().height),maximum=Math.max(0,Math.min(320,area-80));
 const effective=Math.min(preferredTimelineHeight(),maximum),changed=effective!==timelineEffectiveHeight;
 timelineEffectiveHeight=effective;timelineHost.style.height=effective+'px';
 applyTimelinePresentation(effective);
 const handle=timelineElement('ucx-timeline-resize');
 handle.setAttribute('aria-valuemin',Math.min(20,maximum));handle.setAttribute('aria-valuemax',maximum);handle.setAttribute('aria-valuenow',effective);
 handle.setAttribute('aria-valuetext',effective+' Pixel'+(effective<preferredTimelineHeight()?' (bevorzugt '+preferredTimelineHeight()+' Pixel)':''));
 if(changed&&!timelineHost.hidden){syncTimelineMapSize();timelineNextRender=0}
}
window.ucxTimelineHeightDragging=()=>timelineHeightDrag!==null;
function initializeTimelineResize(){
 const handle=timelineElement('ucx-timeline-resize');
 function setHeight(height){statisticsSettings.liveTimelineHeightPx=Math.max(20,Math.min(320,Math.round(height)));applyTimelineHeight();timelineNextRender=0;updateLiveActivity()}
 handle.addEventListener('pointerdown',e=>{
  if((e.button!==undefined&&e.button!==0)||e.isPrimary===false||timelineHeightDrag)return;
  e.preventDefault();e.stopPropagation();handle.setPointerCapture(e.pointerId);handle.focus({preventScroll:true});
  timelineHeightDrag={pointerId:e.pointerId,y:e.clientY,height:timelineEffectiveHeight??preferredTimelineHeight(),previous:statisticsSettings.liveTimelineHeightPx,moved:false};
 });
 handle.addEventListener('pointermove',e=>{
  const drag=timelineHeightDrag;if(!drag||e.pointerId!==drag.pointerId)return;e.preventDefault();e.stopPropagation();
  if(e.clientY===drag.y&&!drag.moved)return;drag.moved=true;setHeight(drag.height+drag.y-e.clientY);
 });
 function finish(e,cancel){
  const drag=timelineHeightDrag;if(!drag||e.pointerId!==drag.pointerId)return;e.stopPropagation();timelineHeightDrag=null;
  if(cancel){statisticsSettings.liveTimelineHeightPx=drag.previous;applyTimelineHeight();timelineNextRender=0;updateLiveActivity()}
  else if(drag.moved)saveMapPresentation();
 }
 handle.addEventListener('pointerup',e=>finish(e,false));handle.addEventListener('pointercancel',e=>finish(e,true));handle.addEventListener('lostpointercapture',e=>finish(e,true));
 handle.addEventListener('keydown',e=>{
  if(e.key==='F2'){e.preventDefault();e.stopPropagation();showTimelineSettings();return}
  if(['ArrowLeft','ArrowRight','PageUp','PageDown'].includes(e.key)){e.preventDefault();e.stopPropagation();return}
  if(!['ArrowUp','ArrowDown','Home','End'].includes(e.key)||e.ctrlKey||e.altKey||e.metaKey)return;e.preventDefault();e.stopPropagation();
  const height=preferredTimelineHeight(),step=e.shiftKey?1:10;
  setHeight(e.key==='Home'?20:e.key==='End'?320:height+(e.key==='ArrowUp'?step:-step));saveMapPresentation();
 });
 handle.addEventListener('dblclick',e=>{e.preventDefault();e.stopPropagation();statisticsSettings.liveTimelineHeightPx=null;applyTimelineHeight();timelineNextRender=0;updateLiveActivity();saveMapPresentation()});
 const layout=()=>{applyTimelineHeight();timelineNextRender=0;updateLiveActivity()};
 window.addEventListener('resize',layout);
 if(typeof ResizeObserver!=='undefined'){
  const observer=new ResizeObserver(layout);for(const element of [timelineHost,timelineElement('ucx-timeline-chart'),map.getContainer().parentElement])if(element)observer.observe(element);
 }
}
function renderLiveTimeline(now=Date.now()){
 if(!isLiveMap||!timelineHost||timelineHost.hidden||timelineEffectiveHeight===0)return;
 const chart=timelineElement('ucx-timeline-chart'),width=Math.max(1,chart.clientWidth||timelineHost.clientWidth||1000),height=Math.max(1,chart.clientHeight||88);
 chart.setAttribute('viewBox',`0 0 ${width} ${height}`);
 const mode=timelinePresentationMode(timelineEffectiveHeight??preferredTimelineHeight()),miniature=mode==='miniature',full=mode==='full';
 if(!timelineSnapshot){timelineElement('ucx-timeline-count').textContent='Verlauf wird geladen…';chart.innerHTML=miniature?'<title>UTC-Zeitdaten werden geladen…</title>':`<text x="0" y="${Math.min(45,height-4)}" fill="#536068">UTC-Zeitdaten werden geladen…</text>`;timelineNextRender=Infinity;timelineLastRender=now;return}
 const started=performance.now(),duration=statisticsSettings.liveTimelineMinutes,bucket=statisticsSettings.liveTimelineBucketMinutes,model=timelineModel(now,duration,bucket);
 const left=full?30:0,right=full?width-8:width,top=miniature?0:height<40?2:5,bottom=miniature?height:Math.max(top+1,height-(full&&height>=60?24:16)),span=Math.max(1,model.end-model.start),x=t=>left+(t-model.start)/span*(right-left);
 const peak=Math.max(1,...model.buckets.map(b=>b.count)),axisColor='#4b6266';
 let svg=`<title>Live-QSO-Verlauf: ${model.coverageLimited?'mindestens ':''}${model.count} QSOs in ${duration} Minuten. UTC.${model.coverageLimited?' Zeitdaten außerhalb des übertragenen Bereichs sind unvollständig.':''} QSO-Lücken sind keine gemessenen Betriebspausen.</title>`;
 if(model.coverageLimited)svg+=`<rect x="${left}" y="${top}" width="${right-left}" height="${bottom-top}" fill="#e3e7e9"/>`;
 for(const gap of model.gaps){const level=timelineGapClass((gap.to-gap.from)/1000),opacity={short:.045,warning:.12,prolonged:.2}[level];svg+=`<rect class="ucx-gap-${level}" x="${x(gap.from)}" y="${top}" width="${Math.max(0,x(gap.to)-x(gap.from))}" height="${bottom-top}" fill="#c58a27" fill-opacity="${opacity}"><title>Leere QSO-Zeitintervalle · ${statisticDuration((gap.to-gap.from)/1000)}</title></rect>`}
 if(model.ongoing){const gap=model.ongoing,level=timelineGapClass(liveActivityState(now).seconds),opacity={short:.025,warning:.09,prolonged:.15}[level];svg+=`<rect class="ucx-gap-ongoing" x="${x(gap.from)}" y="${top}" width="${Math.max(0,x(gap.to)-x(gap.from))}" height="${bottom-top}" fill="#c58a27" fill-opacity="${opacity}"/><path d="M ${x(gap.from)} ${top+1} H ${right}" stroke="#9b6b1a" stroke-dasharray="4 4"><title>Laufende QSO-Lücke</title></path>`}
 if(model.coverageLimited){for(const [from,to] of [[model.start,Math.min(model.end,timelineSnapshot.coverageStartUnixSeconds*1000)],[Math.max(model.start,timelineSnapshot.coverageEndUnixSeconds*1000),timelineSnapshot.futureBeyondCoverageCount>0?model.end:model.start]])if(to>from)svg+=`<rect x="${x(from)}" y="${top}" width="${x(to)-x(from)}" height="${bottom-top}" fill="#e3e7e9" stroke="#7c8c92" stroke-dasharray="2 4"><title>Zeitdaten außerhalb des übertragenen Bereichs</title></rect>`}
 for(const b of model.buckets){if(!b.count)continue;const h=b.count/peak*(bottom-top-(miniature?0:Math.min(4,(bottom-top)*.1))),barWidth=Math.max(1,x(b.to)-x(b.from)-2),bx=Math.min(right-barWidth,x(b.from)+1);svg+=`<rect x="${bx}" y="${bottom-h}" width="${barWidth}" height="${h}" fill="#287f78"><title>${esc(new Date(b.start).toISOString())} – ${esc(new Date(b.end).toISOString())} · ${b.count} QSOs${b.from!==b.start||b.to!==b.end?' · teilweise sichtbar':''}</title></rect>`}
 if(full)svg+=`<path d="M ${left} ${top} V ${bottom} H ${right}" fill="none" stroke="${axisColor}"/>${bottom-top>=20?`<text x="${left-5}" y="${top+10}" text-anchor="end" fill="${axisColor}" font-size="11">${peak}</text>`:''}<text x="${left-5}" y="${bottom}" text-anchor="end" fill="${axisColor}" font-size="11">0</text>`;
 if(!miniature){
  const labels=full?(width>700?4:2):1;
  for(let i=0;i<=labels;i++){const t=model.start+span*i/labels,label=i===labels?'Jetzt':new Date(t).toISOString().slice(11,16),anchor=i===0?'start':i===labels?'end':'middle';svg+=`<text x="${x(t)}" y="${Math.max(11,height-6)}" text-anchor="${anchor}" fill="${axisColor}" font-size="11">${label}${i===0?' UTC':''}</text>`}
 }
 chart.setAttribute('aria-label',`${model.coverageLimited?'Mindestens ':''}${model.count} QSOs in ${duration} Minuten, UTC`);chart.innerHTML=svg;
 timelineElement('ucx-timeline-count').textContent=`${model.coverageLimited?'Mindestens ':''}${model.count} QSOs · ${duration} min · ${bucket}-min-Raster`;
 timelineElement('ucx-timeline-note').textContent=model.coverageLimited?'Zeitbereich nach Uhrkorrektur unvollständig; Daten beim nächsten Import erneuern.':'Live-Bereich · alle Bänder · QSO-Abstände zeigen keine Sendeaktivität.';
 timelineNextRender=model.next;timelineLastRender=now;timelineRenderCount++;
 const milliseconds=performance.now()-started;
 if(milliseconds>50||timelineReportPerformance)try{window.chrome.webview.postMessage({type:'timelinePerformance',milliseconds,samples:timelineSamples.length})}catch{}
 timelineReportPerformance=false;
}
function updateLiveActivity(now=Date.now()){
 if(!isLiveMap||!timelineHost)return;
 const state=liveActivityState(now),host=timelineElement('ucx-timeline-activity'),status={normal:'Normaler QSO-Abstand',warning:'Längere QSO-Lücke',prolonged:'Sehr lange QSO-Lücke',unknown:'Noch kein QSO mit gültiger UTC-Zeit'}[state.status];
 const elapsed=state.seconds===null?'':state.seconds<3600?`${Math.floor(state.seconds/60)}:${String(state.seconds%60).padStart(2,'0')}`:statisticDuration(state.seconds);
 host.className=state.status;host.innerHTML=`<b>${esc(status)}</b>${elapsed?` · Letztes QSO vor <b>${elapsed}</b>`:''}${liveActivity?` · ${esc(liveActivity.callSign)} · Band des letzten QSOs: ${esc(bandDisplay(liveActivity.bandCode))}`:''}${state.future?' · QSO-Zeit in der Zukunft; Abstand 0.':''}`;
 host.title=status+(elapsed?' · Letztes QSO vor '+elapsed:'')+(liveActivity?' · '+liveActivity.callSign+' · Band des letzten QSOs: '+bandDisplay(liveActivity.bandCode)+' · '+statisticUtc(liveActivity.timestampUtc):'');
 const gapLevel=state.status;
 if(now>=timelineNextRender||now<timelineLastRender||timelineHost.dataset.gapLevel!==gapLevel){timelineHost.dataset.gapLevel=gapLevel;renderLiveTimeline(now)}
}
window.ucxSetLiveActivity=function(activity){liveActivity=activity;timelineNextRender=0;updateLiveActivity()};
window.ucxSetLiveTimeline=function(snapshot){
 timelineSnapshot=snapshot;timelineSamples=[];timelineReportPerformance=true;
 for(let i=0;i<(snapshot?.samples?.length??0);i+=2)timelineSamples.push({time:(snapshot.coverageStartUnixSeconds+snapshot.samples[i])*1000,count:snapshot.samples[i+1]});
 timelineSamples.sort((a,b)=>a.time-b.time);timelinePrefix=[0];for(const sample of timelineSamples)timelinePrefix.push(timelinePrefix.at(-1)+sample.count);liveActivity=snapshot?.latestQso??null;timelineNextRender=0;updateLiveActivity();
};
window.ucxDurationDragging=()=>durationDragging;
let liveThresholdDrag=null;
function liveThresholdUiPair(){
 const l=liveThresholdUiLimits,s=statisticsSettings;
 let warning=Math.max(l.warningMinimum,Math.min(l.warningMaximum,s.liveWarningThresholdMinutes)),prolonged=Math.max(l.prolongedMinimum,Math.min(l.prolongedMaximum,s.liveProlongedGapThresholdMinutes));
 if(warning>=prolonged)prolonged=warning+1;
 return {warning,prolonged};
}
function commitLiveThresholds(){
 const s=statisticsSettings,l=liveThresholdUiLimits,w=s.liveWarningThresholdMinutes,p=s.liveProlongedGapThresholdMinutes;
 if(!Number.isInteger(w)||!Number.isInteger(p)||w<l.warningMinimum||w>l.warningMaximum||p<l.prolongedMinimum||p>l.prolongedMaximum||w>=p)return false;
 try{window.chrome.webview.postMessage({type:'liveThresholdPreferences',warningMinutes:w,prolongedMinutes:p})}catch{}
 return true;
}
function applyLiveThreshold(which,raw,persist=true){
 if(!['warning','prolonged'].includes(which)||(typeof raw!=='number'&&(typeof raw!=='string'||!raw.match(/^-?[0-9]+$/))))return false;
 const value=Number(raw);if(!Number.isFinite(value)||!Number.isInteger(value))return false;
 const l=liveThresholdUiLimits,pair=liveThresholdUiPair();let {warning,prolonged}=pair;
 if(which==='warning'){warning=Math.max(l.warningMinimum,Math.min(l.warningMaximum,value));prolonged=Math.max(prolonged,warning+1)}
 else{prolonged=Math.max(l.prolongedMinimum,Math.min(l.prolongedMaximum,value));warning=Math.min(warning,prolonged-1)}
 const changed=statisticsSettings.liveWarningThresholdMinutes!==warning||statisticsSettings.liveProlongedGapThresholdMinutes!==prolonged;
 statisticsSettings.liveWarningThresholdMinutes=warning;statisticsSettings.liveProlongedGapThresholdMinutes=prolonged;
 applyTimelineControlValues();timelineNextRender=0;updateLiveActivity();
 if(changed&&persist)commitLiveThresholds();return true;
}
window.ucxRetainLiveThresholdPreview=function(incoming){
 if(!liveThresholdDrag)return;
 liveThresholdDrag.previous={warning:incoming.liveWarningThresholdMinutes,prolonged:incoming.liveProlongedGapThresholdMinutes};
 incoming.liveWarningThresholdMinutes=statisticsSettings.liveWarningThresholdMinutes;incoming.liveProlongedGapThresholdMinutes=statisticsSettings.liveProlongedGapThresholdMinutes;
};
function initializeLiveThresholdSliders(){
 function finish(event,cancel){
  const drag=liveThresholdDrag;if(!drag||(event?.pointerId!==undefined&&event.pointerId!==drag.pointerId))return;
  event?.stopPropagation?.();liveThresholdDrag=null;
  try{if(drag.slider.hasPointerCapture(drag.pointerId))drag.slider.releasePointerCapture(drag.pointerId)}catch{}
  if(cancel){statisticsSettings.liveWarningThresholdMinutes=drag.previous.warning;statisticsSettings.liveProlongedGapThresholdMinutes=drag.previous.prolonged;applyTimelineControlValues();timelineNextRender=0;updateLiveActivity()}
  else if(statisticsSettings.liveWarningThresholdMinutes!==drag.previous.warning||statisticsSettings.liveProlongedGapThresholdMinutes!==drag.previous.prolonged)commitLiveThresholds();
 }
 for(const which of ['warning','prolonged']){
  const slider=timelineSettingsHost.querySelector('#ucx-live-'+which);
  slider.style.touchAction='none';
  function preview(event){
   const drag=liveThresholdDrag;if(!drag||drag.slider!==slider||event.pointerId!==drag.pointerId)return;
   if(event.buttons===0){finish(event,false);return}
   event.preventDefault();event.stopPropagation();if(!Number.isFinite(event.clientX))return;
   const x=Math.max(drag.left,Math.min(drag.left+drag.width,event.clientX));
   const value=Math.round(drag.minimum+(x-drag.left)/drag.width*(drag.maximum-drag.minimum));
   applyLiveThreshold(which,value,false);
  }
  slider.addEventListener('pointerdown',event=>{
   if((event.button!==undefined&&event.button!==0)||event.isPrimary===false||liveThresholdDrag||!Number.isFinite(event.clientX))return;
   const rect=slider.getBoundingClientRect();if(!Number.isFinite(rect.left)||!Number.isFinite(rect.width)||rect.width<=0)return;
   event.preventDefault();event.stopPropagation();slider.focus({preventScroll:true});
   try{slider.setPointerCapture(event.pointerId)}catch{return}
   const l=liveThresholdUiLimits;
   liveThresholdDrag={slider,pointerId:event.pointerId,which,left:rect.left,width:rect.width,
    minimum:which==='warning'?l.warningMinimum:l.prolongedMinimum,maximum:which==='warning'?l.warningMaximum:l.prolongedMaximum,
    previous:{warning:statisticsSettings.liveWarningThresholdMinutes,prolonged:statisticsSettings.liveProlongedGapThresholdMinutes}};
   preview(event);
  });
  slider.addEventListener('pointermove',preview);slider.addEventListener('pointerup',event=>finish(event,false));
  slider.addEventListener('pointercancel',event=>finish(event,true));slider.addEventListener('lostpointercapture',event=>finish(event,true));
  slider.addEventListener('keydown',event=>{if(event.key==='Escape'&&liveThresholdDrag){event.preventDefault();finish(event,true)}});
  slider.addEventListener('input',event=>{
   if(liveThresholdDrag){applyTimelineControlValues();return}
   if(!applyLiveThreshold(which,event.target.value))applyTimelineControlValues();
  });
 }
 window.addEventListener('pointerup',event=>finish(event,false));window.addEventListener('pointercancel',event=>finish(event,true));
 window.addEventListener('blur',()=>finish(null,true));window.addEventListener('pagehide',()=>finish(null,true));
 document.addEventListener('visibilitychange',()=>{if(document.hidden)finish(null,true)});
}
function applyTimelineControlValues(){
 if(!timelineSettingsHost)return;
 const el=id=>timelineSettingsHost.querySelector('#'+id),s=statisticsSettings;
 if(!durationDragging)el('ucx-timeline-duration').value=s.liveTimelineMinutes;
 el('ucx-timeline-duration-value').textContent=s.liveTimelineMinutes+' min';el('ucx-timeline-snap').checked=s.liveTimelineSnap;el('ucx-timeline-bucket').value=s.liveTimelineBucketMinutes;
 const l=liveThresholdUiLimits,pair=liveThresholdUiPair();
 for(const [id,value,minimum,maximum] of [['ucx-live-warning',pair.warning,l.warningMinimum,l.warningMaximum],['ucx-live-prolonged',pair.prolonged,l.prolongedMinimum,l.prolongedMaximum]]){el(id).min=minimum;el(id).max=maximum;el(id).value=value;el(id+'-value').textContent=value+' min'}
 const outside=pair.warning!==s.liveWarningThresholdMinutes||pair.prolonged!==s.liveProlongedGapThresholdMinutes;
 el('ucx-live-threshold-note').textContent=outside?`Konfiguration: Warnung ${s.liveWarningThresholdMinutes} min, sehr lange Lücke ${s.liveProlongedGapThresholdMinutes} min. Regler zeigen begrenzte Werte; erst eine Änderung übernimmt 1–179 / 2–180 min.`:'Regler: Warnung 1–179 min; sehr lange Lücke 2–180 min. Bei Überschneidung wird der andere Wert um den nötigen Abstand angepasst.';
}
window.ucxApplyTimelineSettings=function(){
 if(!isLiveMap||!timelineHost)return;
 const visibilityChanged=timelineHost.hidden===statisticsSettings.liveTimelineVisible;
 timelineHost.hidden=!statisticsSettings.liveTimelineVisible;
 timelineSettingsHost.style.display=statisticsSettings.liveTimelineSettingsVisible?'block':'none';
 const panel=mapPanels.get('timeline');if(panel)placeMapPanel(panel);
 if(timelineVisibilityCheckbox)timelineVisibilityCheckbox.checked=statisticsSettings.liveTimelineVisible;
 if(timelineSettingsCheckbox)timelineSettingsCheckbox.checked=statisticsSettings.liveTimelineSettingsVisible;
 applyTimelineControlValues();applyTimelineHeight();timelineNextRender=0;updateLiveActivity();
 if(visibilityChanged)syncTimelineMapSize();
};
function setTimelinePresentation(key,value){statisticsSettings[key]=value;window.ucxApplyTimelineSettings();saveMapPresentation()}
function showTimelineSettings(){
 if(!isLiveMap||!timelineSettingsHost)return;
 statisticsSettings.liveTimelineSettingsVisible=true;statisticsSettings.liveTimelineSettingsCollapsed=false;
 window.ucxApplyPanelSettings();window.ucxApplyTimelineSettings();saveMapPresentation();
 timelineSettingsHost.querySelector('#ucx-timeline-duration').focus({preventScroll:true});
}
function initializeLiveTimeline(){
 if(!isLiveMap)return;
 timelineHost=timelineElement('ucx-live-timeline');initializeTimelineResize();L.DomEvent.disableClickPropagation(timelineHost);L.DomEvent.disableScrollPropagation(timelineHost);
 const TimelineSettingsControl=L.Control.extend({onAdd:function(){
  const div=L.DomUtil.create('div','ucx-timeline-settings');L.DomEvent.disableClickPropagation(div);L.DomEvent.disableScrollPropagation(div);
  div.innerHTML=mapPanelMarkup('Live-Verlauf – Einstellungen',`<label class="ucx-range">Zeitraum <input id="ucx-timeline-duration" type="range" min="15" max="240" step="1" aria-label="Zeitraum in Minuten"><output id="ucx-timeline-duration-value"></output></label>
  <label><input id="ucx-timeline-snap" type="checkbox"> Stundenrastung (±3 min beim Ziehen)</label><p class="ucx-note">Pfeiltasten ändern minutengenau. Beim Weiterziehen über die Rastzone löst sich die Rastung.</p>
  <label>UTC-Raster <select id="ucx-timeline-bucket" aria-label="Histogramm-Raster"><option value="1">1 Minute</option><option value="2">2 Minuten</option><option value="5">5 Minuten</option><option value="10">10 Minuten</option></select></label>
  <label class="ucx-range">Warnung <input id="ucx-live-warning" type="range" min="${liveThresholdUiLimits.warningMinimum}" max="${liveThresholdUiLimits.warningMaximum}" step="1" aria-label="QSO-Lücke Warnung in Minuten" aria-describedby="ucx-live-threshold-note"><output id="ucx-live-warning-value"></output></label>
  <label class="ucx-range">Sehr lange Lücke <input id="ucx-live-prolonged" type="range" min="${liveThresholdUiLimits.prolongedMinimum}" max="${liveThresholdUiLimits.prolongedMaximum}" step="1" aria-label="Sehr lange QSO-Lücke in Minuten" aria-describedby="ucx-live-threshold-note"><output id="ucx-live-prolonged-value"></output></label>
  <p id="ucx-live-threshold-note" class="ucx-note"></p>
  <button type="button" id="ucx-timeline-defaults">Verlauf zurücksetzen</button><p class="ucx-note">Rollendes Fenster; Rastergrenzen sind fest in UTC. QSO-Lücken sind keine bestätigten Betriebspausen.</p>`);
  timelineSettingsHost=div;return div;
 }});
 const control=new TimelineSettingsControl({position:'bottomright'});map.addControl(control);registerMapPanel('timeline',control.getContainer());
 const el=id=>timelineSettingsHost.querySelector('#'+id),duration=el('ucx-timeline-duration');
 duration.addEventListener('pointerdown',()=>{durationDragging=true});
 function updateDuration(){statisticsSettings.liveTimelineMinutes=magneticTimelineDuration(Number(duration.value),statisticsSettings.liveTimelineSnap,durationDragging);el('ucx-timeline-duration-value').textContent=statisticsSettings.liveTimelineMinutes+' min';timelineNextRender=0;updateLiveActivity()}
 duration.addEventListener('input',updateDuration);
 function finishDuration(){updateDuration();durationDragging=false;duration.value=statisticsSettings.liveTimelineMinutes;saveMapPresentation()}
 duration.addEventListener('pointerup',finishDuration);duration.addEventListener('pointercancel',finishDuration);
 duration.addEventListener('change',()=>{if(!durationDragging){updateDuration();saveMapPresentation()}});
 duration.addEventListener('keydown',()=>{durationDragging=false});
 el('ucx-timeline-snap').addEventListener('change',e=>setTimelinePresentation('liveTimelineSnap',e.target.checked));
 el('ucx-timeline-bucket').addEventListener('change',e=>setTimelinePresentation('liveTimelineBucketMinutes',Number(e.target.value)));
 initializeLiveThresholdSliders();
 el('ucx-timeline-defaults').addEventListener('click',()=>{Object.assign(statisticsSettings,{liveTimelineMinutes:60,liveTimelineSnap:true,liveTimelineBucketMinutes:5,liveTimelineHeightPx:null});applyLiveThreshold('warning',10);applyLiveThreshold('prolonged',15);window.ucxApplyTimelineSettings();saveMapPresentation()});
 setInterval(()=>updateLiveActivity(),1000);
}
""";
}
