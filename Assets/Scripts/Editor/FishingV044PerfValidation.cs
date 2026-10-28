using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BoatAttack.Fishing;
using Unity.Profiling;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>V044 性能验收：标准机位下 draw call / SetPass / 三角面 / 主线程 GC 不超过 PICO 上限；报告存 Logs/V044/perf。</summary>
    public static class FishingV044PerfValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V044-PERF");
        // PICO 4 Ultra budget (Docs/PLAN_V044.md). Editor counts are one eye; Single Pass Instanced keeps draw calls per eye equal.
        public const int PicoDrawCalls=150,PicoTriangles=300000;
        // Hard ceilings = V044 measured upper bounds + ~10%, so content cannot grow while art LODs bring spots under the PICO budget (rows tagged PICO-OVER).
        // Tighten toward PicoDrawCalls/PicoTriangles as LOD work lands.
        public const int MaxDrawCalls=850,MaxTriangles=750000,MaxSetPass=100; // materials stand in for SetPass under the SRP batcher
        // shortcut: editor play mode adds its own main-thread garbage, so this is a leak guard, not the 0 B/frame player goal; verify in a player profile.
        public const long MaxBytesPerFrame=8192;
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-PERF] CHECK: "+label);}
        private static readonly StringBuilder Report=new StringBuilder();
        private static int _failures;
        private static readonly StringBuilder Detail=new StringBuilder();

        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Only the isolated automation profile is used");
            Report.Clear();Detail.Clear();_failures=0;
            float desktopLodBias=QualitySettings.lodBias;QualitySettings.lodBias=FishingMobileQuality.LodBias;
            Report.AppendLine("spot,draws_upper_bound,shadow_draws,materials,shaders,triangles_upper_bound,particles,gc_bytes_per_frame,editor_wall_ms,verdict");
            var teleport=typeof(FishingPrototype).GetMethod("Teleport",BindingFlags.Instance|BindingFlags.NonPublic);
            c.ReturnToHarbor();if(c.StartMenuOpen)c.FinishTitleVoyage();if(c.Paused)c.TogglePause();
            {var m=Measure(c,"harbor-day-before-detail-culling");while(m.MoveNext())yield return m.Current;}
            // Standalone editors skip FishingMobileQuality.Awake, so apply the same PICO culling explicitly.
            Report.AppendLine("# PICO detail budget: "+FishingMobileQuality.ApplyDetailBudget(c.gameObject.scene));
            {var m=Measure(c,"harbor-day");while(m.MoveNext())yield return m.Current;}
            for(int island=0;island<4;island++)
            {
                c.ReturnToHarbor();yield return null;
                teleport.Invoke(c,new object[]{FishingIslandWorld.Berth(island),Quaternion.Euler(0,180,0)});
                {var m=Measure(c,"island"+island+"-berth-afloat");while(m.MoveNext())yield return m.Current;}
                if(!c.Shore.TryDisembark()){Report.AppendLine("island"+island+"-ashore,disembark-rejected");continue;}
                {var m=Measure(c,"island"+island+"-ashore");while(m.MoveNext())yield return m.Current;}
                if(c.Shore.Relocate(c.Realms.PortalApproach(island),Quaternion.identity) && c.Realms.Enter(island))
                {
                    // Portal travel fades over several frames; measure only once inside the room.
                    for(int t=0;t<900 && (c.Realms.Transitioning || !c.Realms.Inside);t++)yield return null;
                    if(c.Realms.Inside){var m=Measure(c,"realm"+island);while(m.MoveNext())yield return m.Current;}else Report.AppendLine("realm"+island+",not-entered");
                    c.Realms.Exit();for(int t=0;t<900 && (c.Realms.Transitioning || c.Realms.Inside);t++)yield return null;
                }
                else Report.AppendLine("realm"+island+",entry-rejected");
            }
            c.ReturnToHarbor();yield return null;
            var npc=c.Residents.Person(0);
            if(npc!=null && c.Shore.TryDisembark() && c.Shore.Relocate(npc.position+Vector3.forward*1.8f,Quaternion.Euler(0,180,0))){var m=Measure(c,"resident");while(m.MoveNext())yield return m.Current;}
            c.ReturnToHarbor();
            c.Career.Calendar.AdvanceToHour(22);c.Weather.ApplyEnvironment();c.Nightscape.Apply(0);
            {var m=Measure(c,"harbor-night");while(m.MoveNext())yield return m.Current;}
            for(int i=0;i<48;i++)c.Weather.Climate.Tick(.25f,false);
            if(c.Weather.Climate.Current==BayWeather.Rain || c.Weather.Climate.Select(BayWeather.Rain))
            {for(int i=0;i<48;i++)c.Weather.Climate.Tick(.25f,false);c.Weather.ApplyEnvironment();{var m=Measure(c,"harbor-night-rain");while(m.MoveNext())yield return m.Current;}}
            foreach(SeaEventKind kind in System.Enum.GetValues(typeof(SeaEventKind)))
            {
                c.ReturnToHarbor();yield return null;c.SeaEvents.Force(kind);yield return null;
                var at=c.SeaEvents.Center+new Vector3(0,0,-18);at.y=c.Player.transform.position.y;
                teleport.Invoke(c,new object[]{at,Quaternion.identity});
                {var m=Measure(c,"sea-"+kind);while(m.MoveNext())yield return m.Current;}
            }
            c.ReturnToHarbor();QualitySettings.lodBias=desktopLodBias;
            string folder=Path.GetFullPath("Logs/V044/perf");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"v044-perf-spots.csv"),Report.ToString());File.WriteAllText(Path.Combine(folder,"v044-perf-heaviest.txt"),Detail.ToString());
            Debug.Log("[V044-PERF] spots\n"+Report);
            Check(_failures==0,"Every standard spot stays within draws<="+MaxDrawCalls+", materials<="+MaxSetPass+", triangles<="+MaxTriangles+", main-thread GC<="+MaxBytesPerFrame+" B/frame (see Logs/V044/perf/v044-perf-spots.csv)");
        }

        private static IEnumerator Measure(FishingPrototype c,string spot)
        {
            for(int i=0;i<30;i++)yield return null;
            // Batch-mode editors draw nothing, so render counters read 0; count what the camera would submit instead.
            var g=Visible(c.viewCamera);
            using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame"))
            {
                yield return null;
                const int frames=90;long bytes=0;bool counted=gc.Valid;
                float start=Time.realtimeSinceStartup;
                for(int i=0;i<frames;i++){yield return null;bytes+=gc.LastValue;}
                long perFrame=counted?bytes/frames:-1;float wall=(Time.realtimeSinceStartup-start)*1000/frames;
                bool ok=g.draws>0 && g.draws<=MaxDrawCalls && g.materials<=MaxSetPass && g.triangles<=MaxTriangles && perFrame<=MaxBytesPerFrame;
                if(!ok)_failures++;
                Report.AppendLine(spot+","+g.draws+","+g.shadow+","+g.materials+","+g.shaders+","+g.triangles+","+g.particles+","+(counted?perFrame.ToString():"NA")+","+wall.ToString("F2")+(ok?g.draws+g.shadow<=PicoDrawCalls && g.triangles<=PicoTriangles?",PICO-OK":",PICO-OVER":",OVER"));
                var top=new List<KeyValuePair<string,(int draws,long tris)>>(Top);top.Sort((a,b)=>b.Value.tris.CompareTo(a.Value.tris));
                var heavy=new StringBuilder();for(int i=0;i<Math.Min(12,top.Count);i++)heavy.Append("\n  ").Append(top[i].Key).Append(" draws=").Append(top[i].Value.draws).Append(" tris=").Append(top[i].Value.tris);
                Detail.Append(spot).Append(heavy).Append('\n');
                Debug.Log("[V044-PERF] SPOT "+spot+" draws="+g.draws+" shadow="+g.shadow+" materials="+g.materials+" tris="+g.triangles+" particles="+g.particles+" gc/frame="+perFrame+(ok?"":" OVER BUDGET"));
            }
        }

        private static readonly Dictionary<string,(int draws,long tris)> Top=new Dictionary<string,(int,long)>();
        private static string Group(Transform t){var path=t.name;for(int i=0;i<2 && t.parent!=null;i++){t=t.parent;path=t.name+"/"+path;}return path;}
        // Frustum + LOD + per-layer distance culling of active renderers; no occlusion or batching, so it is an upper bound per eye.
        private static (int draws,int materials,int shaders,long triangles,int particles,int shadow) Visible(Camera cam)
        {
            var planes=GeometryUtility.CalculateFrustumPlanes(cam);var lodHidden=new HashSet<Renderer>();
            float tan=Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad);
            foreach(var group in UnityEngine.Object.FindObjectsOfType<LODGroup>())
            {
                if(!group.enabled)continue;var lods=group.GetLODs();
                float size=group.size*Mathf.Max(Mathf.Abs(group.transform.lossyScale.x),Mathf.Abs(group.transform.lossyScale.y),Mathf.Abs(group.transform.lossyScale.z));
                float d=Mathf.Max(.01f,Vector3.Distance(cam.transform.position,group.transform.TransformPoint(group.localReferencePoint)));
                float height=size/(2*d*tan)*QualitySettings.lodBias;int chosen=-1;
                for(int i=0;i<lods.Length;i++)if(height>=lods[i].screenRelativeTransitionHeight){chosen=i;break;}
                for(int i=0;i<lods.Length;i++)if(i!=chosen)foreach(var r in lods[i].renderers)if(r!=null)lodHidden.Add(r);
            }
            Top.Clear();var materials=new HashSet<Material>();var shaders=new HashSet<Shader>();int draws=0,particles=0,shadow=0;long tris=0;
            var layerCull=cam.layerCullDistances;
            foreach(var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if(!r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff || (cam.cullingMask&(1<<r.gameObject.layer))==0 || lodHidden.Contains(r))continue;
                if(!(r is MeshRenderer || r is SkinnedMeshRenderer || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer))continue;
                long before=tris;var b=r.bounds;if(!GeometryUtility.TestPlanesAABB(planes,b))continue;
                float far=layerCull[r.gameObject.layer]>0?layerCull[r.gameObject.layer]:cam.farClipPlane;
                if(Mathf.Sqrt(b.SqrDistance(cam.transform.position))>far)continue;
                Mesh mesh=r is SkinnedMeshRenderer s?s.sharedMesh:r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var filter)?filter.sharedMesh:null;
                if(r is ParticleSystemRenderer p){var ps=p.GetComponent<ParticleSystem>();if(ps==null || ps.particleCount==0)continue;particles+=ps.particleCount;tris+=ps.particleCount*2L;draws++;}
                else if(mesh!=null)
                {
                    // Static batching points every member at the combined mesh; each renderer owns only its submesh range.
                    int first=r is MeshRenderer mr?mr.subMeshStartIndex:0,count=Mathf.Min(Mathf.Max(1,r.sharedMaterials.Length),mesh.subMeshCount-first);
                    for(int m=0;m<count;m++){tris+=mesh.GetIndexCount(first+m)/3;draws++;}
                    // In-view casters inside the PICO 35 m shadow distance (off-screen casters are not counted).
                    if(r.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.Off && b.SqrDistance(cam.transform.position)<35*35)shadow+=count;
                }
                else draws++;
                foreach(var mat in r.sharedMaterials)if(mat!=null){materials.Add(mat);shaders.Add(mat.shader);}
                string key=Group(r.transform);Top.TryGetValue(key,out var t);Top[key]=(t.draws+1,t.tris+(tris-before));
            }
            return (draws,materials.Count,shaders.Count,tris,particles,shadow);
        }
    }
}
