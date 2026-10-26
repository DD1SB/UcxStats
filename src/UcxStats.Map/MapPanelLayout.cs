namespace UcxStats.Map;

internal static class MapPanelLayout
{
    public const string Styles = """
.ucx-panel{position:absolute!important;float:none!important;margin:0!important;z-index:800;box-sizing:border-box;overflow:hidden!important;max-height:none!important;max-width:calc(100% - 16px)}
.ucx-panel-header{display:flex;align-items:center;gap:5px;border-bottom:1px solid #dce2e5;padding-bottom:5px;margin-bottom:5px;white-space:nowrap}
.ucx-drag-handle{flex:1;cursor:grab;touch-action:none;user-select:none;font-weight:600;outline-offset:2px}.ucx-drag-handle:active{cursor:grabbing}
.ucx-panel-header button{border:0;background:#eef1f3;color:#263642;border-radius:3px;cursor:pointer;min-width:26px;min-height:24px}
.ucx-panel-body{overflow:auto;box-sizing:border-box}.ucx-panel-body[hidden]{display:none}.ucx-panel button{font:inherit}
.ucx-range{display:flex!important;align-items:center;gap:8px;flex-wrap:wrap;margin:7px 0!important;white-space:normal!important}
.ucx-range input[type=range]{flex:1;width:170px;min-width:100px;accent-color:#287b78;cursor:pointer}.ucx-range output{min-width:54px;text-align:right;font-variant-numeric:tabular-nums}
""";

