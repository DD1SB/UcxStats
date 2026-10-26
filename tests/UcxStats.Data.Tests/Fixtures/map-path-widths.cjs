'use strict';
const fs=require('node:fs');
const vm=require('node:vm');
const scenario=process.argv[2];
const data=JSON.parse(fs.readFileSync(0,'utf8'));
const {sandbox}=require('./map-runtime.cjs').createRuntime(data,{scenario:process.argv[2]});
// Allow the asynchronous DXCC fetch to finish before taking the baseline snapshot.
setImmediate(() => {
    vm.runInContext(`
const bands=[...bandLayers.values()];
const paths=()=>bands.flatMap(b=>[...b.paths.layers]);
const longPaths=()=>bands.flatMap(b=>[...b.longPaths.layers]);
const allPathLayers=()=>[...paths(),...longPaths()];
const weights=(layers,expected)=>{assert.ok(layers.length>0);for(const layer of layers)assert.equal(layer.options.weight,expected)};
if(scenario==='defaults'){weights(paths(),1.3);weights(longPaths(),1.0)}
if(scenario==='configured'){weights(paths(),1.75);weights(longPaths(),2.25)}

// Set a non-default view/selection/visibility before changing any widths.
map.setView([15,165],5);bands[1].enabled=false;
showLong=true;showQsos=false;showStations=false;showNight=true;refreshAll();
assert.ok(dxccGeometryReady&&map.hasLayer(dxccLayer));
const firstPaths=allPathLayers();
const pathOptions=firstPaths.map(layer=>({...layer.options}));
const otherLayers=[...bands.flatMap(b=>[...b.points.layers]),...stationLayer.layers,...nightLayer.layers,...dxccLayer.layers,baseTiles];
// A future unrelated layer in the shared renderer must remain untouched too.
const graticule=L.polyline([[0,0],[1,1]],{renderer:pathRenderer,weight:.75}).addTo(map);
otherLayers.push(graticule);
const otherOptions=otherLayers.map(layer=>({...layer.options}));
const otherStyleCalls=otherLayers.map(layer=>layer.styleCalls);
const visibleBefore=[...map.layers];
const stateBefore=()=>JSON.stringify({center:map.center,zoom:map.zoom,viewCalls:map.viewCalls,fitCalls:map.fitCalls,bands:bands.map(b=>b.enabled),showQsos,showPaths,showLong,showStations,showNight,showDxcc,dxccStatus});
const state=stateBefore();

if(scenario==='shortpath'){
 window.ucxSetPathWidths({shortpathWidth:2.5});weights(paths(),2.5);weights(longPaths(),1);
 for(const layer of paths())assert.equal(layer.styleCalls,1);
 for(const layer of longPaths())assert.equal(layer.styleCalls,0);
}
if(scenario==='longpath'){
 window.ucxSetPathWidths({longpathWidth:3.5});weights(paths(),1.3);weights(longPaths(),3.5);
 for(const layer of paths())assert.equal(layer.styleCalls,0);
 for(const layer of longPaths())assert.equal(layer.styleCalls,1);
}
if(scenario==='invalid'){
 for(const value of [undefined,null,0,-1,.49,8.01,1e20,NaN,Infinity,'2.5','wide',true,{},[]])window.ucxSetPathWidths({shortpathWidth:value,longpathWidth:value});
 window.ucxSetPathWidths(null);window.ucxSetPathWidths();
 weights(paths(),1.3);weights(longPaths(),1);
 assert.ok(allPathLayers().every(layer=>layer.styleCalls===0));
}
if(scenario==='live'){
 // Exercise exactly the script emitted by the C# WebView bridge.
 eval(updateScript);weights(paths(),2.5);weights(longPaths(),3.5);
}

// Resident instances, visibility, selection, view, DXCC and all non-weight path options survive.
assert.deepEqual(allPathLayers(),firstPaths);
assert.deepEqual([...map.layers],visibleBefore);
assert.equal(stateBefore(),state);
for(let i=0;i<firstPaths.length;i++){
 const {weight:before,...oldOptions}=pathOptions[i],{weight:after,...newOptions}=firstPaths[i].options;
 assert.deepEqual(newOptions,oldOptions);
}
for(let i=0;i<otherLayers.length;i++){
 assert.deepEqual({...otherLayers[i].options},otherOptions[i]);assert.equal(otherLayers[i].styleCalls,otherStyleCalls[i]);
}

if(scenario==='live'){
 const originalCount=paths().length,originalLongCount=longPaths().length;
 window.ucxAppendQsos([{...qsos[0],Id:3,lon:-170}]);
 assert.ok(paths().length>originalCount);assert.equal(longPaths().length,originalLongCount+5);
 weights(paths(),2.5);weights(longPaths(),3.5);
 window.ucxAppendQsos([{...qsos[0],Id:4,bandCode:99,band:'New band'}]);
 const newBand=bandLayers.get('99');
 weights([...newBand.paths.layers],2.5);weights([...newBand.longPaths.layers],3.5);
 const beforeReplace=allPathLayers();
 window.ucxReplaceQsos([{...qsos[1],Id:2,lat:41}]);
 assert.ok(allPathLayers().every(layer=>!beforeReplace.includes(layer)));
 weights(paths(),2.5);weights(longPaths(),3.5);
 assert.equal(bands[0].paths.layers.size,0);assert.equal(bands[1].enabled,false);
 assert.equal(stateBefore(),state);
 // Reset is another style-only update and also governs later additions.
 window.ucxSetPathWidths({shortpathWidth:1.3,longpathWidth:1});
 window.ucxAppendQsos([qsos[0]]);weights(paths(),1.3);weights(longPaths(),1);
 assert.equal(stateBefore(),state);
}
`, sandbox, { timeout: 5000 });
    console.log(`Passed generated-map scenario: ${scenario}`);
});
