namespace UcxStats.Map;

internal static class MapStatisticsPanel
{
    public const string Styles = """
.ucx-statistics{background:rgba(255,255,255,.96);padding:8px 10px;border-radius:4px;box-shadow:0 1px 5px #777;font-size:13px;line-height:1.4;max-width:min(440px,70vw);max-height:65vh;overflow:auto}
.ucx-statistics summary{cursor:pointer;font-weight:600}.ucx-statistics details{margin-top:6px}.ucx-statistics table{border-collapse:collapse;width:100%;font-variant-numeric:tabular-nums}.ucx-statistics td,.ucx-statistics th{padding:2px 4px;text-align:right;border-bottom:1px solid #eee}.ucx-statistics td:first-child,.ucx-statistics th:first-child{text-align:left}.ucx-statistics p{margin:5px 0}.ucx-statistics .ucx-note{color:#555;font-size:12px;max-width:390px}.ucx-statistics label{display:block;margin:4px 0}.ucx-statistics input[type=number]{width:65px}
""";

    public const string Script = """
let statisticsSnapshot=null,statisticsSettings={...initialStatisticsSettings};
let statisticsHost=null,statisticsCheckbox=null;
const StatisticsControl=L.Control.extend({onAdd:function(){
 const div=L.DomUtil.create('div','ucx-statistics');L.DomEvent.disableClickPropagation(div);L.DomEvent.disableScrollPropagation(div);
 div.innerHTML=mapPanelMarkup('QSO-Statistik',`<div id="ucx-stat-summary">Statistik wird geladen…</div>
 <details><summary>Bänder · ausgewählte Bänder</summary><div class="ucx-note">Q = QSOs · M = kartierbar · D = DXCC · F = Großfelder · G = 4er-Grids</div><div id="ucx-stat-bands"></div></details>
 <label><input id="ucx-contest-visible" type="checkbox"> Contestzeit anzeigen</label>
 <details id="ucx-contest-section" open><summary>Contestzeit · gesamter QSO-Bereich</summary><div id="ucx-stat-contest"></div><p class="ucx-note">QSO-basierte Schätzung. CQ-Rufen ohne QSO ist nicht erkennbar. Keine gemessene Sendezeit und keine Prüfung von Contestregeln. Bandwahl ändert diese Zeiten nicht.</p></details>
 <details><summary>Statistikoptionen</summary>
 <label class="ucx-range">Pausenschwelle <input id="ucx-pause-threshold" aria-label="Contest-Pausenschwelle in Minuten" type="range" min="5" max="180" step="1"><output id="ucx-pause-value"></output></label>
 <label>Zeitbudget (Minuten, optional) <input id="ucx-allowance" type="number" min="1" max="525600" step="1"></label>
 <div id="ucx-stat-validation" class="ucx-note"></div></details>`);
 statisticsHost=div;
 div.querySelector('#ucx-pause-threshold').addEventListener('input',()=>{
  statisticsSettings.contestPauseThresholdMinutes=Number(statElement('ucx-pause-threshold').value);
  statElement('ucx-pause-value').textContent=statisticsSettings.contestPauseThresholdMinutes+' min';saveStatisticsPreferences();
 });
 for(const selector of ['#ucx-contest-visible','#ucx-allowance'])
  div.querySelector(selector).addEventListener('change',saveStatisticsPreferences);
 return div;
}});
const statisticsControl=new StatisticsControl({position:'bottomleft'});map.addControl(statisticsControl);registerMapPanel('statistics',statisticsControl.getContainer());
function statElement(id){return statisticsHost.querySelector('#'+id)}
function statisticDuration(seconds){seconds=Math.max(0,Math.floor(seconds));const h=Math.floor(seconds/3600),m=Math.floor(seconds%3600/60),s=seconds%60;return `${h}:${String(m).padStart(2,'0')}:${String(s).padStart(2,'0')}`}
function statisticUtc(value){return value?esc(value.replace('T',' ').replace(/(?:Z|\+00:00)$/,' UTC')):'nicht verfügbar'}
function statisticOdx(odx,details=true){return odx?`${odx.distanceKm.toLocaleString('de-DE',{maximumFractionDigits:1})} km${details?` · ${esc(odx.callSign)} · ${esc(odx.locator)} · ${esc(bandDisplay(odx.bandCode))}<br><small>${statisticUtc(odx.timestampUtc)}</small>`:''}`:'nicht verfügbar'}
// Select preaggregated identity memberships: each distinct identity contributes once
// if any of its bands is selected. No QSO rows are processed in the browser.
function selectedStatistics(){
 const selected=selectedBandCodes(),snapshot=statisticsSnapshot;
 const rows=(snapshot?.bands??[]).filter(x=>selected.has(x.bandCode));
 const count=groups=>(groups??[]).reduce((n,x)=>n+(x.bandCodes.some(b=>selected.has(b))?x.count:0),0);
 const odx=rows.map(x=>x.summary.odx).filter(Boolean).sort((a,b)=>b.distanceKm-a.distanceKm||a.qsoId-b.qsoId)[0]??null;
 return {totalQsos:rows.reduce((n,x)=>n+x.summary.totalQsos,0),mappableQsos:rows.reduce((n,x)=>n+x.summary.mappableQsos,0),
  dxccs:count(snapshot?.dxccMemberships),majorFields:count(snapshot?.fieldMemberships),grids:count(snapshot?.gridMemberships),odx,rows,noBands:selected.size===0};
}
function renderStatistics(){
 if(!statisticsHost)return;
 if(!statisticsSnapshot){statElement('ucx-stat-summary').textContent='Statistik wird geladen…';statElement('ucx-stat-bands').innerHTML='';statElement('ucx-stat-contest').innerHTML='';return}
 const s=selectedStatistics();
 statElement('ucx-stat-summary').innerHTML=`<p><b>${s.noBands?'Keine Bänder ausgewählt':'Ausgewählte Bänder · zusammen'}</b></p><table><tr><td>QSOs gesamt</td><td>${s.totalQsos}</td></tr><tr><td>Kartierbare QSOs</td><td>${s.mappableQsos}</td></tr><tr><td>Nicht kartierbar</td><td>${s.totalQsos-s.mappableQsos}</td></tr><tr><td>DXCCs</td><td>${s.dxccs}</td></tr><tr><td>Großfelder (2 Zeichen)</td><td>${s.majorFields}</td></tr><tr><td>Grids (4 Zeichen)</td><td>${s.grids}</td></tr></table><p><b>ODX:</b> ${statisticOdx(s.odx)}</p>`;
 statElement('ucx-stat-bands').innerHTML=s.rows.length?`<table><thead><tr><th>Band</th><th>Q</th><th>M</th><th>D</th><th>F</th><th>G</th><th>ODX km</th></tr></thead><tbody>${s.rows.map(b=>{const x=b.summary;return `<tr><td>${esc(b.band)}</td><td>${x.totalQsos}</td><td>${x.mappableQsos}</td><td>${x.dxccs}</td><td>${x.majorFields}</td><td>${x.grids}</td><td>${x.odx?x.odx.distanceKm.toFixed(1):'–'}</td></tr>`}).join('')}</tbody></table><details><summary>ODX je Band</summary>${s.rows.map(b=>`<p><b>${esc(b.band)}:</b> ${statisticOdx(b.summary.odx)}</p>`).join('')}</details>`:'Keine QSOs auf ausgewählten Bändern.';
 renderContestTime();
}
function renderContestTime(){
 const t=statisticsSnapshot?.contest;if(!t)return;
 const allowance=t.allowanceSeconds==null?'':`<p>Zeitbudget: ${statisticDuration(t.allowanceSeconds)} · ${t.remainingSeconds>=0?'Verbleibend':'Überschritten'}: ${statisticDuration(Math.abs(t.remainingSeconds))}</p>`;
 statElement('ucx-stat-contest').innerHTML=`<p>Erstes QSO: ${statisticUtc(t.firstUtc)}<br>Letztes QSO: ${statisticUtc(t.lastUtc)}</p><p>Brutto: ${statisticDuration(t.grossSeconds)}<br>Geschätzte gezählte Zeit: <b>${statisticDuration(t.operatingSeconds)}</b><br>Geschätzte Pausenzeit: ${statisticDuration(t.pauseSeconds)} · ${t.pauseCount} Pausen</p><p class="ucx-note">Eine Lücke zählt vollständig als Pause, wenn sie größer als ${statisticsSettings.contestPauseThresholdMinutes} Minuten ist.</p>${allowance}${t.invalidTimestampCount?`<p>Ohne gültige UTC-Zeit: ${t.invalidTimestampCount} QSOs</p>`:''}<details><summary>Pausenintervalle (UTC${t.pauseCount>t.pauses.length?`, erste ${t.pauses.length} von ${t.pauseCount}`:''})</summary>${t.pauses.map(p=>`<p>${statisticUtc(p.fromUtc)} – ${statisticUtc(p.toUtc)} · ${statisticDuration(p.seconds)}</p>`).join('')||'Keine erkannten Pausen.'}</details>`;
}
window.ucxSetMapStatistics=function(snapshot){
 statisticsSnapshot=snapshot;
 for(const b of snapshot?.bands??[])getBandLayer(b.bandCode,b.band);
 renderBandControls();renderStatistics();
};
window.ucxSetStatisticsVisible=function(visible){statisticsSettings.statisticsVisible=!!visible;statisticsHost.style.display=visible?'block':'none';if(statisticsCheckbox)statisticsCheckbox.checked=!!visible;const panel=mapPanels.get('statistics');if(panel)placeMapPanel(panel)};
window.ucxSetStatisticsSettings=function(settings){
 const preview=statisticsSettings.liveTimelineMinutes,heightPreview=statisticsSettings.liveTimelineHeightPx;
 const incoming={...settings};window.ucxRetainLiveThresholdPreview?.(incoming);
 statisticsSettings=incoming;if(window.ucxDurationDragging?.())statisticsSettings.liveTimelineMinutes=preview;
 if(window.ucxTimelineHeightDragging?.())statisticsSettings.liveTimelineHeightPx=heightPreview;
 window.ucxSetStatisticsVisible(settings.statisticsVisible);
 statElement('ucx-contest-visible').checked=settings.contestTimeVisible;
 statElement('ucx-contest-section').style.display=settings.contestTimeVisible?'block':'none';
 statElement('ucx-pause-threshold').value=settings.contestPauseThresholdMinutes;
 statElement('ucx-allowance').value=settings.operatingTimeAllowanceMinutes??'';
 statElement('ucx-pause-value').textContent=settings.contestPauseThresholdMinutes+' min';
 renderContestTime();window.ucxApplyPanelSettings?.();window.ucxApplyTimelineSettings?.(settings);
};
function saveStatisticsPreferences(){
 const pause=Number(statElement('ucx-pause-threshold').value),allowanceText=statElement('ucx-allowance').value.trim(),allowance=allowanceText===''?null:Number(allowanceText);
 const valid=Number.isInteger(pause)&&pause>=5&&pause<=180&&(allowance===null||(Number.isInteger(allowance)&&allowance>0&&allowance<=525600));
 statElement('ucx-stat-validation').textContent=valid?'':'Bitte gültige Minutenwerte eingeben.';
 if(!valid)return;
 statisticsSettings={...statisticsSettings,contestTimeVisible:statElement('ucx-contest-visible').checked,contestPauseThresholdMinutes:pause,operatingTimeAllowanceMinutes:allowance};
 statElement('ucx-contest-section').style.display=statisticsSettings.contestTimeVisible?'block':'none';
 try{window.chrome.webview.postMessage({type:'statisticsPreferences',settings:statisticsSettings})}catch{}

}
""";
}