    public const string Script = """
const mapPanels=new Map();let panelStack=800;
const panelDefaults={contents:{x:1,y:0},statistics:{x:0,y:1},timeline:{x:1,y:1}};
const panelKeys={contents:['contentsPanelPosition','contentsPanelCollapsed'],statistics:['statisticsPanelPosition','statisticsPanelCollapsed'],timeline:['timelinePanelPosition','liveTimelineSettingsCollapsed']};
function mapPanelMarkup(title,body){return `<div class="ucx-panel-header"><button type="button" class="ucx-panel-collapse" title="Ein-/ausklappen" aria-label="${esc(title)} ein-/ausklappen">▾</button><span class="ucx-drag-handle" tabindex="0" role="button" title="Ziehen oder mit Pfeiltasten verschieben; Pos1 setzt zurück" aria-label="${esc(title)} verschieben">⠿ ${esc(title)}</span></div><div class="ucx-panel-body">${body}</div>`}
function normalizedPanelPosition(value,fallback){return value&&Number.isFinite(value.x)&&Number.isFinite(value.y)?{x:Math.max(0,Math.min(1,value.x)),y:Math.max(0,Math.min(1,value.y))}:{...fallback}}
function panelPixelPosition(position,width,height,panelWidth,panelHeight){return {left:8+position.x*Math.max(0,width-panelWidth-16),top:8+position.y*Math.max(0,height-panelHeight-16)}}
function saveMapPresentation(){try{window.chrome.webview.postMessage({type:'mapPresentation',settings:statisticsSettings})}catch{}}
function placeMapPanel(state){
 const container=map.getContainer(),width=container.clientWidth,height=container.clientHeight;
 if(!width||!height)return;
 const panelWidth=Math.max(1,Math.min({contents:250,statistics:440,timeline:350}[state.id],width-16));
 state.element.style.width=panelWidth+'px';state.element.style.maxWidth=panelWidth+'px';
 state.body.style.maxHeight=Math.max(0,height-state.header.getBoundingClientRect().height-40)+'px';
 const rect=state.element.getBoundingClientRect(),position=normalizedPanelPosition(statisticsSettings[panelKeys[state.id][0]],panelDefaults[state.id]);
 const pixel=panelPixelPosition(position,width,height,rect.width,rect.height);
 state.left=pixel.left;state.top=pixel.top;
 state.element.style.left=pixel.left+'px';state.element.style.top=pixel.top+'px';
}
function applyPanelCollapse(state){const collapsed=!!statisticsSettings[panelKeys[state.id][1]];state.body.hidden=collapsed;state.collapse.textContent=collapsed?'▸':'▾';state.collapse.setAttribute('aria-expanded',String(!collapsed))}
function registerMapPanel(id,element){
 element.classList.add('ucx-panel');map.getContainer().appendChild(element);
 const raise=()=>{element.style.zIndex=String(++panelStack)};element.addEventListener('pointerdown',raise);element.addEventListener('focusin',raise);
 const state={id,element,body:element.querySelector('.ucx-panel-body'),header:element.querySelector('.ucx-panel-header'),collapse:element.querySelector('.ucx-panel-collapse'),handle:element.querySelector('.ucx-drag-handle'),drag:null,left:8,top:8};
 element.addEventListener('keydown',e=>{if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Home','End','PageUp','PageDown',' ','Enter'].includes(e.key)&&e.target.closest('input,select,button,summary,a'))e.stopPropagation()});
 mapPanels.set(id,state);applyPanelCollapse(state);placeMapPanel(state);
 state.collapse.addEventListener('click',e=>{e.stopPropagation();const key=panelKeys[id][1];statisticsSettings[key]=!statisticsSettings[key];applyPanelCollapse(state);placeMapPanel(state);saveMapPresentation()});
 state.handle.addEventListener('pointerdown',e=>{
  if((e.button!==undefined&&e.button!==0)||e.isPrimary===false||e.target.closest('button,input,select,summary,a'))return;
  e.preventDefault();e.stopPropagation();raise();state.handle.setPointerCapture(e.pointerId);
  state.drag={pointerId:e.pointerId,x:e.clientX,y:e.clientY,left:state.left,top:state.top,previous:statisticsSettings[panelKeys[id][0]]};
 });
 state.handle.addEventListener('pointermove',e=>{
  if(!state.drag||e.pointerId!==state.drag.pointerId)return;e.preventDefault();e.stopPropagation();
  const container=map.getContainer(),rect=element.getBoundingClientRect(),w=Math.max(0,container.clientWidth-rect.width-16),h=Math.max(0,container.clientHeight-rect.height-16);
  const x=Math.max(0,Math.min(w,state.drag.left-8+e.clientX-state.drag.x)),y=Math.max(0,Math.min(h,state.drag.top-8+e.clientY-state.drag.y));
  statisticsSettings[panelKeys[id][0]]={x:w?x/w:0,y:h?y/h:0};placeMapPanel(state);
 });
 function endDrag(e,cancel){if(!state.drag||e.pointerId!==state.drag.pointerId)return;e.stopPropagation();if(cancel)statisticsSettings[panelKeys[id][0]]=state.drag.previous;state.drag=null;placeMapPanel(state);if(!cancel)saveMapPresentation()}
 state.handle.addEventListener('pointerup',e=>endDrag(e,false));state.handle.addEventListener('pointercancel',e=>endDrag(e,true));
 state.handle.addEventListener('keydown',e=>{
  const delta={ArrowLeft:[-8,0],ArrowRight:[8,0],ArrowUp:[0,-8],ArrowDown:[0,8]}[e.key];
  if(e.key!=='Home'&&!delta)return;e.preventDefault();e.stopPropagation();
  const container=map.getContainer(),rect=element.getBoundingClientRect(),p=normalizedPanelPosition(statisticsSettings[panelKeys[id][0]],panelDefaults[id]);
  statisticsSettings[panelKeys[id][0]]=e.key==='Home'?null:{x:p.x+delta[0]/Math.max(1,container.clientWidth-rect.width-16),y:p.y+delta[1]/Math.max(1,container.clientHeight-rect.height-16)};
  statisticsSettings[panelKeys[id][0]]=normalizedPanelPosition(statisticsSettings[panelKeys[id][0]],panelDefaults[id]);placeMapPanel(state);saveMapPresentation();
 });
 if(typeof ResizeObserver!=='undefined'){state.observer=new ResizeObserver(()=>placeMapPanel(state));state.observer.observe(element)}
 return state;
}
function repositionMapPanels(){for(const state of mapPanels.values())if(!state.drag)placeMapPanel(state)}
window.ucxApplyPanelSettings=function(){for(const state of mapPanels.values()){applyPanelCollapse(state);placeMapPanel(state)}};
window.ucxResetPanelPositions=function(){for(const keys of Object.values(panelKeys))statisticsSettings[keys[0]]=null;repositionMapPanels();saveMapPresentation()};
window.addEventListener('resize',repositionMapPanels);
if(typeof ResizeObserver!=='undefined')new ResizeObserver(repositionMapPanels).observe(map.getContainer());
""";
}
