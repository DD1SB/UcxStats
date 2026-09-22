using System.Text.Encodings.Web;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using UcxStats.Data.Database;
using UcxStats.Core.Ucx;

namespace UcxStats.Wpf;

public partial class MapWindow : Window
{
    private IReadOnlyList<MapQsoPoint> _points;
    private readonly Guid _syncId = Guid.NewGuid();
    private bool _fullscreen;
    private readonly MapTileSettings _tileSettings = MapTileSettings.Load();
    private readonly string _mapWebFolder;
    private const string MapVirtualHost = "ucxstats.local";
    private WindowStyle _oldStyle;
    private WindowState _oldState;
    private ResizeMode _oldResizeMode;

    public MapWindow(IReadOnlyList<MapQsoPoint> points, string title)
    {
        InitializeComponent();
        _points = points;
        _mapWebFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UcxStats",
            "MapWeb",
            _syncId.ToString("N"));
        Directory.CreateDirectory(_mapWebFolder);
        HeaderText.Text = title;
        Loaded += MapWindow_Loaded;
        Closed += MapWindow_Closed;
        MapSyncService.ViewChanged += MapSyncService_ViewChanged;
    }

    private async void MapWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Karte wird vorbereitet…";
            await MapWebView.EnsureCoreWebView2Async();
            var core = MapWebView.CoreWebView2;
            if (core is null)
                throw new InvalidOperationException("WebView2 konnte nicht initialisiert werden.");
            ConfigureWebView(core);
            core.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            core.WebMessageReceived += CoreWebView2_WebMessageReceived;
            NavigateMap(_points);
            StatusText.Text = $"{_points.Count:N0} QSOs mit Locator und Stations-QTH · Tiles: {_tileSettings.ProviderName}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Karte konnte nicht geladen werden.";
            MessageBox.Show(this, ex.ToString(), "UcxStats Karte", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task UpdateDataAsync(IReadOnlyList<MapQsoPoint> points, string title)
    {
        _points = points;
        HeaderText.Text = title;
        StatusText.Text = "Kartendaten werden aktualisiert…";
        if (MapWebView.CoreWebView2 is null)
        {
            await MapWebView.EnsureCoreWebView2Async();
            var core = MapWebView.CoreWebView2;
            if (core is null)
                return;
            ConfigureWebView(core);
            core.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }
        NavigateMap(_points);
        StatusText.Text = $"{_points.Count:N0} QSOs mit Locator und Stations-QTH · Tiles: {_tileSettings.ProviderName}.";
    }

    public async Task UpdateLiveDataAsync(IReadOnlyList<MapQsoPoint> points, string title)
    {
        HeaderText.Text = title;

        if (MapWebView.CoreWebView2 is null)
        {
            await UpdateDataAsync(points, title);
            return;
        }

        var oldIds = _points.Select(x => x.Id).ToHashSet();
        var newIds = points.Select(x => x.Id).ToHashSet();

        // Existing rows changed or disappeared: use a full in-window reload.
        // New QSOs, which are the normal live case, are appended incrementally.
        if (!oldIds.IsSubsetOf(newIds))
        {
            await UpdateDataAsync(points, title);
            return;
        }

        var added = points.Where(x => !oldIds.Contains(x.Id)).ToList();
        if (added.Count == 0)
        {
            _points = points;
            StatusText.Text = $"{_points.Count:N0} QSOs · keine neuen kartierbaren QSOs.";
            return;
        }

        var oldBands = _points.Select(x => x.BandCode).ToHashSet();
        if (added.Any(x => !oldBands.Contains(x.BandCode)))
        {
            // A brand-new band also needs a new band checkbox in the Leaflet control.
            await UpdateDataAsync(points, title);
            return;
        }

        var json = BuildPayloadJson(added);
        await MapWebView.ExecuteScriptAsync(
            $"window.ucxAppendQsos && window.ucxAppendQsos({json});");

        _points = points;
        StatusText.Text = $"{_points.Count:N0} QSOs · +{added.Count:N0} live hinzugefügt.";
    }

    private void MapWindow_Closed(object? sender, EventArgs e)
    {
        MapSyncService.ViewChanged -= MapSyncService_ViewChanged;
        if (MapWebView.CoreWebView2 is not null)
            MapWebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;

        try
        {
            if (Directory.Exists(_mapWebFolder))
                Directory.Delete(_mapWebFolder, recursive: true);
        }
        catch
        {
            // Temporary map files are harmless if cleanup is delayed by WebView2.
        }
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type))
                return;

            if (type.GetString() == "tileError")
            {
                StatusText.Text =
                    $"Kartentiles konnten nicht geladen werden ({_tileSettings.ProviderName}). " +
                    $"Provider-Konfiguration: {MapTileSettings.SettingsPath}";
                return;
            }

            if (type.GetString() != "view" || SyncMapsCheck.IsChecked != true)
                return;

            MapSyncService.Publish(
                _syncId,
                root.GetProperty("lat").GetDouble(),
                root.GetProperty("lon").GetDouble(),
                root.GetProperty("zoom").GetDouble());
        }
        catch
        {
            // Ignore malformed browser messages.
        }
    }

    private async void MapSyncService_ViewChanged(MapViewState state)
    {
        if (state.SourceId == _syncId || SyncMapsCheck.IsChecked != true || MapWebView.CoreWebView2 is null)
            return;

        try
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            await MapWebView.ExecuteScriptAsync(
                $"window.ucxApplyRemoteView && window.ucxApplyRemoteView({state.Latitude.ToString(culture)},{state.Longitude.ToString(culture)},{state.Zoom.ToString(culture)});");
        }
        catch
        {
            // Map synchronization is best-effort while a WebView reloads or closes.
        }
    }

    private void ConfigureWebView(CoreWebView2 core)
    {
        core.SetVirtualHostNameToFolderMapping(
            MapVirtualHost,
            _mapWebFolder,
            CoreWebView2HostResourceAccessKind.Allow);

        // Keep the normal Edge/WebView2 UA but identify the actual application.
        var userAgent = core.Settings.UserAgent ?? string.Empty;
        if (!userAgent.Contains("UcxStats/", StringComparison.OrdinalIgnoreCase))
            core.Settings.UserAgent = $"{userAgent} UcxStats/0.12.3.3".Trim();
    }

    private void NavigateMap(IReadOnlyList<MapQsoPoint> points)
    {
        var htmlPath = Path.Combine(_mapWebFolder, "map.html");
        File.WriteAllText(htmlPath, BuildHtml(points, _tileSettings));
        var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        MapWebView.CoreWebView2?.Navigate($"https://{MapVirtualHost}/map.html?v={cacheBust}");
    }

    private static string BuildPayloadJson(IEnumerable<MapQsoPoint> points)
    {
        var payload = points.Select(p => new
        {
            p.Id,
            source = Path.GetFileName(p.SourceFile),
            call = p.CallSign,
            date = p.QsoDate,
            time = p.StartTime,
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

    private static string BuildHtml(IReadOnlyList<MapQsoPoint> points, MapTileSettings tileSettings)
    {
        var json = BuildPayloadJson(points);
        var tileUrlJson = JsonSerializer.Serialize(tileSettings.UrlTemplate);
        var tileAttributionJson = JsonSerializer.Serialize(tileSettings.Attribution);
        var tileProviderJson = JsonSerializer.Serialize(tileSettings.ProviderName);

        return $$$"""
<!doctype html><html><head><meta charset="utf-8">
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css">
<style>
html,body,#map{height:100%;margin:0}body{font-family:Segoe UI,Arial,sans-serif}.leaflet-popup-content{line-height:1.35}
.ucx-control{background:#fff;padding:8px 10px;border-radius:4px;box-shadow:0 1px 5px #777;max-height:70vh;overflow:auto;min-width:145px}
.ucx-control label{display:block;white-space:nowrap;margin:2px 0;cursor:pointer}.ucx-control hr{border:0;border-top:1px solid #ddd;margin:6px 0}
.ucx-swatch{display:inline-block;width:12px;height:4px;margin:0 5px 2px 2px;vertical-align:middle;border-radius:2px}
.ucx-title{font-weight:600;margin-bottom:4px}
</style>
</head><body><div id="map"></div>
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
<script>
const qsos={{{json}}};
const map=L.map('map',{preferCanvas:true,worldCopyJump:true}).setView([30,10],2);
const tileProvider={{{tileProviderJson}}};
const baseTiles=L.tileLayer({{{tileUrlJson}}},{maxZoom:{{{tileSettings.MaxZoom}}},attribution:{{{tileAttributionJson}}}});
let tileErrorReported=false;
baseTiles.on('tileerror',e=>{
 if(tileErrorReported)return;
 tileErrorReported=true;
 try{window.chrome.webview.postMessage({type:'tileError',provider:tileProvider,url:e?.tile?.src??''});}catch{}
});
baseTiles.addTo(map);
const renderer=L.canvas({padding:.5}), stationLayer=L.layerGroup().addTo(map), nightLayer=L.layerGroup(), bounds=[];
const bandLayers=new Map();
let showQsos=true, showPaths=true, showLong=false, showStations=true, showNight=false, suppressViewMessage=false;
const bandColors={
 '160 m':'#6a3d9a','80 m':'#1f78b4','40 m':'#33a02c','30 m':'#b15928','20 m':'#e31a1c','17 m':'#ff7f00',
 '15 m':'#f1c40f','12 m':'#8e44ad','10 m':'#00a6a6','6 m':'#e84393','2 m':'#2d3436','70 cm':'#636e72',
 '23 cm':'#0984e3','13 cm':'#00b894','3,4 GHz':'#d63031','5,6 GHz':'#e17055','10 GHz':'#6c5ce7'
};
const fallbackColors=['#1f78b4','#33a02c','#e31a1c','#ff7f00','#6a3d9a','#00a6a6','#b15928','#e84393'];
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
 const out=[[points[0][0],points[0][1]]];
 let prev=points[0][1];
 for(let i=1;i<points.length;i++){
  let lon=points[i][1];
  while(lon-prev>180)lon-=360;
  while(lon-prev<-180)lon+=360;
  out.push([points[i][0],lon]);
  prev=lon;
 }
 return out;
}
function longGreatCircle(lat1,lon1,lat2,lon2,n=72){
 const p=v3(lat1,lon1),q=v3(lat2,lon2),d=Math.acos(Math.max(-1,Math.min(1,dot3(p,q))));
 if(!isFinite(d)||d<1e-12)return [[lat1,lon1],[lat2,lon2]];
 const axis=norm3(cross3(p,q)).map(x=>-x),angle=2*Math.PI-d,pts=[];
 for(let i=0;i<=n;i++)pts.push(ll3(rot3(p,axis,angle*i/n)));
 pts[0]=[lat1,lon1];
 pts[pts.length-1]=[lat2,lon2];
 return unwrapLongitudes(pts);
}
function addWrappedCircleMarker(layer,lat,lon,options,popup){
 for(const shift of [-360,0,360]){
  const marker=L.circleMarker([lat,lon+shift],options);
  if(popup)marker.bindPopup(popup);
  marker.addTo(layer);
 }
}
function normLon(lon){while(lon>180)lon-=360;while(lon<=-180)lon+=360;return lon;}
function solarPosition(date){
 const jd=date.getTime()/86400000+2440587.5,d=jd-2451545.0,
       g=rad((357.529+0.98560028*d)%360),q=(280.459+0.98564736*d)%360,
       L=rad((q+1.915*Math.sin(g)+0.020*Math.sin(2*g))%360),e=rad(23.439-0.00000036*d),
       ra=deg(Math.atan2(Math.cos(e)*Math.sin(L),Math.cos(L))),dec=deg(Math.asin(Math.sin(e)*Math.sin(L))),
       gmst=(280.46061837+360.98564736629*(jd-2451545.0))%360;
 return {lat:dec,lon:normLon(ra-gmst)};
}
function destination(lat,lon,bearing,distance){
 const p=rad(lat),l=rad(lon),b=rad(bearing),sd=Math.sin(distance),cd=Math.cos(distance),
       p2=Math.asin(Math.sin(p)*cd+Math.cos(p)*sd*Math.cos(b)),
       l2=l+Math.atan2(Math.sin(b)*sd*Math.cos(p),cd-Math.sin(p)*Math.sin(p2));
 return [deg(p2),deg(l2)];
}
function updateNightLayer(){
 nightLayer.clearLayers();
 const sun=solarPosition(new Date()),antiLat=-sun.lat,antiLon=normLon(sun.lon+180),ring=[];
 for(let b=0;b<=360;b+=2)ring.push(destination(antiLat,antiLon,b,Math.PI/2));
 const unwrapped=unwrapLongitudes(ring);
 // The terminator is only the boundary. Close the night polygon over the
 // geographic pole that lies on the night side; otherwise Leaflet closes
 // first/last terminator points with an artificial horizontal chord.
 const poleLat=sun.lat>=0?-90:90;
 const firstLon=unwrapped[0][1],lastLon=unwrapped[unwrapped.length-1][1];
 const closed=[...unwrapped,[poleLat,lastLon],[poleLat,firstLon]];
 for(const shift of [-720,-360,0,360,720]){
  const copy=closed.map(x=>[x[0],x[1]+shift]);
  L.polygon(copy,{stroke:false,fillColor:'#101828',fillOpacity:.22,interactive:false,noClip:true}).addTo(nightLayer);
 }
}
updateNightLayer();
setInterval(updateNightLayer,60000);
function getBandLayer(band){
 if(!bandLayers.has(band))bandLayers.set(band,{enabled:true,color:colorForBand(band),points:L.layerGroup(),paths:L.layerGroup(),longPaths:L.layerGroup()});
 return bandLayers.get(band);
}
function refreshBand(band){
 const b=bandLayers.get(band); if(!b)return;
 if(b.enabled&&showQsos){if(!map.hasLayer(b.points))b.points.addTo(map)}else if(map.hasLayer(b.points))map.removeLayer(b.points);
 if(b.enabled&&showPaths){if(!map.hasLayer(b.paths))b.paths.addTo(map)}else if(map.hasLayer(b.paths))map.removeLayer(b.paths);
 if(b.enabled&&showLong){if(!map.hasLayer(b.longPaths))b.longPaths.addTo(map)}else if(map.hasLayer(b.longPaths))map.removeLayer(b.longPaths);
}
function refreshAll(){for(const band of bandLayers.keys())refreshBand(band);if(showStations){if(!map.hasLayer(stationLayer))stationLayer.addTo(map)}else if(map.hasLayer(stationLayer))map.removeLayer(stationLayer);if(showNight){if(!map.hasLayer(nightLayer))nightLayer.addTo(map)}else if(map.hasLayer(nightLayer))map.removeLayer(nightLayer);}
const stations=new Map();
function addQso(q,includeBounds=true){
 const b=getBandLayer(q.band), extra=q.distance==null?'':`${q.distance.toFixed(1)} km · ${q.bearing?.toFixed(0)}°<br>`;
 const qPopup=`<b>${esc(q.call)}</b><br>${esc(q.date)} ${esc(q.time)} UTC<br>${esc(q.band)} · ${esc(q.mode)} · ${esc(q.locator)}<br>${extra}<small>${esc(q.source)}</small>`;
 addWrappedCircleMarker(b.points,q.lat,q.lon,{renderer,radius:3,weight:1,color:b.color,fillColor:b.color,fillOpacity:.65},qPopup);
 if(includeBounds)bounds.push([q.lat,q.lon]);

 const key=`${q.station}|${q.slat}|${q.slon}`;
 if(!stations.has(key)){
  stations.set(key,{name:q.station,lat:q.slat,lon:q.slon});
  addWrappedCircleMarker(stationLayer,q.slat,q.slon,{renderer,radius:7,weight:2,fillOpacity:.9},`<b>${esc(q.station)}</b><br>Stations-QTH`);
  if(includeBounds)bounds.push([q.slat,q.slon]);
 }

 for(const segment of gc(q.slat,q.slon,q.lat,q.lon))
  L.polyline(segment,{renderer,color:b.color,weight:1.3,opacity:.28,interactive:false}).addTo(b.paths);

 const lp=longGreatCircle(q.slat,q.slon,q.lat,q.lon);
 for(const shift of [-720,-360,0,360,720]){
  const copy=lp.map(x=>[x[0],x[1]+shift]);
  L.polyline(copy,{renderer,color:b.color,weight:1.0,opacity:.15,dashArray:'5 5',interactive:false}).addTo(b.longPaths);
 }

 refreshBand(q.band);
}
for(const q of qsos)addQso(q,true);
window.ucxAppendQsos=function(items){
 for(const q of items)addQso(q,false);
 refreshAll();
};
for(const b of bandLayers.values()){b.points.addTo(map);b.paths.addTo(map)}
if(bounds.length)map.fitBounds(bounds,{padding:[20,20],maxZoom:7});

const UcxControl=L.Control.extend({onAdd:function(){
 const div=L.DomUtil.create('div','ucx-control');L.DomEvent.disableClickPropagation(div);L.DomEvent.disableScrollPropagation(div);
 div.innerHTML='<div class="ucx-title">Karteninhalt</div>'+
  '<label><input id="ctl-qso" type="checkbox" checked> QSOs</label>'+
  '<label><input id="ctl-path" type="checkbox" checked> Shortpath</label>'+ 
  '<label><input id="ctl-long" type="checkbox"> Longpath</label>'+
  '<label><input id="ctl-station" type="checkbox" checked> Stations-QTH</label>'+
  '<label><input id="ctl-night" type="checkbox"> Tag/Nacht</label><hr><div class="ucx-title">Bänder</div>'+
  [...bandLayers.keys()].map(b=>`<label><input class="ctl-band" data-band="${esc(b)}" type="checkbox" checked><span class="ucx-swatch" style="background:${bandLayers.get(b).color}"></span>${esc(b)}</label>`).join('');
 setTimeout(()=>{
  div.querySelector('#ctl-qso').addEventListener('change',e=>{showQsos=e.target.checked;refreshAll()});
  div.querySelector('#ctl-path').addEventListener('change',e=>{showPaths=e.target.checked;refreshAll()});
  div.querySelector('#ctl-long').addEventListener('change',e=>{showLong=e.target.checked;refreshAll()});
  div.querySelector('#ctl-station').addEventListener('change',e=>{showStations=e.target.checked;refreshAll()});
  div.querySelector('#ctl-night').addEventListener('change',e=>{showNight=e.target.checked;refreshAll()});
  div.querySelectorAll('.ctl-band').forEach(cb=>cb.addEventListener('change',e=>{const b=bandLayers.get(e.target.dataset.band);if(b){b.enabled=e.target.checked;refreshBand(e.target.dataset.band)}}));
 },0);
 return div;
}});
map.addControl(new UcxControl({position:'topright'}));
function postView(){if(suppressViewMessage)return;const c=map.getCenter();if(window.chrome?.webview)window.chrome.webview.postMessage({type:'view',lat:c.lat,lon:c.lng,zoom:map.getZoom()});}
map.on('moveend',postView);
window.ucxApplyRemoteView=(lat,lon,zoom)=>{suppressViewMessage=true;map.setView([lat,lon],zoom,{animate:false});setTimeout(()=>suppressViewMessage=false,0);};
</script></body></html>
""";
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11 || (e.Key == Key.Escape && _fullscreen)) { ToggleFullscreen(); e.Handled = true; }
    }
    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _oldStyle=WindowStyle; _oldState=WindowState; _oldResizeMode=ResizeMode;
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; WindowState=WindowState.Maximized; _fullscreen=true;
        }
        else
        {
            WindowStyle=_oldStyle; ResizeMode=_oldResizeMode; WindowState=_oldState; _fullscreen=false;
        }
    }
}
