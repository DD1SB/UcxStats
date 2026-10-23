using System.Text.Encodings.Web;
using System.Text.Json;
using UcxStats.Core.Ucx;
using UcxStats.Data.Database;

namespace UcxStats.Map;

public static class MapPageBuilder
{
    private static readonly JsonSerializerOptions PathWidthJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string BuildPathWidthsScript(MapPathWidths widths) =>
        $"window.ucxSetPathWidths && window.ucxSetPathWidths({SerializePathWidths(widths)});";

    private static string SerializePathWidths(MapPathWidths widths) =>
        JsonSerializer.Serialize(widths.Normalize(), PathWidthJsonOptions);

    public static string Build(
        IReadOnlyList<MapQsoPoint> points,
        MapTileDefinition tileSettings,
        MapDxccPageOptions dxcc,
        MapPathWidths? pathWidths = null)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(tileSettings);
        ArgumentNullException.ThrowIfNull(dxcc);

        var qsoJson = BuildQsoPayloadJson(points);
        var dxccStatusJson = DxccMapPayloadFactory.Serialize(dxcc.Status);
        var tileUrlJson = JsonSerializer.Serialize(tileSettings.UrlTemplate);
        var tileAttributionJson = JsonSerializer.Serialize(tileSettings.Attribution);
        var tileProviderJson = JsonSerializer.Serialize(tileSettings.ProviderName);
        var dxccUrlJson = JsonSerializer.Serialize(dxcc.GeometryUrl);
        var dxccDiagnosticJson = JsonSerializer.Serialize(dxcc.Diagnostic);
        var geometryAvailableJson = dxcc.GeometryAvailable ? "true" : "false";
        var enabledByDefaultJson = dxcc.EnabledByDefault ? "true" : "false";
        var pathWidthsJson = SerializePathWidths(pathWidths ?? MapPathWidths.Default);
        var minimumWidthJson = JsonSerializer.Serialize(MapPathWidths.Minimum);
        var maximumWidthJson = JsonSerializer.Serialize(MapPathWidths.Maximum);

