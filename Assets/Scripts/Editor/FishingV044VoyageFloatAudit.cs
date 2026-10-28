using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>Sails home → four islands → home along planned water routes, captures the player view (ahead, ±60°)
    /// every ~3 s and fails on any in-frustum renderer hanging high above the sea with nothing beneath it.</summary>
    public static class FishingV044VoyageFloatAudit
    {
        public static void Run()=>FishingV011Validation.Begin("V044-VOYAGE");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-VOYAGE] CHECK: "+label);}
        private const float Height=2.5f,Gap=4f,StepMetres=1.2f;private const int StepsPerSample=30;
        // Legitimately airborne: birds, particles, lighthouse beams, sky.
        private static readonly string[] Allowed={"Gull","鸥","Bird","Beam","Sky","Sun","Moon","Star"};
        // Racing-level props that must stay hidden in the fishing world even when they rest on the water.
        private static readonly string[] DebugNames={"Trigger","Debug","Gizmo","Placeholder"};
        private static readonly string[] Leftovers={"Props_ArrowSign_Dynamic","Windsurfer","Props_Paraglider","Geo_Props_Blimp"};

        public static IEnumerator Checks(FishingPrototype c)
        {
            string root=Path.GetFullPath("Logs/V044/voyage");Directory.CreateDirectory(root);
            if(c.StartMenuOpen)c.FinishTitleVoyage();if(c.Paused)c.TogglePause();c.ReturnToHarbor();yield return null;
            var stops=new List<(string name,Vector3 berth)>{("home",c.Home.Berth)};
            for(int i=0;i<4;i++)stops.Add(("island"+i,FishingIslandWorld.Berth(i)));
            stops.Add(("home",c.Home.Berth));
            var rows=new List<string>{"route,sample,suspect,path,mesh,components,minY,gap,reason"};var bad=new HashSet<string>();
            Gaps.Clear();
            Check(!c.WaterRoute.ClearPoint(new Vector3(-260,0,-150)),"Original mainland interior cannot be mistaken for navigable water");
            Check(!c.WaterRoute.ClearSegment(new Vector3(-260,0,-270),new Vector3(-260,0,-140)),"A route cannot cross beneath the original terrain's cut edge");
            Check(Physics.Linecast(new Vector3(-260,1,-270),new Vector3(-260,1,-240),out var edgeHit,~(1<<4),QueryTriggerInteraction.Ignore)
                && edgeHit.collider.name=="Terrain cut-edge collision","Original terrain's open cut edge also blocks manual boats");
            foreach(var t in Terrain.activeTerrains)
            {
                var tc=t.GetComponent<TerrainCollider>();
                Debug.Log("[V044-VOYAGE] TERRAIN "+PathOf(t.transform)+" pos="+t.transform.position+" size="+t.terrainData.size+" layer="+LayerMask.LayerToName(t.gameObject.layer)+" collider="+(tc!=null && tc.enabled)+" data="+t.terrainData.name+" heightAt(-260,-150)="+t.SampleHeight(new Vector3(-260,0,-150)));
            }
            {
                var cam=c.viewCamera.transform;var pose=(cam.position,cam.rotation);
                // Fixed overview poses where floating sheets were reported by the lead.
                var island=FishingIslandWorld.Center(0)+new Vector3(45,24,75);
                foreach(var (label,position,look) in new[]{("home-sky-pose",c.Home.Origin+new Vector3(22,10,-48),new Vector3(-.2f,.18f,1)),("island0-far-pose",island,FishingIslandWorld.Center(0)-island),("phantom-above",new Vector3(-260,120,20),new Vector3(0,-1,-1.2f)),("phantom-sea",new Vector3(-200,3,-30),new Vector3(-.4f,0,-1))})
                {
                    cam.SetPositionAndRotation(position,Quaternion.LookRotation(look));
                    FishingV016Validation.Capture(c,root,label);
                    foreach(var row in Scan(c,label,bad))rows.Add(row);
                }
                cam.SetPositionAndRotation(pose.position,pose.rotation);
            }
            var routes=new List<(Vector3 from,Vector3 to,string name)>();
            for(int i=1;i<5;i++)routes.Add((c.Home.Berth,stops[i].berth,"home-"+stops[i].name));
            for(int i=1;i<5;i++)routes.Add((stops[i].berth,stops[i+1].berth,stops[i].name+"-"+stops[i+1].name));
            foreach(var (from,to,name) in routes)
            {
                string dir=Path.Combine(root,name);Directory.CreateDirectory(dir);
                var start=from==c.Home.Berth?from:from+Vector3.forward*10;
                var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=start+Vector3.up*.7f;c.Player.transform.position=rb.position;Physics.SyncTransforms();
                int destination=to==c.Home.Berth?c.HomeNavigationIndex:c.IslandNavigationIndex(stops.FindIndex(s=>s.berth==to)-1);
                c.SelectNavigation(destination);Check(c.WaterRoute.Plan(),"Water route planned for "+name+" / "+c.WaterRoute.Failure);
                var points=c.WaterRoute.Points.ToArray();float length=0;
                for(int i=1;i<points.Length;i++){Check(c.WaterRoute.ClearSegment(points[i-1],points[i]),"Terrain and hull clearance / "+name+" segment "+i);length+=Vector3.Distance(points[i-1],points[i]);}
                int sample=0,segment=1;float segmentStart=0;
                for(float d=0;d<=length;sample++)
                {
                    for(int s=0;s<StepsPerSample && d<=length;s++,d+=StepMetres)
                    {
                        while(segment<points.Length-1 && d>segmentStart+Vector3.Distance(points[segment-1],points[segment])){segmentStart+=Vector3.Distance(points[segment-1],points[segment]);segment++;}
                        var delta=points[segment]-points[segment-1];var heading=Quaternion.LookRotation(delta);
                        var p=Vector3.Lerp(points[segment-1],points[segment],(d-segmentStart)/delta.magnitude)+Vector3.up*.7f;
                        rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=p;rb.rotation=heading;c.Player.transform.SetPositionAndRotation(p,heading);
                        yield return null;
                    }
                    var cam=c.viewCamera.transform;var rotation=cam.rotation;
                    try
                    {
                        foreach(int yaw in new[]{0,-60,60})
                        {
                            cam.rotation=Quaternion.AngleAxis(yaw,Vector3.up)*rotation;
                            string label=sample.ToString("D3")+(yaw==0?"-ahead":yaw<0?"-left":"-right");
                            FishingV016Validation.Capture(c,dir,label);
                            foreach(var row in Scan(c,name+"/"+label,bad))rows.Add(row);
                        }
                    }
                    finally{cam.rotation=rotation;}
                }
            }
            File.WriteAllLines(Path.Combine(root,"suspects.csv"),rows);
            foreach(var path in bad)Debug.Log("[V044-VOYAGE] FLOATING: "+path);
            Check(bad.Count==0,"No unsupported floating renderer is visible along any voyage ("+bad.Count+" found, see "+Path.Combine(root,"suspects.csv")+")");
            c.ReturnToHarbor();
        }

        private static IEnumerable<string> Scan(FishingPrototype c,string sample,HashSet<string> bad)
        {
            var camera=c.viewCamera;var planes=GeometryUtility.CalculateFrustumPlanes(camera);
            var all=UnityEngine.Object.FindObjectsOfType<Renderer>();
            foreach(var r in all)
            {
                if(!r.enabled || !r.gameObject.activeInHierarchy || r.transform.IsChildOf(c.Player.transform) || r.transform.IsChildOf(c.Shore.Actor))continue;
                var b=r.bounds;if(b.size.magnitude>400 || !GeometryUtility.TestPlanesAABB(planes,b))continue;
                string path=PathOf(r.transform),mesh=r.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh!=null?filter.sharedMesh.name:"";
                var canvas=r.GetComponentInParent<Canvas>();
                string reason=canvas!=null && canvas.renderMode==RenderMode.WorldSpace?"world-canvas":mesh=="Quad"||mesh=="Plane"?"flat-mesh":"";
                float gap=-1;
                if(b.min.y>Height)
                {
                    if(!Gaps.TryGetValue(r,out gap))
                    {
                        gap=float.PositiveInfinity;
                        foreach(var hit in Physics.RaycastAll(new Vector3(b.center.x,b.min.y+.01f,b.center.z),Vector3.down,200,~0,QueryTriggerInteraction.Ignore))
                            if(!hit.transform.IsChildOf(r.transform) && hit.distance<gap)gap=hit.distance;
                        // Sea level is the floor when no collider sits under open water.
                        gap=Mathf.Min(gap,b.min.y);
                        // Many level meshes (cliff bushes, roofs, LOD trees) have no collider; another mesh just beneath counts as support.
                        var below=new Bounds(new Vector3(b.center.x,b.min.y-Gap*.5f,b.center.z),new Vector3(b.size.x*.5f,Gap,b.size.z*.5f));
                        if(gap>Gap && all.Any(o=>o!=r && o is MeshRenderer && o.enabled && o.gameObject.activeInHierarchy && !o.transform.IsChildOf(r.transform) && o.bounds.size.magnitude<400 && o.bounds.Intersects(below)))gap=0;
                        Gaps[r]=gap;
                    }
                    if(gap>Gap)reason+=(reason.Length>0?"+":"")+"floating";
                }
                if(DebugNames.Any(a=>r.name.IndexOf(a,StringComparison.OrdinalIgnoreCase)>=0))reason+=(reason.Length>0?"+":"")+"debug-name";
                var tint=r.sharedMaterial!=null && r.sharedMaterial.HasProperty("_BaseColor")?r.sharedMaterial.GetColor("_BaseColor"):r.sharedMaterial!=null && r.sharedMaterial.HasProperty("_Color")?r.sharedMaterial.color:Color.black;
                if(tint.g>.6f && tint.g>tint.r+.25f && tint.g>tint.b+.25f)reason+=(reason.Length>0?"+":"")+"bright-green";
                if(filter!=null && filter.sharedMesh!=null)
                {
                    // A runtime sheet: one local axis paper-thin, the other two over 5 m, standing upright.
                    var size=Vector3.Scale(filter.sharedMesh.bounds.size,r.transform.lossyScale);size=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
                    float thin=Mathf.Min(size.x,Mathf.Min(size.y,size.z)),wide=size.x+size.y+size.z-thin-Mathf.Max(size.x,Mathf.Max(size.y,size.z));
                    if(thin<.15f && wide>5 && b.size.y>5)reason+=(reason.Length>0?"+":"")+"giant-sheet";
                }
                if(Leftovers.Any(a=>path.Contains(a)))reason+=(reason.Length>0?"+":"")+"race-leftover";
                if(reason.Length==0)continue;
                bool suspect=reason.Contains("race-leftover") || reason.Contains("giant-sheet") || reason.Contains("debug-name") || reason.Contains("floating") && !(r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || Allowed.Any(a=>path.IndexOf(a,StringComparison.OrdinalIgnoreCase)>=0));
                if(suspect)bad.Add(path);
                string components=string.Join(" ",r.GetComponents<Component>().Select(x=>x.GetType().Name));
                yield return string.Join(",",sample,suspect?"1":"0",path.Replace(',',' '),mesh.Replace(',',' '),components,b.min.y.ToString("F1"),gap.ToString("F1"),reason);
            }
        }
        private static readonly Dictionary<Renderer,float> Gaps=new Dictionary<Renderer,float>();
        private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    }
}
