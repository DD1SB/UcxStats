using System.Diagnostics;
using System.Text.Json;
using UcxStats.Data.Database;
using UcxStats.Data.Dxcc;
using UcxStats.Map;
using Xunit.Abstractions;

namespace UcxStats.Data.Tests;

public sealed class MapUiRefinementTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("normal")]
    [InlineData("multiple-windows")]
    [InlineData("empty-single")]
    [InlineData("bucket-boundaries")]
    [InlineData("partial-buckets")]
    [InlineData("rolling-duration")]
    [InlineData("corrections-deletions")]
    [InlineData("future-skew")]
    [InlineData("midnight-dst")]
    [InlineData("gap-classes")]
    [InlineData("timer-no-events")]
    [InlineData("panel-independent")]
    [InlineData("panel-resize")]
    [InlineData("panel-reset-collapse")]
    [InlineData("control-interactions")]
    [InlineData("slider-ordering")]
    [InlineData("threshold-fixed-limits")]
    [InlineData("threshold-edge-overflow")]
    [InlineData("threshold-outside-release")]
    [InlineData("threshold-pointercancel")]
    [InlineData("threshold-lostpointercapture")]
    [InlineData("threshold-blur")]
    [InlineData("threshold-pagehide")]
    [InlineData("threshold-hidden")]
    [InlineData("threshold-escape")]
    [InlineData("threshold-missed-release")]
    [InlineData("threshold-invalid-input")]
    [InlineData("threshold-invalid-pointer")]
    [InlineData("threshold-wide-restart")]
    [InlineData("threshold-settings-update")]
    [InlineData("snap-escape")]
    [InlineData("visibility-runtime")]
    [InlineData("settings-panel-control")]
    [InlineData("height-default")]
    [InlineData("height-restart")]
    [InlineData("height-pointer")]
    [InlineData("height-keyboard")]
    [InlineData("height-limits")]
    [InlineData("height-viewport")]
    [InlineData("height-scaling")]
    [InlineData("height-settings-update")]
    [InlineData("height-reset")]
    [InlineData("mode-boundaries")]
    [InlineData("mode-transitions")]
    [InlineData("mode-scaling")]
    [InlineData("mode-settings-access")]
    [InlineData("mode-visibility")]
    [InlineData("mode-viewport")]
    [InlineData("mode-keyboard")]
    [InlineData("mode-quiet-timer")]
    [InlineData("mode-startup")]
    [InlineData("render-performance")]
    public async Task GeneratedPageKeepsMapStateDuringUiAndTimelineChanges(string scenario)
    {
        var now=new DateTimeOffset(2026,1,1,12,0,0,TimeSpan.Zero);
        var a=new QsoStatisticsAccumulator();
        a.Add(1,"FIRST",2,"JO40AA","2026-01-01","11:58:00",281,true,100);
        a.Add(2,"LAST",4,"JO40AB","2026-01-01","11:59:00",281,true,200);
        a.Add(3,"NOLOC",4,"","2026-01-01","12:00:00",null,false,null);
        var snapshot=a.Build();
        var points=new[] {new MapQsoPoint(1,"a.ucx","FIRST","2026-01-01","11:58:00",2,0,"JO40AA",50,8,"Home",48,11,100,90),
            new MapQsoPoint(2,"a.ucx","LAST","2026-01-01","11:59:00",4,0,"JO40AB",50,8,"Home",48,11,200,90)};
        var status=DxccMapPayloadFactory.Create(new DxccStatusSnapshot([new(281,4,2,0,0)],0));
        var settings=new MapSettings {LiveTimelineHeightPx=scenario=="height-restart"?260:scenario=="mode-startup"?20:null};
        if(scenario=="threshold-wide-restart")
            settings=settings with {LiveWarningThresholdMinutes=1439,LiveProlongedGapThresholdMinutes=1440};
        var html=MapPageBuilder.Build(points,new("Test","https://tiles.example/{z}/{x}/{y}.png","Test",18),
            new(true,true,"https://ucxstats.local/dxcc.geojson",status,"ready"),statisticsSettings:settings,isLiveMap:scenario!="normal");
        if(scenario!="normal") Assert.DoesNotContain("aria-label=\"Live-QSO-Verlauf\" hidden",html);
        Assert.Contains("max-height:none!important",html);
        Assert.DoesNotContain("ucx-timeline-config",html);
        Assert.DoesNotContain(".ucx-timeline-heading button",html);
        Assert.DoesNotContain("grid-template-columns:auto minmax(0,1fr) auto",html);
        if(scenario=="normal")Assert.DoesNotContain("id=\"ucx-timeline-resize\"",html);
        if(scenario=="height-restart")Assert.Contains("--ucx-timeline-height:260px",html);
        var info=new ProcessStartInfo("node") {RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"Fixtures","map-ui-refinement.cjs"));info.ArgumentList.Add(scenario);
        using var process=Process.Start(info)!;
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new {html,
            updateScript=MapStatisticsPayload.BuildSnapshotScript(MapStatisticsPayload.Serialize(snapshot,settings)),
            timelineScript=LiveTimelinePayload.BuildScript(LiveTimelinePayload.Serialize(LiveTimelinePayload.Create(snapshot,now)))}));
        process.StandardInput.Close();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try {await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(entireProcessTree:true);throw;}
        var result=await stdout+await stderr;output.WriteLine(result);Assert.True(process.ExitCode==0,result);
    }
}
