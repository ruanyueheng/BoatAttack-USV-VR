using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BoatAttack.Fishing
{
    /// <summary>Explicit bounded capture; ordinary gameplay has no per-frame capture work.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class FishingPerformanceCapture : MonoBehaviour
    {
        public const string EnableFlag="-fishing-performance-capture";
        private const int Capacity=131072;
        private const double Duration=600;
        private static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
        private struct Sample
        {
            public int Frame,State,Quality,Target,Gc0,Gc1,Gc2,Segment,CameraFrame;
            public double Seconds,Wall,Cpu,Main,Render,Gpu;
            public ulong TimingStamp;
            public Vector3 Position;
            public float Daylight;
            public long Allocated,Managed,Native,Graphics;
        }
        private FishingPrototype _c;
        private Sample[] _samples;
        private readonly FrameTiming[] _timing=new FrameTiming[1];
        private int _count,_gc0,_gc1,_gc2;
        private int _lastWorldRenderFrame=-1000;
        private double _started,_previous,_nextMemory;
        private long _allocated,_managed,_native,_graphics;
        private ulong _lastTimingStamp;
        private bool _timingsEnabled,_stopped;
        private string _metadata;
        public string OutputPath { get; private set; }
        public int Segment { get; set; }

        public static void AttachIfRequested(FishingPrototype c)
        {
            bool voyage=Array.IndexOf(Environment.GetCommandLineArgs(),FishingPerformanceVoyage.EnableFlag)>=0;
            if((!voyage && Array.IndexOf(Environment.GetCommandLineArgs(),EnableFlag)<0) || c.GetComponent<FishingPerformanceCapture>()!=null)return;
            var capture=c.gameObject.AddComponent<FishingPerformanceCapture>();capture.Begin(c);
            if(voyage)c.gameObject.AddComponent<FishingPerformanceVoyage>().Begin(c,capture);
        }

        public void Begin(FishingPrototype c)
        {
            if(c==null || _samples!=null)throw new InvalidOperationException("Performance capture needs one initialized controller and cannot be restarted.");
            _c=c;RenderPipelineManager.endCameraRendering+=WorldCameraRendered;_samples=new Sample[Capacity];_started=_previous=Time.realtimeSinceStartupAsDouble;
            _timingsEnabled=FrameTimingManager.IsFeatureEnabled();
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            _metadata="UTC start: "+DateTime.UtcNow.ToString("O",Invariant)+"\nGame: "+FishingPrototype.Version
                +"\nUnity: "+Application.unityVersion+"\nPlatform: "+Application.platform+"; editor="+Application.isEditor
                +"\nGPU: "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType
                +"\nScreen: "+Screen.width+"x"+Screen.height+"; targetFrameRate="+Application.targetFrameRate+"; vSync="+QualitySettings.vSyncCount
                +"\nInitial quality: "+c.GraphicsQuality+"; renderScale="+(pipeline!=null?pipeline.renderScale.ToString(Invariant):"NA")
                +"; shadowDistance="+(pipeline!=null?pipeline.shadowDistance.ToString(Invariant):"NA")
                +"\nRuntime to capture start seconds: "+Time.realtimeSinceStartupAsDouble.ToString("F4",Invariant)
                +"\nFrameTimingStats enabled: "+_timingsEnabled
                +"\nPositions/state identify the collection frame. Driver timings can lag it; timing_timestamp identifies that frame."
                +"\nRepeated/unavailable CPU/GPU timings are NA, never zero. Memory is refreshed once per second."
                +"\nCPU timing includes present/wait; main-thread and render-thread columns are also recorded."
                +"\nAllocated bytes cover the collecting main thread, not all worker threads. Native/graphics memory are Unity profiler counters."
                +"\nCapture does not drive the boat, change quality, or certify headset performance. Compare the same route/time/weather/settings.\n";
            Debug.Log("[PERFORMANCE] Explicit capture started: 600 seconds or 131072 frames; writes once on completion/exit. Frame timings enabled="+_timingsEnabled);
            _gc0=GC.CollectionCount(0);_gc1=GC.CollectionCount(1);_gc2=GC.CollectionCount(2);
            _allocated=GC.GetAllocatedBytesForCurrentThread();
        }

        private void LateUpdate()
        {
            if(_stopped || _samples==null)return;
            if(_c==null){StopAndWrite();return;}
            double now=Time.realtimeSinceStartupAsDouble;
            double cpu=double.NaN,main=double.NaN,render=double.NaN,gpu=double.NaN;ulong stamp=0;
            if(_timingsEnabled)
            {
                FrameTimingManager.CaptureFrameTimings();
                if(FrameTimingManager.GetLatestTimings(1,_timing)>0 && _timing[0].frameStartTimestamp!=0 && _timing[0].frameStartTimestamp!=_lastTimingStamp)
                {
                    var timing=_timing[0];stamp=_lastTimingStamp=timing.frameStartTimestamp;
                    cpu=ValidTiming(timing.cpuFrameTime);main=ValidTiming(timing.cpuMainThreadFrameTime);
                    render=ValidTiming(timing.cpuRenderThreadFrameTime);gpu=ValidTiming(timing.gpuFrameTime);
                }
            }
            if(now>=_nextMemory)
            {
                _managed=GC.GetTotalMemory(false);_native=Profiler.GetTotalAllocatedMemoryLong();_graphics=Profiler.GetAllocatedMemoryForGraphicsDriver();
                _nextMemory=now+1;
            }
            int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            int state=(_c.Ashore?1:0)|(_c.RifleHunting!=null && _c.RifleHunting.Presented?2:0)|(_c.Paused?4:0)
                |(_c.StartMenuOpen?8:0)|(_c.Realms!=null && _c.Realms.Inside?16:0)|(_c.Anchored?32:0);
            _samples[_count++]=new Sample{Frame=Time.frameCount,CameraFrame=_lastWorldRenderFrame,Seconds=now-_started,Wall=(now-_previous)*1000,
                Cpu=cpu,Main=main,Render=render,Gpu=gpu,TimingStamp=stamp,Position=_c.ExplorerPosition,
                State=state,Segment=Segment,Quality=_c.GraphicsQuality,Target=_c.NavigationTarget,Daylight=_c.Weather!=null?_c.Weather.Daylight:float.NaN,
                Gc0=gc0-_gc0,Gc1=gc1-_gc1,Gc2=gc2-_gc2,Allocated=Math.Max(0,allocated-_allocated),Managed=_managed,Native=_native,Graphics=_graphics};
            _previous=now;_gc0=gc0;_gc1=gc1;_gc2=gc2;_allocated=allocated;
            if(_count==Capacity || now-_started>=Duration)StopAndWrite();
        }

        private static double ValidTiming(double value)=>value>0 && !double.IsInfinity(value)?value:double.NaN;
        private static string Number(double value)=>double.IsNaN(value)||double.IsInfinity(value)?"NA":value.ToString("F4",Invariant);
        private static string Memory(long value)=>value>0?value.ToString(Invariant):"NA";

        public void StopAndWrite()
        {
            if(_stopped || _samples==null)return;
            _stopped=true;
            try
            {
                string folder=Application.isEditor?Path.GetFullPath("Logs/Performance"):Path.Combine(Application.persistentDataPath,"Performance");
                Directory.CreateDirectory(folder);
                OutputPath=Path.Combine(folder,"voyage-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",Invariant)+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".csv");
                using(var writer=new StreamWriter(OutputPath,false,new UTF8Encoding(false)))
                {
                    writer.WriteLine("frame,seconds,wall_ms,cpu_frame_ms,cpu_main_ms,cpu_render_ms,gpu_ms,timing_timestamp,x,y,z,state_flags,quality,navigation_target,daylight,main_thread_allocated_bytes,gc0,gc1,gc2,managed_heap_bytes,unity_allocated_bytes,graphics_driver_bytes,segment_id,world_camera_frame");
                    for(int i=0;i<_count;i++)
                    {
                        var s=_samples[i];
                        writer.WriteLine(string.Join(",",s.Frame.ToString(Invariant),Number(s.Seconds),Number(s.Wall),Number(s.Cpu),Number(s.Main),Number(s.Render),Number(s.Gpu),s.TimingStamp.ToString(Invariant),
                            Number(s.Position.x),Number(s.Position.y),Number(s.Position.z),s.State.ToString(Invariant),s.Quality.ToString(Invariant),s.Target.ToString(Invariant),Number(s.Daylight),
                            s.Allocated.ToString(Invariant),s.Gc0.ToString(Invariant),s.Gc1.ToString(Invariant),s.Gc2.ToString(Invariant),Memory(s.Managed),Memory(s.Native),Memory(s.Graphics),s.Segment.ToString(Invariant),s.CameraFrame.ToString(Invariant)));
                    }
                }
                var report=new StringBuilder(_metadata).Append("State bits: 1 ashore, 2 rifle visible, 4 paused, 8 start menu, 16 realm, 32 anchored.\nCollected frames: ").Append(_count).Append('\n');
                var values=new double[_count];
                for(int i=0;i<_count;i++)values[i]=_samples[i].Wall;report.AppendLine(Summarize("Wall frame ms",values,_count));
                for(int i=0;i<_count;i++)values[i]=_samples[i].Cpu;report.AppendLine(Summarize("CPU frame ms",values,_count));
                for(int i=0;i<_count;i++)values[i]=_samples[i].Gpu;report.AppendLine(Summarize("GPU frame ms",values,_count));
                File.WriteAllText(Path.ChangeExtension(OutputPath,".txt"),report.ToString(),new UTF8Encoding(false));
                Debug.Log("[PERFORMANCE] Wrote "+_count+" frames: "+OutputPath);
            }
            catch(Exception ex){Debug.LogError("[PERFORMANCE] Capture could not be saved: "+ex);}
            finally{RenderPipelineManager.endCameraRendering-=WorldCameraRendered;enabled=false;}
        }

        private static string Summarize(string label,double[] values,int count)
        {
            int valid=0,over33=0,over50=0,over100=0;
            for(int i=0;i<count;i++)
            {
                double value=values[i];if(double.IsNaN(value)||double.IsInfinity(value)||value<=0)continue;
                values[valid++]=value;if(value>33)over33++;if(value>50)over50++;if(value>100)over100++;
            }
            if(valid==0)return label+": NA (no valid timings)";
            Array.Sort(values,0,valid);
            return label+": valid="+valid+"; P50="+Number(values[Math.Max(0,(int)Math.Ceiling(valid*.50)-1)])
                +"; P95="+Number(values[Math.Max(0,(int)Math.Ceiling(valid*.95)-1)])+"; P99="+Number(values[Math.Max(0,(int)Math.Ceiling(valid*.99)-1)])
                +"; >33="+over33+"; >50="+over50+"; >100="+over100;
        }

        private void WorldCameraRendered(ScriptableRenderContext context,Camera camera)
        {if(_c!=null && camera==_c.viewCamera)_lastWorldRenderFrame=Time.frameCount;}

        private void OnDisable()=>StopAndWrite();
        private void OnApplicationQuit()=>StopAndWrite();
    }
}
