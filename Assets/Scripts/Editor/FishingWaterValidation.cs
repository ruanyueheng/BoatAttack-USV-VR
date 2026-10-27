using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using WaterSystem;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingWaterValidation
    {
        private const BindingFlags StaticFields=BindingFlags.Static|BindingFlags.NonPublic;
        private sealed class Registration { public int Id;public int Start;public float3[] Points; }
        public static void Run()
        {
            // One-shot GPU depth assertions need compiled passes, not the editor's temporary async fallback.
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            FishingV011Validation.Begin("WATER");
        }
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException("[WATER] "+label);Debug.Log("[WATER] CHECK: "+label);}
        private static NativeArray<float3> Input=>(NativeArray<float3>)typeof(GerstnerWavesJobs).GetField("_positions",StaticFields).GetValue(null);
        private static Dictionary<int,int2> Registry=>(Dictionary<int,int2>)typeof(GerstnerWavesJobs).GetField("Registry",StaticFields).GetValue(null);

        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c!=null && c.Automated && c.HasIsolatedProfile,"Production water is checked only in an isolated acceptance career");
            // Batch editor updates do not repaint Game View; render the real camera to exercise its completion signal.
            c.viewCamera.Render();
            float deadline=Time.realtimeSinceStartup+15;
            while(!c.CoastalDepthReady && Time.realtimeSinceStartup<deadline)yield return null;
            Check(c.CoastalDepthReady,"The first completed world render precedes the one-shot shoreline capture");
            var water=Water.Instance;Check(water!=null && water.enabled && GerstnerWavesJobs.Initialized,"The real active water and CPU waves are initialized");
            var map=Resources.Load<FishingMapDefinition>("FishingMap");var area=Shader.GetGlobalVector("_WaterDepthBounds");
            var texture=Shader.GetGlobalTexture("_WaterDepthMap") as RenderTexture;
            Check(map!=null && map.Bounds.Valid && texture!=null && texture.IsCreated(),"The production shader uses a created world-depth render texture");
            Check(Mathf.Abs(area.x-((float)map.Bounds.MinX-32))<.01f && Mathf.Abs(area.y-((float)map.Bounds.MinZ-32))<.01f
                && Mathf.Abs(1/area.z-((float)map.Bounds.Width+64))<.02f && Mathf.Abs(1/area.w-((float)map.Bounds.Height+64))<.02f,
                "CPU capture and shader UVs share the measured world coverage plus a 32 m guard band");
            var camera=(Camera)typeof(Water).GetField("_depthCam",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(water);
            Check(camera!=null && !camera.enabled && camera.targetTexture==null && camera.cullingMask==(1<<10)
                && Mathf.Abs(camera.orthographicSize*2-1/area.w)<.02f && Mathf.Abs(camera.aspect-area.w/area.z)<.001f,
                "The one-shot orthographic capture covers the same rectangle, samples only terrain, and detaches afterwards");
            Check(texture.width<=4096 && texture.height<=4096 && 4L*texture.width*texture.height<=(Application.platform==RuntimePlatform.Android?48L:64L)*1024*1024,
                "Depth allocation respects its per-platform 48/64 MiB and 4096-edge caps");
            Check(1/area.z/texture.width<=.489f && 1/area.w/texture.height<=.489f,
                "This measured world retains at least the original 1024/500 m shoreline sampling density");
            Vector4 authored=water.settingsData.originOffset;
            bool rejected=false;try{water.CaptureDepthMap(new Rect(float.NaN,0,1,1));}catch(ArgumentException){rejected=true;}
            Check(rejected && water.settingsData.originOffset==authored,"Invalid capture bounds are rejected without modifying shared settings");
            var grounds=new List<MeshCollider>();
            for(int i=0;i<4;i++)grounds.Add(c.Islands.WorldRoot.GetChild(i).Find("Walkable beach and interior").GetComponent<MeshCollider>());
            for(int i=0;i<2;i++)grounds.Add(c.transform.Find(FishingCoastalPresentation.IsletName(i)+"/Walkable sculpted shoreline").GetComponent<MeshCollider>());
            grounds.Add(c.Home.transform.Find("V035 Home HomeLand").GetComponentInChildren<MeshCollider>());
            foreach(var ground in grounds)
                Check(ground!=null && ground.gameObject.layer==10 && ground.GetComponent<Renderer>()!=null,
                    "Real island/home shoreline is visible to the depth capture: "+ground.name);
            ValidateDepthShader(water,area,grounds);
            ValidateSummary();
            var lifetime=ValidateWaveLifetime(water);try{while(lifetime.MoveNext())yield return lifetime.Current;}finally{(lifetime as IDisposable)?.Dispose();}
            var capture=c.GetComponent<FishingPerformanceCapture>();bool ownsCapture=capture==null;
            if(ownsCapture){capture=c.gameObject.AddComponent<FishingPerformanceCapture>();capture.Begin(c);}
            try
            {
                for(int i=0;i<3;i++)yield return null;
                if(ownsCapture)
                {
                    capture.StopAndWrite();
                    Check(!string.IsNullOrEmpty(capture.OutputPath) && File.Exists(capture.OutputPath)
                        && File.ReadAllLines(capture.OutputPath).Length>=2 && File.ReadAllText(Path.ChangeExtension(capture.OutputPath,".txt")).Contains("Collected frames:"),
                        "Explicit performance sampling writes real frame rows and one summary without changing quality");
                }
            }
            finally{if(ownsCapture && capture!=null)Object.DestroyImmediate(capture);}
            Check(GerstnerWavesJobs.Initialized && Input.IsCreated,"Real boat and other samplers continue for three frames after water reinitialization");
        }

        private static void ValidateDepthShader(Water water,Vector4 bounds,List<MeshCollider> grounds)
        {
            var shader=Shader.Find("Hidden/WaterSystem/DepthProbe");
            Check(shader!=null && shader.isSupported && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat),"A float GPU probe can execute the production WaterTextureDepth function");
            var material=new Material(shader);var readback=new Texture2D(1,1,TextureFormat.RFloat,false,true);
            var target=RenderTexture.GetTemporary(1,1,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear);
            var active=RenderTexture.active;
            try
            {
                float Probe(Vector3 p)
                {
                    material.SetVector("_ProbePosition",p);Graphics.Blit(null,target,material);
                    RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,1,1),0,0,false);readback.Apply(false,false);
                    return readback.GetPixel(0,0).r;
                }
                float width=1/bounds.z,height=1/bounds.w;
                var outside=new[]{new Vector3(bounds.x-2,0,bounds.y+height*.5f),new Vector3(bounds.x+width+2,0,bounds.y+height*.5f),
                    new Vector3(bounds.x+width*.5f,0,bounds.y-2),new Vector3(bounds.x+width*.5f,0,bounds.y+height+2)};
                foreach(var p in outside)Check(Mathf.Abs(Probe(p)-water.surfaceData._waterMaxVisibility)<.01f,"Out-of-coverage GPU depth is deep water instead of clamped edge foam: "+p);
                foreach(var ground in grounds)
                {
                    Check(ShallowPoint(ground,water.transform.position.y,out var p),"A real submerged shoreline triangle exists: "+ground.name);
                    float depth=Probe(p),expected=-p.y;
                    if(float.IsNaN(depth) || depth>=2.6f || Mathf.Abs(depth-expected)>=1.25f)
                    {
                        var depthCamera=(Camera)typeof(Water).GetField("_depthCam",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(water);
                        var viewport=depthCamera.WorldToViewportPoint(p);
                        Debug.Log("[WATER] DEPTH DIAGNOSTIC: initial="+depth
                            +" renderer="+ground.GetComponent<Renderer>().bounds+" depthPass="+ground.GetComponent<Renderer>().sharedMaterial.FindPass("DepthOnly")
                            +" viewport="+viewport+" uv="+new Vector2((p.x-bounds.x)*bounds.z,(p.z-bounds.y)*bounds.w)
                            +" flippedZ="+Probe(new Vector3(p.x,p.y,bounds.y+height-(p.z-bounds.y))));
                        foreach(var hit in Physics.RaycastAll(new Ray(new Vector3(p.x,20,p.z),Vector3.down),100,1<<10,QueryTriggerInteraction.Ignore))
                            Debug.Log("[WATER] DEPTH HIT: "+hit.collider.name+" at "+hit.point+" layer="+hit.collider.gameObject.layer);
                        var renderer=ground.GetComponent<Renderer>();var original=renderer.sharedMaterial;
                        Debug.Log("[WATER] RENDERER: enabled="+renderer.enabled+" forced="+renderer.forceRenderingOff+" active="+renderer.gameObject.activeInHierarchy
                            +" shader="+original.shader.name+" supported="+original.shader.isSupported+" queue="+original.renderQueue+" cull="+original.GetFloat("_Cull")+" invert="+GL.invertCulling);
                    }
                    Check(!float.IsNaN(depth) && !float.IsInfinity(depth) && depth>0 && depth<2.6f && Mathf.Abs(depth-expected)<1.25f,
                        "GPU capture retains actual shallow coast depth: "+ground.name+" point="+p+" depth="+depth+" expected="+expected);
                }
            }
            finally{RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(material);Object.DestroyImmediate(readback);}
        }

        private static bool ShallowPoint(MeshCollider ground,float water,out Vector3 point)
        {
            var mesh=ground.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
            float best=float.MaxValue;point=default;
            for(int i=0;i<indices.Length;i+=3)
            {
                var p=ground.transform.TransformPoint((vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3);
                if(p.y>=water-.15f || p.y<=water-1.3f || !ground.Raycast(new Ray(p+Vector3.up*20,Vector3.down),out var hit,40))continue;
                float score=Mathf.Abs(hit.point.y-(water-.7f));
                if(score>=best || hit.point.y>=water-.15f || hit.point.y<=water-1.3f)continue;
                best=score;point=hit.point;
            }
            return best<float.MaxValue;
        }

        private static void ValidateSummary()
        {
            var method=typeof(FishingPerformanceCapture).GetMethod("Summarize",BindingFlags.Static|BindingFlags.NonPublic);
            string result=(string)method.Invoke(null,new object[]{"fixture",new[]{double.NaN,0d,4d,2d,120d,60d,40d},7});
            Check(result.Contains("valid=5") && result.Contains("P50=40.0000") && result.Contains("P95=120.0000") && result.Contains("P99=120.0000")
                && result.Contains(">33=3") && result.Contains(">50=2") && result.Contains(">100=1"),"Performance percentiles and spike counts exclude unavailable/zero timings");
            string missing=(string)method.Invoke(null,new object[]{"fixture",new[]{double.NaN,0d,double.PositiveInfinity},3});
            Check(missing.Contains("NA (no valid timings)"),"Unsupported timing data is reported as NA rather than a fake zero-millisecond result");
        }

        private static IEnumerator ValidateWaveLifetime(Water water)
        {
            var originals=new List<Registration>();var input=Input;
            foreach(var pair in Registry)
            {
                var points=new float3[pair.Value.y-pair.Value.x];for(int i=0;i<points.Length;i++)points[i]=input[pair.Value.x+i];
                originals.Add(new Registration{Id=pair.Key,Start=pair.Value.x,Points=points});
            }
            originals.Sort((a,b)=>a.Start.CompareTo(b.Start));
            var sample=new NativeArray<float3>(2,Allocator.Persistent);sample[0]=new float3(21,0,38);sample[1]=new float3(-18,0,43);
            int id=int.MinValue+900000;while(Registry.ContainsKey(id))id++;
            bool restored=false;
            try
            {
                GerstnerWavesJobs.UpdateHeights();water.enabled=false;
                GerstnerWavesJobs.Cleanup();GerstnerWavesJobs.Cleanup();GerstnerWavesJobs.UpdateHeights();
                GerstnerWavesJobs.UpdateSamplePoints(ref sample,id);
                var sentinel=new[]{new float3(123,456,789)};var normal=new[]{new float3(9,8,7)};
                GerstnerWavesJobs.GetData(id,ref sentinel,ref normal);
                Check(!GerstnerWavesJobs.Initialized && !Input.IsCreated && Registry.Count==0 && math.all(sentinel[0]==new float3(123,456,789)),
                    "Disabling real water completes the pending job; repeated cleanup and late sampler calls cannot access freed arrays");
                water.enabled=true;var created=Input;GerstnerWavesJobs.Init();
                Check(GerstnerWavesJobs.Initialized && created.Equals(Input),"Re-enabling water initializes once and duplicate Init retains its live buffers");
                for(int frame=0;frame<12;frame++)yield return null;
                foreach(var original in originals)
                    Check(Registry.TryGetValue(original.Id,out var range) && range.y-range.x==original.Points.Length,
                        "Existing sampler naturally registers again after restart: "+original.Id);
                restored=true;
                GerstnerWavesJobs.UpdateSamplePoints(ref sample,id);
                var registered=Registry[id];
                Check(math.all(Input[registered.x]==sample[0]) && math.all(Input[registered.x+1]==sample[1]),
                    "A single first registration immediately copies both real non-origin coordinates");
                var initialPosition=new float3[2];var initialNormal=new float3[2];GerstnerWavesJobs.GetData(id,ref initialPosition,ref initialNormal);
                Check(math.all(initialNormal[0]==new float3(0,1,0)),"A new sampler has a safe up normal until its first wave job finishes");
                for(int frame=0;frame<3;frame++)yield return null;
                GerstnerWavesJobs.UpdateSamplePoints(ref sample,id);
                var heights=new float3[2];var normals=new float3[2];GerstnerWavesJobs.GetData(id,ref heights,ref normals);
                Check(math.all(math.isfinite(heights[0])) && math.all(math.isfinite(heights[1])) && math.all(math.isfinite(normals[0])) && math.lengthsq(normals[0])>.1f,
                    "The restarted production height job returns finite positions and a real normal");
            }
            finally
            {
                if(!water.enabled)water.enabled=true;
                if(!restored){GerstnerWavesJobs.Cleanup();GerstnerWavesJobs.Init();RestoreRegistrations(originals);}
                GerstnerWavesJobs.RemoveSamplePoints(id);if(sample.IsCreated)sample.Dispose();
            }
        }

        private static void RestoreRegistrations(List<Registration> registrations)
        {
            foreach(var registration in registrations)
            {
                var points=new NativeArray<float3>(registration.Points,Allocator.TempJob);
                try{GerstnerWavesJobs.UpdateSamplePoints(ref points,registration.Id);}
                finally{points.Dispose();}
            }
        }
    }
}