        return $$$"""
<!doctype html><html><head><meta charset="utf-8">
<link rel="stylesheet" href="leaflet/leaflet.css">
<style>
html,body,#map{height:100%;margin:0}body{font-family:Segoe UI,Arial,sans-serif}.leaflet-popup-content{line-height:1.35}
.ucx-control{background:#fff;padding:8px 10px;border-radius:4px;box-shadow:0 1px 5px #777;max-height:70vh;overflow:auto;min-width:165px}
.ucx-control label{display:block;white-space:nowrap;margin:2px 0;cursor:pointer}.ucx-control label.ucx-disabled{color:#777;cursor:not-allowed}.ucx-control hr{border:0;border-top:1px solid #ddd;margin:6px 0}
.ucx-swatch{display:inline-block;width:12px;height:4px;margin:0 5px 2px 2px;vertical-align:middle;border-radius:2px}
.ucx-title{font-weight:600;margin-bottom:4px}.ucx-dxcc-note{max-width:210px;color:#555;font-size:11px;white-space:normal;margin-top:4px}
.ucx-dxcc-legend{background:rgba(255,255,255,.94);padding:8px 10px;border-radius:4px;box-shadow:0 1px 5px #777;line-height:1.25}
.ucx-dxcc-legend-row{white-space:nowrap;margin:3px 0}.ucx-dxcc-legend-sample{display:inline-block;width:25px;height:10px;margin-right:6px;vertical-align:middle;border-width:2px;background-clip:padding-box}
.dxcc-unknown{background:#8b8f94;border-color:#666;border-style:dashed;opacity:.6}.dxcc-notworked{background:#d73027;border-color:#9f1e17;border-style:dotted}.dxcc-worked{background:#f4c542;border-color:#8a6d00;border-style:dashed}.dxcc-other{background:#70c5e8;border-color:#247da0;border-style:double}.dxcc-credited{background:#3aa657;border-color:#166c2f;border-style:solid}
.ucx-dxcc-popup-title{font-weight:700;font-size:14px}.ucx-dxcc-popup-meta{color:#555;margin-bottom:7px}.ucx-dxcc-popup-section{margin-top:5px}
</style>
</head><body><div id="map"></div>
<script src="leaflet/leaflet.js"></script>
<script>
const qsos={{{qsoJson}}};
const initialDxccStatus={{{dxccStatusJson}}};
const dxccConfig={geometryAvailable:{{{geometryAvailableJson}}},enabledByDefault:{{{enabledByDefaultJson}}},url:{{{dxccUrlJson}}},diagnostic:{{{dxccDiagnosticJson}}}};
const map=L.map('map',{preferCanvas:true,worldCopyJump:true}).setView([30,10],2);
map.createPane('ucxDxccPane');map.getPane('ucxDxccPane').style.zIndex='320';
map.getPane('ucxDxccPane').style.pointerEvents='auto';
map.createPane('ucxNightPane');map.getPane('ucxNightPane').style.zIndex='340';map.getPane('ucxNightPane').style.pointerEvents='none';
map.createPane('ucxPathPane');map.getPane('ucxPathPane').style.zIndex='410';map.getPane('ucxPathPane').style.pointerEvents='none';
map.createPane('ucxMarkerPane');map.getPane('ucxMarkerPane').style.zIndex='430';
const tileProvider={{{tileProviderJson}}};
const baseTiles=L.tileLayer({{{tileUrlJson}}},{maxZoom:{{{tileSettings.MaxZoom}}},attribution:{{{tileAttributionJson}}}});
let tileErrorReported=false;
baseTiles.on('tileerror',e=>{
 if(tileErrorReported)return;
 tileErrorReported=true;
 try{window.chrome.webview.postMessage({type:'tileError',provider:tileProvider,url:e?.tile?.src??''});}catch{}
});
baseTiles.addTo(map);
const markerRenderer=L.canvas({padding:.5,pane:'ucxMarkerPane'}),pathRenderer=L.canvas({padding:.5,pane:'ucxPathPane'}),stationLayer=L.layerGroup().addTo(map),nightLayer=L.layerGroup(),bounds=[];
const bandLayers=new Map();
let pathWidths={{{pathWidthsJson}}};
function validPathWidth(value,fallback){return typeof value==='number'&&Number.isFinite(value)&&value>={{{minimumWidthJson}}}&&value<={{{maximumWidthJson}}}?value:fallback;}
window.ucxSetPathWidths=function(widths){
 const shortpathWidth=validPathWidth(widths?.shortpathWidth,pathWidths.shortpathWidth),longpathWidth=validPathWidth(widths?.longpathWidth,pathWidths.longpathWidth);
 for(const b of bandLayers.values()){
  if(shortpathWidth!==pathWidths.shortpathWidth)b.paths.eachLayer(layer=>layer.setStyle({weight:shortpathWidth}));
  if(longpathWidth!==pathWidths.longpathWidth)b.longPaths.eachLayer(layer=>layer.setStyle({weight:longpathWidth}));
 }
 pathWidths={shortpathWidth,longpathWidth};
};
let showQsos=true,showPaths=true,showLong=false,showStations=true,showNight=false,suppressViewMessage=false;
let showDxcc=dxccConfig.geometryAvailable&&dxccConfig.enabledByDefault,dxccLayer=null,dxccGeometryReady=false,dxccStatus=initialDxccStatus,bandControlHost=null,dxccControlCheckbox=null;
const dxccFeatureLayers=new Map();
const bandColors={
 '160 m':'#6a3d9a','80 m':'#1f78b4','40 m':'#33a02c','30 m':'#b15928','20 m':'#e31a1c','17 m':'#ff7f00',
 '15 m':'#f1c40f','12 m':'#8e44ad','10 m':'#00a6a6','6 m':'#e84393','2 m':'#2d3436','70 cm':'#636e72',
 '23 cm':'#0984e3','13 cm':'#00b894','3,4 GHz':'#d63031','5,6 GHz':'#e17055','10 GHz':'#6c5ce7'
};
const fallbackColors=['#1f78b4','#33a02c','#e31a1c','#ff7f00','#6a3d9a','#00a6a6','#b15928','#e84393'];
const dxccStyles={
 Unknown:{color:'#666',weight:1,opacity:.55,dashArray:'10 6',fillColor:'#8b8f94',fillOpacity:.10},
 NotWorked:{color:'#9f1e17',weight:1,opacity:.8,dashArray:'2 5',fillColor:'#d73027',fillOpacity:.16},
 Worked:{color:'#8a6d00',weight:1.2,opacity:.9,dashArray:'7 5',fillColor:'#f4c542',fillOpacity:.18},
 OtherConfirmed:{color:'#247da0',weight:1.3,opacity:.95,dashArray:'8 3 2 3',fillColor:'#70c5e8',fillOpacity:.19},
 DxccCredited:{color:'#166c2f',weight:1.7,opacity:1,dashArray:null,fillColor:'#3aa657',fillOpacity:.20}
};
const dxccStatusLabels={Unknown:'Unknown / deleted',NotWorked:'Not worked',Worked:'Worked',OtherConfirmed:'eQSL / ClubLog confirmed',DxccCredited:'LoTW / Paper confirmed'};
const dxccStatusPriority={Unknown:0,NotWorked:1,Worked:2,OtherConfirmed:3,DxccCredited:4};
function esc(v){return String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));}
function rad(x){return x*Math.PI/180} function deg(x){return x*180/Math.PI}
function colorForBand(band){if(bandColors[band])return bandColors[band];let h=0;for(const c of band)h=(h*31+c.charCodeAt(0))>>>0;return fallbackColors[h%fallbackColors.length];}
function gc(a,b,c,d,n=36){
 const p=[rad(a),rad(b)],q=[rad(c),rad(d)],D=2*Math.asin(Math.sqrt(Math.sin((q[0]-p[0])/2)**2+Math.cos(p[0])*Math.cos(q[0])*Math.sin((q[1]-p[1])/2)**2));
 if(!isFinite(D)||D<1e-12)return [[[a,b],[c,d]]];
 const seg=[[]];let last=null;
 for(let i=0;i<=n;i++){const f=i/n,A=Math.sin((1-f)*D)/Math.sin(D),B=Math.sin(f*D)/Math.sin(D),
 x=A*Math.cos(p[0])*Math.cos(p[1])+B*Math.cos(q[0])*Math.cos(q[1]),y=A*Math.cos(p[0])*Math.sin(p[1])+B*Math.cos(q[0])*Math.sin(q[1]),z=A*Math.sin(p[0])+B*Math.sin(q[0]),
 lat=deg(Math.atan2(z,Math.sqrt(x*x+y*y))),lon=deg(Math.atan2(y,x));
 if(last!==null&&Math.abs(lon-last)>180)seg.push([]);seg[seg.length-1].push([lat,lon]);last=lon}
 return seg.filter(x=>x.length>1);
}
function v3(lat,lon){lat=rad(lat);lon=rad(lon);return [Math.cos(lat)*Math.cos(lon),Math.cos(lat)*Math.sin(lon),Math.sin(lat)];}
function cross3(a,b){return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];}
function dot3(a,b){return a[0]*b[0]+a[1]*b[1]+a[2]*b[2];}
function norm3(a){const n=Math.hypot(a[0],a[1],a[2]);return [a[0]/n,a[1]/n,a[2]/n];}
function rot3(v,a,t){const c=Math.cos(t),ss=Math.sin(t),d=dot3(a,v),x=cross3(a,v);return [v[0]*c+x[0]*ss+a[0]*d*(1-c),v[1]*c+x[1]*ss+a[1]*d*(1-c),v[2]*c+x[2]*ss+a[2]*d*(1-c)];}
function ll3(v){return [deg(Math.atan2(v[2],Math.hypot(v[0],v[1]))),deg(Math.atan2(v[1],v[0]))];}
function unwrapLongitudes(points){
 if(points.length===0)return [];
 const out=[[points[0][0],points[0][1]]];let prev=points[0][1];
 for(let i=1;i<points.length;i++){let lon=points[i][1];while(lon-prev>180)lon-=360;while(lon-prev<-180)lon+=360;out.push([points[i][0],lon]);prev=lon}
 return out;
}
function longGreatCircle(lat1,lon1,lat2,lon2,n=72){
 const p=v3(lat1,lon1),q=v3(lat2,lon2),d=Math.acos(Math.max(-1,Math.min(1,dot3(p,q))));
 if(!isFinite(d)||d<1e-12)return [[lat1,lon1],[lat2,lon2]];
 const axis=norm3(cross3(p,q)).map(x=>-x),angle=2*Math.PI-d,pts=[];
 for(let i=0;i<=n;i++)pts.push(ll3(rot3(p,axis,angle*i/n)));
 pts[0]=[lat1,lon1];pts[pts.length-1]=[lat2,lon2];return unwrapLongitudes(pts);
}
function addWrappedCircleMarker(layer,lat,lon,options,popup){for(const shift of [-360,0,360]){const marker=L.circleMarker([lat,lon+shift],options);if(popup)marker.bindPopup(popup);marker.addTo(layer)}}
function normLon(lon){while(lon>180)lon-=360;while(lon<=-180)lon+=360;return lon;}
function solarPosition(date){
 const jd=date.getTime()/86400000+2440587.5,d=jd-2451545.0,g=rad((357.529+0.98560028*d)%360),q=(280.459+0.98564736*d)%360,
       L=rad((q+1.915*Math.sin(g)+0.020*Math.sin(2*g))%360),e=rad(23.439-0.00000036*d),ra=deg(Math.atan2(Math.cos(e)*Math.sin(L),Math.cos(L))),dec=deg(Math.asin(Math.sin(e)*Math.sin(L))),gmst=(280.46061837+360.98564736629*(jd-2451545.0))%360;
 return {lat:dec,lon:normLon(ra-gmst)};
}
function destination(lat,lon,bearing,distance){
 const p=rad(lat),l=rad(lon),b=rad(bearing),sd=Math.sin(distance),cd=Math.cos(distance),p2=Math.asin(Math.sin(p)*cd+Math.cos(p)*sd*Math.cos(b)),l2=l+Math.atan2(Math.sin(b)*sd*Math.cos(p),cd-Math.sin(p)*Math.sin(p2));
 return [deg(p2),deg(l2)];
}
function updateNightLayer(){
 nightLayer.clearLayers();const sun=solarPosition(new Date()),antiLat=-sun.lat,antiLon=normLon(sun.lon+180),ring=[];
 for(let b=0;b<=360;b+=2)ring.push(destination(antiLat,antiLon,b,Math.PI/2));
 const unwrapped=unwrapLongitudes(ring),poleLat=sun.lat>=0?-90:90,firstLon=unwrapped[0][1],lastLon=unwrapped[unwrapped.length-1][1],closed=[...unwrapped,[poleLat,lastLon],[poleLat,firstLon]];
 for(const shift of [-720,-360,0,360,720])L.polygon(closed.map(x=>[x[0],x[1]+shift]),{pane:'ucxNightPane',stroke:false,fillColor:'#101828',fillOpacity:.22,interactive:false,noClip:true}).addTo(nightLayer);
}
updateNightLayer();setInterval(updateNightLayer,60000);
function getBandLayer(bandCode,band){
 const key=String(bandCode);if(!bandLayers.has(key)){bandLayers.set(key,{code:Number(bandCode),display:band,enabled:true,color:colorForBand(band),points:L.layerGroup(),paths:L.layerGroup(),longPaths:L.layerGroup()});if(bandControlHost)renderBandControls()}
 return bandLayers.get(key);
}
function refreshBand(bandCode){
 const b=bandLayers.get(String(bandCode));if(!b)return;
 if(b.enabled&&showQsos){if(!map.hasLayer(b.points))b.points.addTo(map)}else if(map.hasLayer(b.points))map.removeLayer(b.points);
 if(b.enabled&&showPaths){if(!map.hasLayer(b.paths))b.paths.addTo(map)}else if(map.hasLayer(b.paths))map.removeLayer(b.paths);
 if(b.enabled&&showLong){if(!map.hasLayer(b.longPaths))b.longPaths.addTo(map)}else if(map.hasLayer(b.longPaths))map.removeLayer(b.longPaths);
}
function refreshAll(){for(const b of bandLayers.values())refreshBand(b.code);if(showStations){if(!map.hasLayer(stationLayer))stationLayer.addTo(map)}else if(map.hasLayer(stationLayer))map.removeLayer(stationLayer);if(showNight){if(!map.hasLayer(nightLayer))nightLayer.addTo(map)}else if(map.hasLayer(nightLayer))map.removeLayer(nightLayer);}
const stations=new Map();
function addQso(q,includeBounds=true){
 const b=getBandLayer(q.bandCode,q.band),extra=q.distance==null?'':`${q.distance.toFixed(1)} km · ${q.bearing?.toFixed(0)}°<br>`;
 const qPopup=`<b>${esc(q.call)}</b><br>${esc(q.date)} ${esc(q.time)} UTC<br>${esc(q.band)} · ${esc(q.mode)} · ${esc(q.locator)}<br>${extra}<small>${esc(q.source)}</small>`;
 addWrappedCircleMarker(b.points,q.lat,q.lon,{renderer:markerRenderer,radius:3,weight:1,color:b.color,fillColor:b.color,fillOpacity:.65},qPopup);if(includeBounds)bounds.push([q.lat,q.lon]);
 const key=`${q.station}|${q.slat}|${q.slon}`;
 if(!stations.has(key)){stations.set(key,{name:q.station,lat:q.slat,lon:q.slon});addWrappedCircleMarker(stationLayer,q.slat,q.slon,{renderer:markerRenderer,radius:7,weight:2,fillOpacity:.9},`<b>${esc(q.station)}</b><br>Stations-QTH`);if(includeBounds)bounds.push([q.slat,q.slon])}
 for(const segment of gc(q.slat,q.slon,q.lat,q.lon))L.polyline(segment,{renderer:pathRenderer,color:b.color,weight:pathWidths.shortpathWidth,opacity:.28,interactive:false}).addTo(b.paths);
 const lp=longGreatCircle(q.slat,q.slon,q.lat,q.lon);for(const shift of [-720,-360,0,360,720])L.polyline(lp.map(x=>[x[0],x[1]+shift]),{renderer:pathRenderer,color:b.color,weight:pathWidths.longpathWidth,opacity:.15,dashArray:'5 5',interactive:false}).addTo(b.longPaths);
 refreshBand(q.bandCode);
}
for(const q of qsos)addQso(q,true);
function ensureDxccBands(snapshot){let changed=false;for(const row of snapshot?.bands??[]){const key=String(row.bandCode);if(!bandLayers.has(key)){getBandLayer(row.bandCode,row.band);changed=true}}if(changed&&bandControlHost)renderBandControls()}
ensureDxccBands(dxccStatus);
window.ucxAppendQsos=function(items){for(const q of items)addQso(q,false);refreshAll()};
window.ucxReplaceQsos=function(items){for(const b of bandLayers.values()){b.points.clearLayers();b.paths.clearLayers();b.longPaths.clearLayers()}stationLayer.clearLayers();stations.clear();for(const q of items)addQso(q,false);refreshAll()};
for(const b of bandLayers.values()){b.points.addTo(map);b.paths.addTo(map)}
if(bounds.length)map.fitBounds(bounds,{padding:[20,20],maxZoom:7});

function selectedBandCodes(){return new Set([...bandLayers.values()].filter(x=>x.enabled).map(x=>x.code));}
function summarizeDxcc(properties){
 const selected=selectedBandCodes(),entityId=Number(properties?.entityId),catalogStatus=properties?.catalogStatus;
 if(catalogStatus!=='Current'||!Number.isInteger(entityId)||!dxccStatus?.available)return {status:'Unknown',qsoCount:0,worked:[],other:[],credited:[],noBands:selected.size===0};
 if(selected.size===0)return {status:'NotWorked',qsoCount:0,worked:[],other:[],credited:[],noBands:true};
 const rows=(dxccStatus.bands??[]).filter(x=>x.entityId===entityId&&selected.has(x.bandCode)),qsoCount=rows.reduce((n,x)=>n+x.qsoCount,0),worked=rows.filter(x=>x.qsoCount>0).map(x=>x.bandCode),other=rows.filter(x=>x.otherConfirmedCount>0).map(x=>x.bandCode),credited=rows.filter(x=>x.dxccCreditedCount>0).map(x=>x.bandCode);
 const status=qsoCount===0?'NotWorked':rows.reduce((best,x)=>(dxccStatusPriority[x.status]??0)>dxccStatusPriority[best]?x.status:best,'Worked');
 return {status,qsoCount,worked,other,credited,noBands:false};
}
function dxccStyle(feature){return dxccStyles[summarizeDxcc(feature?.properties).status]??dxccStyles.Unknown;}
function bandDisplay(code){const b=bandLayers.get(String(code));if(b)return b.display;const row=(dxccStatus.bands??[]).find(x=>x.bandCode===code);return row?.band??String(code)}
function formatBands(codes){return codes.length?[...new Set(codes)].sort((a,b)=>a-b).map(bandDisplay).join(', '):'–';}
function buildDxccPopup(properties){
 const summary=summarizeDxcc(properties),entity=Number.isInteger(Number(properties?.entityId))?`DXCC ${Number(properties.entityId)}`:'Nicht-DXCC-Geometrie',prefix=properties?.prefix?` · ${esc(properties.prefix)}`:'';
 const status=summary.noBands?'No bands selected':dxccStatusLabels[summary.status];
 return `<div class="ucx-dxcc-popup-title">${esc(properties?.name??'Unknown geometry')}</div><div class="ucx-dxcc-popup-meta">${esc(entity)}${prefix}</div><b>Status:</b> ${esc(status)}<br><b>Selected-band QSOs:</b> ${summary.qsoCount}<div class="ucx-dxcc-popup-section"><b>Worked bands:</b><br>${esc(formatBands(summary.worked))}</div><div class="ucx-dxcc-popup-section"><b>eQSL / ClubLog:</b><br>${esc(formatBands(summary.other))}</div><div class="ucx-dxcc-popup-section"><b>LoTW / Paper:</b><br>${esc(formatBands(summary.credited))}</div>`;
}
function reportDxccInteractionError(phase,error,properties){
 const id=Number(properties?.entityId),detail=error instanceof Error?error.message:String(error);
 try{window.chrome.webview.postMessage({type:'dxccError',phase,message:`${phase}${Number.isInteger(id)?` (DXCC ${id})`:''}: ${detail}`})}catch{}
}
function buildDxccPopupSafely(feature){
 const properties=feature?.properties??{};
 try{return buildDxccPopup(properties)}catch(error){
  reportDxccInteractionError('popup-content',error,properties);
  const id=Number(properties?.entityId);
  return `<div class="ucx-dxcc-popup-title">DXCC popup unavailable</div>${Number.isInteger(id)?`DXCC ${id}`:''}`
 }
}
function openDxccPopup(layer,latlng){
 try{layer.openPopup(latlng)}catch(error){reportDxccInteractionError('popup-open',error,layer?.feature?.properties)}
}
// With preferCanvas, the higher resident marker canvas is the DOM click target
// even over empty areas. Leaflet then never asks the lower DXCC canvas to hit-test.
// Reuse Leaflet's polygon hit test on otherwise-unhandled map clicks.
function dxccLayerAt(latlng){
 if(!showDxcc||!dxccGeometryReady||!dxccLayer||!map.hasLayer(dxccLayer))return null;
 const point=map.latLngToLayerPoint(latlng);let hit=null;
 dxccLayer.eachLayer(layer=>{
  if(layer.options?.interactive===false||typeof layer._containsPoint!=='function')return;
  if(layer._pxBounds&&!layer._pxBounds.contains(point))return;
  if(layer._containsPoint(point))hit=layer
 });
 return hit
}
function handleDxccMapClick(event){
 // A marker or another interactive Leaflet layer remains authoritative. This
 // fallback handles only a click whose Leaflet source is the map itself.
 if(event.sourceTarget&&event.sourceTarget!==map)return;
 const layer=dxccLayerAt(event.latlng);if(layer)openDxccPopup(layer,event.latlng)
}
map.on('click',handleDxccMapClick);
const DxccLegend=L.Control.extend({onAdd:function(){const div=L.DomUtil.create('div','ucx-dxcc-legend');div.innerHTML='<div class="ucx-title">DXCC status</div><div class="ucx-dxcc-legend-row"><span class="ucx-dxcc-legend-sample dxcc-notworked"></span>Not worked</div><div class="ucx-dxcc-legend-row"><span class="ucx-dxcc-legend-sample dxcc-worked"></span>Worked</div><div class="ucx-dxcc-legend-row"><span class="ucx-dxcc-legend-sample dxcc-other"></span>eQSL / ClubLog confirmed</div><div class="ucx-dxcc-legend-row"><span class="ucx-dxcc-legend-sample dxcc-credited"></span>LoTW / Paper confirmed</div><div class="ucx-dxcc-legend-row"><span class="ucx-dxcc-legend-sample dxcc-unknown"></span>Unknown / deleted</div><div id="dxcc-legend-note" class="ucx-dxcc-note"></div>';L.DomEvent.disableClickPropagation(div);return div}});
const dxccLegend=new DxccLegend({position:'bottomright'});dxccLegend.addTo(map);dxccLegend.getContainer().style.display='none';
function updateDxccLegend(){
 dxccLegend.getContainer().style.display=showDxcc&&dxccGeometryReady?'block':'none';const note=dxccLegend.getContainer().querySelector('#dxcc-legend-note');
 if(!note)return;if(!dxccStatus?.available)note.textContent='Status unavailable';else if(selectedBandCodes().size===0)note.textContent='No bands selected';else note.textContent='Selected bands: '+selectedBandCodes().size;
}
window.ucxRefreshDxccStyles=function(){
 const started=performance.now();if(dxccLayer)dxccLayer.eachLayer(layer=>{if(layer.setStyle)layer.setStyle(dxccStyle(layer.feature))});updateDxccLegend();
 if(dxccGeometryReady){try{window.chrome.webview.postMessage({type:'dxccPerformance',phase:'restyle',milliseconds:performance.now()-started});}catch{}}
};
window.ucxSetDxccVisible=function(visible){showDxcc=Boolean(visible)&&dxccConfig.geometryAvailable;if(dxccControlCheckbox)dxccControlCheckbox.checked=showDxcc;if(dxccLayer){if(showDxcc&&!map.hasLayer(dxccLayer))dxccLayer.addTo(map);else if(!showDxcc&&map.hasLayer(dxccLayer))map.removeLayer(dxccLayer)}updateDxccLegend();try{window.chrome.webview.postMessage({type:'dxccVisibility',visible:showDxcc})}catch{}};
window.ucxSetDxccStatus=function(snapshot){dxccStatus=snapshot??{available:false,bands:[]};ensureDxccBands(dxccStatus);window.ucxRefreshDxccStyles()};
async function initializeDxccGeometry(){
 if(!dxccConfig.geometryAvailable||!dxccConfig.url)return;
 const started=performance.now();
 try{
  const response=await fetch(dxccConfig.url);if(!response.ok)throw new Error(`HTTP ${response.status}`);const geoJson=await response.json();
  let popupBindingCount=0;
  dxccLayer=L.geoJSON(geoJson,{pane:'ucxDxccPane',interactive:true,style:dxccStyle,onEachFeature:(feature,layer)=>{layer.options.interactive=true;const id=Number(feature?.properties?.entityId);if(Number.isInteger(id)){if(!dxccFeatureLayers.has(id))dxccFeatureLayers.set(id,[]);dxccFeatureLayers.get(id).push(layer)}layer.bindPopup(()=>buildDxccPopupSafely(feature),{maxWidth:310});popupBindingCount++}});
  const featureCount=Array.isArray(geoJson?.features)?geoJson.features.length:0;if(popupBindingCount!==featureCount)reportDxccInteractionError('feature-binding',new Error(`bound ${popupBindingCount} of ${featureCount} features`));
  dxccGeometryReady=true;if(showDxcc)dxccLayer.addTo(map);window.ucxRefreshDxccStyles();
  try{window.chrome.webview.postMessage({type:'dxccPerformance',phase:'initialize',milliseconds:performance.now()-started,features:geoJson?.features?.length??0});}catch{}
 }catch(error){showDxcc=false;dxccGeometryReady=false;if(dxccControlCheckbox){dxccControlCheckbox.checked=false;dxccControlCheckbox.disabled=true;dxccControlCheckbox.closest('label')?.classList.add('ucx-disabled')}updateDxccLegend();try{window.chrome.webview.postMessage({type:'dxccError',phase:'initialize',message:String(error)});}catch{}}
}

function renderBandControls(){
 if(!bandControlHost)return;bandControlHost.innerHTML=[...bandLayers.values()].map(b=>`<label><input class="ctl-band" data-band-code="${b.code}" type="checkbox" ${b.enabled?'checked':''}><span class="ucx-swatch" style="background:${b.color}"></span>${esc(b.display)}</label>`).join('');
 bandControlHost.querySelectorAll('.ctl-band').forEach(cb=>cb.addEventListener('change',e=>{const b=bandLayers.get(String(e.target.dataset.bandCode));if(b){b.enabled=e.target.checked;refreshBand(b.code);window.ucxRefreshDxccStyles()}}));
}
const UcxControl=L.Control.extend({onAdd:function(){
 const div=L.DomUtil.create('div','ucx-control');L.DomEvent.disableClickPropagation(div);L.DomEvent.disableScrollPropagation(div);
 const dxccDisabled=dxccConfig.geometryAvailable?'':' disabled',dxccClass=dxccConfig.geometryAvailable?'':' class="ucx-disabled"',dxccTitle=esc(dxccConfig.diagnostic??'');
 div.innerHTML='<div class="ucx-title">Karteninhalt</div>'+'<label><input id="ctl-qso" type="checkbox" checked> QSOs</label>'+'<label><input id="ctl-path" type="checkbox" checked> Shortpath</label>'+'<label><input id="ctl-long" type="checkbox"> Longpath</label>'+'<label><input id="ctl-station" type="checkbox" checked> Stations-QTH</label>'+'<label><input id="ctl-night" type="checkbox"> Tag/Nacht</label>'+`<label${dxccClass} title="${dxccTitle}"><input id="ctl-dxcc" type="checkbox"${dxccDisabled}${showDxcc?' checked':''}> DXCC status</label>`+'<div class="ucx-dxcc-note">'+esc(dxccConfig.diagnostic??'')+'</div><hr><div class="ucx-title">Bänder</div><div id="ucx-band-controls"></div>';
 bandControlHost=div.querySelector('#ucx-band-controls');dxccControlCheckbox=div.querySelector('#ctl-dxcc');renderBandControls();
 setTimeout(()=>{
  div.querySelector('#ctl-qso').addEventListener('change',e=>{showQsos=e.target.checked;refreshAll()});div.querySelector('#ctl-path').addEventListener('change',e=>{showPaths=e.target.checked;refreshAll()});div.querySelector('#ctl-long').addEventListener('change',e=>{showLong=e.target.checked;refreshAll()});div.querySelector('#ctl-station').addEventListener('change',e=>{showStations=e.target.checked;refreshAll()});div.querySelector('#ctl-night').addEventListener('change',e=>{showNight=e.target.checked;refreshAll()});dxccControlCheckbox.addEventListener('change',e=>window.ucxSetDxccVisible(e.target.checked));
 },0);return div;
}});
map.addControl(new UcxControl({position:'topright'}));
initializeDxccGeometry();
function postView(){if(suppressViewMessage)return;const c=map.getCenter();if(window.chrome?.webview)window.chrome.webview.postMessage({type:'view',lat:c.lat,lon:c.lng,zoom:map.getZoom()});}
map.on('moveend',postView);
window.ucxApplyRemoteView=(lat,lon,zoom)=>{suppressViewMessage=true;map.setView([lat,lon],zoom,{animate:false});setTimeout(()=>suppressViewMessage=false,0)};
</script></body></html>
""";
    }

    public static string BuildQsoPayloadJson(IEnumerable<MapQsoPoint> points)
    {
        var payload = points.Select(p => new
        {
            p.Id,
            source = Path.GetFileName(p.SourceFile),
            call = p.CallSign,
            date = p.QsoDate,
            time = p.StartTime,
            bandCode = p.BandCode,
            band = UcxDisplayFormat.Band((byte)p.BandCode),
            mode = UcxDisplayFormat.Mode((byte)p.ModeCode),
            locator = p.Locator,
            lat = p.Latitude,
            lon = p.Longitude,
            station = p.StationProfileName,
            slat = p.StationLatitude,
            slon = p.StationLongitude,
            distance = p.DistanceKm,
            bearing = p.BearingDeg
        });

        return JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.Default });
    }
}
