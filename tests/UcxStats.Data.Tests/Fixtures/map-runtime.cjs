'use strict';
const assert = require('node:assert/strict');
const vm = require('node:vm');
const { performance } = require('node:perf_hooks');

function createRuntime(data, extra = {}) {
 const timers=[],messages=[],windowEvents=new Map(),documentEvents=new Map(),elements=new Map();
 let viewportHeight=848,mapContainer=null,activeElement=null;
 let now=Date.parse('2026-01-01T12:00:00Z');
 class Clock extends Date {constructor(...args){super(...(args.length?args:[now]))}static now(){return now}}
 class Element {
  constructor(tag='div',kind='') {this.tagName=tag.toUpperCase();this.kind=kind;this.style={};this.children=new Map();this.nodes=[];this.listeners=new Map();this.dataset={};this.attributes={};this.innerHTML='';this.value='';this.checked=false;this.hidden=false;this.clientWidth=1000;this.clientHeight=700;const classes=new Set();this.classList={add:x=>classes.add(x),remove:x=>classes.delete(x),contains:x=>classes.has(x)};}
  querySelector(selector) {
   if(!this.children.has(selector)){
    const id=selector.startsWith('#')?selector.slice(1):null;
    const markup=id?this.innerHTML.match(new RegExp('<([a-z]+)[^>]*id="'+id+'"[^>]*>')):null;
    const el=new Element(markup?.[1]??(selector.includes('handle')?'span':selector.includes('collapse')?'button':'div'));
    if(markup)for(const match of markup[0].matchAll(/([\w-]+)="([^"]*)"/g)){el.attributes[match[1]]=match[2];if(['value','min','max','step','type'].includes(match[1]))el[match[1]]=match[2]}
    this.children.set(selector,el);if(id)elements.set(id,el);
   }
   return this.children.get(selector);
  }
  querySelectorAll(){if(this._bandMarkup===this.innerHTML)return this._bandElements;this._bandMarkup=this.innerHTML;return this._bandElements=[...this.innerHTML.matchAll(/data-band-code="(\d+)"[^>]*>/g)].map(match=>{const el=new Element('input');el.dataset.bandCode=match[1];el.checked=match[0].includes('checked');return el})}
  addEventListener(name,handler){this.listeners.set(name,handler)}
  setAttribute(name,value){this.attributes[name]=String(value)}
  getAttribute(name){return this.attributes[name]}
  appendChild(element){if(element.parent)element.parent.nodes=element.parent.nodes.filter(x=>x!==element);this.nodes.push(element);element.parent=this;return element}
  getBoundingClientRect(){const collapsed=this.children.get('.ucx-panel-body')?.hidden;const width=parseFloat(this.style.width)||(this.kind==='ucx-statistics'?400:this.kind==='ucx-timeline-settings'?340:this.kind==='ucx-control'?250:200);return {width:Math.min(width,parseFloat(this.style.maxWidth)||width),height:this.kind?(collapsed?44:Math.min(280,Number.isFinite(parseFloat(this.children.get('.ucx-panel-body')?.style.maxHeight))?parseFloat(this.children.get('.ucx-panel-body').style.maxHeight)+40:280)):30,left:parseFloat(this.style.left)||0,top:parseFloat(this.style.top)||0}}
  closest(selectors){return selectors.split(',').some(x=>x.trim().toUpperCase()===this.tagName)?this:null}
  focus(){activeElement=this;this.focused=true}
  setPointerCapture(id){this.capturedPointer=id}
  hasPointerCapture(id){return this.capturedPointer===id}
  releasePointerCapture(id){if(!this.hasPointerCapture(id))return;this.capturedPointer=null;this.listeners.get('lostpointercapture')?.({pointerId:id,stopPropagation(){}})}
 }
 class Layer {constructor(kind,options={}){this.kind=kind;this.options={...options};this.styleCalls=0}addTo(target){target.addLayer(this);return this}bindPopup(content){this.popup=content;return this}setStyle(style){Object.assign(this.options,style);this.styleCalls++;return this}}
 class Group extends Layer {constructor(){super('group');this.layers=new Set()}addLayer(layer){this.layers.add(layer);return this}eachLayer(fn){this.layers.forEach(fn);return this}clearLayers(){this.layers.clear();return this}}
 class MapDouble extends Group {
  constructor(){super();this.panes=new Map();this.events=new Map();this.viewCalls=0;this.fitCalls=0;this.container=elements.get('map')??new Element();mapContainer=this.container;this.invalidations=0;
   const footerHeight=()=>{const footer=elements.get('ucx-live-timeline');return footer&&!footer.hidden?footer.getBoundingClientRect().height:0};
   Object.defineProperty(this.container,'clientHeight',{get:()=>Math.max(0,viewportHeight-footerHeight()),set:value=>{viewportHeight=value+footerHeight()}});
   const body=new Element();Object.defineProperty(body,'clientHeight',{get:()=>viewportHeight});this.container.parentElement=body}
  createPane(name){this.panes.set(name,new Element())}getPane(name){return this.panes.get(name)}getContainer(){return this.container}
  setView(center,zoom){this.center=center;this.zoom=zoom;this.viewCalls++;return this}fitBounds(){this.fitCalls++;return this}
  hasLayer(layer){return this.layers.has(layer)}removeLayer(layer){this.layers.delete(layer);return this}on(name,fn){this.events.set(name,fn);return this}addControl(control){control.addTo(this)}
  getCenter(){return {lat:this.center[0],lng:this.center[1]}}getZoom(){return this.zoom}invalidateSize(options){this.invalidations++;this.sizeOptions=options;this.events.get('moveend')?.();return this}
 }
 const L={map:()=>new MapDouble(),canvas:options=>({...options}),tileLayer:(_url,options)=>Object.assign(new Layer('tiles',options),{on(){return this}}),layerGroup:()=>new Group(),circleMarker:(_c,o)=>new Layer('marker',o),polyline:(_c,o)=>new Layer('path',o),polygon:(_c,o)=>new Layer('polygon',o),geoJSON:(geo,options)=>{const group=new Group();for(const feature of geo.features){const layer=new Layer('dxcc',options.style(feature));layer.feature=feature;options.onEachFeature(feature,layer);group.addLayer(layer)}return group},Control:{extend:definition=>class{addTo(map){this.container=definition.onAdd.call(this,map);return this}getContainer(){return this.container}}},DomUtil:{create:(_tag,kind)=>new Element('div',kind)},DomEvent:{disableClickPropagation(el){el.clickBlocked=true},disableScrollPropagation(el){el.scrollBlocked=true}}};
 for(const match of data.html.matchAll(/<(section|svg|button|span|div)[^>]*id="([^"]*)"[^>]*>/g)){const el=new Element(match[1]);el.hidden=match[0].includes(' hidden');for(const a of match[0].matchAll(/([\w-]+)="([^"]*)"/g))el.setAttribute(a[1],a[2]);elements.set(match[2],el);
  if(match[2]==='ucx-live-timeline'){
   const initial=match[0].match(/--ucx-timeline-height:(\d+)px/);
   el.getBoundingClientRect=()=>({width:mapContainer?.clientWidth??1000,height:el.hidden?0:Math.max(0,Math.min(parseFloat(el.style.height)||(initial?Number(initial[1]):(mapContainer?.clientWidth??1000)<=750?174:148),viewportHeight-80)),left:0,top:0});
  }
  if(match[2]==='ucx-timeline-chart'){
   Object.defineProperty(el,'clientWidth',{get:()=>Math.max(1,(mapContainer?.clientWidth??1000)-(elements.get('ucx-live-timeline')?.dataset.presentationMode==='full'?24:0))});
   Object.defineProperty(el,'clientHeight',{get:()=>Math.max(0,(elements.get('ucx-live-timeline')?.getBoundingClientRect().height??148)-(elements.get('ucx-live-timeline')?.dataset.presentationMode==='full'?60:1))});
  }}
 const sandbox=vm.createContext({L,assert,console,performance,Date:Clock,timers,messages,Element,windowEvents,documentEvents,...data,...extra,
  advance:seconds=>{now+=seconds*1000},setNow:ms=>{now=ms},setViewport:(width,height)=>{mapContainer.clientWidth=width;viewportHeight=height},
  document:{getElementById:id=>elements.get(id)??null,get activeElement(){return activeElement},hidden:false,addEventListener:(name,fn)=>documentEvents.set(name,fn)},
  window:{addEventListener:(name,fn)=>{const previous=windowEvents.get(name);windowEvents.set(name,e=>{previous?.(e);fn(e)})},chrome:{webview:{postMessage:message=>messages.push(JSON.parse(JSON.stringify(message)))}},location:{reload(){assert.fail('Page reload')}}},
  setTimeout:fn=>{fn();return 1},setInterval:(fn,ms)=>{timers.push({fn,ms});return timers.length},
  fetch:async()=>({ok:true,json:async()=>({features:[{properties:{entityId:281,catalogStatus:'Current'}}]})})
 });
 vm.runInContext(data.html.match(/<script>\s*([\s\S]*?)<\/script>/)[1],sandbox,{timeout:5000});
 return {sandbox,run:script=>vm.runInContext(script,sandbox,{timeout:10000})};
}
module.exports={createRuntime};
