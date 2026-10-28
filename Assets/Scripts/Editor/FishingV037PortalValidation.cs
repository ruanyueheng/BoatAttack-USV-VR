using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV037PortalValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V037-PORTAL");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V037-PORTAL] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            var camera=c.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;float fov=c.viewCamera.fieldOfView;
            Directory.CreateDirectory("Docs/Images/V037");
            try
            {
                if(c.StartMenuOpen)c.FinishTitleVoyage();c.Weather.ApplyEnvironment();
                for(int id=0;id<4;id++)
                {
                    var portal=c.Realms.Portal(id);
                    var approach=portal.Find("Approach");
                    Check(approach!=null && approach.gameObject.activeInHierarchy,"Entrance retains its authored walkable approach "+id);
                    var entranceVisual=portal.GetComponentInChildren<FishingPortalVisual>();
                    if(id==0)Check(entranceVisual.Height<2.4f,"Compact windmill entrance keeps its crescent below the balcony");
                    foreach(var renderer in portal.GetComponentsInChildren<Renderer>(true))
                        Check(!renderer.enabled || renderer.transform.IsChildOf(entranceVisual.transform),"No legacy renderer is layered over the new entrance "+id);
                    Check(portal.GetComponentsInChildren<Collider>(true).Length==0,"Entrance keeps its nonblocking interaction marker "+id);
                    CaptureApproach(c,portal,approach.position,"portal-approach-"+id);
                    CapturePortal(c,portal,approach.position,"portal-"+id);
                }
                var exit=c.Realms.ExitPortal(0);var room=exit.parent;bool active=room.gameObject.activeSelf;
                try
                {
                    room.gameObject.SetActive(true);Physics.SyncTransforms();
                    CaptureApproach(c,exit,exit.position+exit.forward*1.8f,"portal-exit-approach-0");
                    CapturePortal(c,exit,exit.position+exit.forward*1.8f,"portal-exit-0");
                }
                finally{room.gameObject.SetActive(active);Physics.SyncTransforms();}
                // PICO-tier ocean review: same harbour-side framing with and without scene textures, day and night.
                {
                    var centre0=FishingIslandWorld.Center(0);var eye0=centre0+new Vector3(45,24,75);
                    foreach(float hour in new[]{11f,22f})
                    {
                        c.Career.Calendar.AdvanceToHour(hour);c.Weather.ApplyEnvironment();
                        foreach(bool pico in new[]{false,true})
                        {
                            if(pico)Shader.EnableKeyword("_WATER_NO_SCENE_TEXTURES");else Shader.DisableKeyword("_WATER_NO_SCENE_TEXTURES");
                            for(int f=0;f<3;f++)yield return null;
                            c.viewCamera.fieldOfView=60;c.viewCamera.transform.SetPositionAndRotation(eye0,Quaternion.LookRotation(centre0+Vector3.up*3-eye0));
                            FishingV016Validation.Capture(c,"Logs/V044/vfx","ocean-"+(hour<12?"day":"night")+(pico?"-pico":"-desktop"));
                        }
                    }
                    Shader.DisableKeyword("_WATER_NO_SCENE_TEXTURES");c.Career.Calendar.AdvanceToHour(11);c.Weather.ApplyEnvironment();
                }
                // Diagnostic for the striped sheets in island0-far: same framing with and without vfx-owned renderers.
                {
                    var centre0=FishingIslandWorld.Center(0);var eye0=centre0+new Vector3(45,24,75);
                    void Far(string label){c.viewCamera.fieldOfView=60;c.viewCamera.transform.SetPositionAndRotation(eye0,Quaternion.LookRotation(centre0+Vector3.up*3-eye0));FishingV016Validation.Capture(c,"Logs/V044/vfx",label);}
                    Far("diag-island0-far-same-frame");
                    for(int f=0;f<5;f++){c.viewCamera.transform.SetPositionAndRotation(eye0,Quaternion.LookRotation(centre0+Vector3.up*3-eye0));yield return null;}
                    Far("diag-island0-far-settled");
                    var hidden=new List<Renderer>();
                    foreach(var r in c.Effects.GetComponentsInChildren<Renderer>())if(r.enabled){r.enabled=false;hidden.Add(r);}
                    foreach(var v in UnityEngine.Object.FindObjectsOfType<FishingPortalVisual>())foreach(var r in v.GetComponentsInChildren<Renderer>())if(r.enabled){r.enabled=false;hidden.Add(r);}
                    Far("diag-island0-far-no-vfx");
                    foreach(var r in hidden)r.enabled=true;
                }
                // V044 themed portals: one identical framing per island for the comparison sheet.
                for(int id=0;id<4;id++)
                {
                    var door=c.Realms.Portal(id);var art=door.GetComponentInChildren<FishingPortalVisual>();
                    Check(art.Island==id,"Portal is themed for its own island "+id);
                    var facing=Vector3.ProjectOnPlane(door.Find("Approach").position-door.position,Vector3.up).normalized;
                    var centre=door.TransformPoint(Vector3.up*(art.Height*.5f));
                    c.viewCamera.fieldOfView=60;c.viewCamera.transform.SetPositionAndRotation(centre+facing*3.4f,Quaternion.LookRotation(-facing));
                    for(int f=0;f<3;f++)yield return null;
                    c.viewCamera.fieldOfView=60;c.viewCamera.transform.SetPositionAndRotation(centre+facing*3.4f,Quaternion.LookRotation(-facing));
                    FishingV016Validation.Capture(c,"Logs/V044/vfx","theme-"+id);
                }
                // V044 art review: pulse flash plus pickup sparkle and splash in front of a portal.
                Directory.CreateDirectory("Logs/V044/vfx");
                var review=c.Realms.Portal(1);var reviewEye=review.Find("Approach").position+Vector3.up*1.65f;
                CaptureApproach(c,review,review.Find("Approach").position,"portal-approach-1");
                review.GetComponentInChildren<FishingPortalVisual>().Pulse();
                var front=Vector3.Lerp(reviewEye,review.position,.55f);
                c.Effects.Burst(front,new Color(1,.79f,.37f));c.Effects.Burst(front+Vector3.right*.6f,new Color(.46f,.82f,.80f));
                for(int f=0;f<6;f++)yield return null;
                c.viewCamera.fieldOfView=70;c.viewCamera.transform.SetPositionAndRotation(reviewEye,Quaternion.LookRotation(front-reviewEye));
                FishingV016Validation.Capture(c,"Logs/V044/vfx","after-portal-pulse-burst");
                for(int f=0;f<90;f++)yield return null;
                c.Effects.Celebrate(front+Vector3.left*.5f);for(int f=0;f<10;f++)yield return null;
                c.viewCamera.fieldOfView=70;c.viewCamera.transform.SetPositionAndRotation(reviewEye,Quaternion.LookRotation(front-reviewEye));
                FishingV016Validation.Capture(c,"Logs/V044/vfx","after-portal-idle-dust-celebrate");
                // Watering review: damp soil spots on the ground in front of the door.
                if(c.Shore.Ground(front,out var soil))for(int f=0;f<6;f++)c.Effects.Wet(soil+new Vector3((f%3-1)*.35f,0,(f/3)*.4f-.2f));
                for(int f=0;f<5;f++)yield return null;
                c.viewCamera.fieldOfView=60;c.viewCamera.transform.SetPositionAndRotation(reviewEye,Quaternion.LookRotation(soil-reviewEye));
                FishingV016Validation.Capture(c,"Logs/V044/vfx","after-wet-soil");
                // Night review: fireflies drifting in front of the grove door.
                c.Career.Calendar.AdvanceToHour(22);c.Weather.ApplyEnvironment();
                for(int f=0;f<14;f++)c.Effects.Firefly(front+new Vector3(Mathf.Cos(f*2.4f)*(1+f%3*.5f),.2f+(f%5)*.3f,Mathf.Sin(f*2.4f)*.8f));
                for(int f=0;f<40;f++)yield return null;
                c.viewCamera.fieldOfView=70;c.viewCamera.transform.SetPositionAndRotation(reviewEye,Quaternion.LookRotation(front-reviewEye));
                FishingV016Validation.Capture(c,"Logs/V044/vfx","after-night-fireflies");
            }
            finally{camera.SetPositionAndRotation(position,rotation);c.viewCamera.fieldOfView=fov;}
            for(int id=0;id<4;id++)
            {
                Inspect(c.Realms.Portal(id));Inspect(c.Realms.ExitPortal(id));
                Check(Vector3.Distance(c.Realms.ExitPortal(id).position,c.Realms.ExitPortal(id).parent.position)>10,"Exit remains on terminal platform "+id);
            }
            bool reduced=c.Weather.ReducedEffects;
            var go=new GameObject("V037 temporary portal lifecycle fixture");
            var visual=go.AddComponent<FishingPortalVisual>();visual.Build(c,0,false);
            var frame=go.transform.Find("Carved brass arch").GetComponent<MeshFilter>().sharedMesh;
            var veil=go.transform.Find("Visible flowing light curtain").GetComponent<MeshRenderer>();
            var stars=go.transform.Find("Drifting gold stardust").GetComponent<MeshRenderer>();
            var meshes=new List<Mesh>();var materials=new List<Material>();
            foreach(var f in go.GetComponentsInChildren<MeshFilter>())meshes.Add(f.sharedMesh);
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>())materials.Add(r.sharedMaterial);
            try
            {
                if(!c.Weather.ReducedEffects)c.Weather.ToggleReducedEffects();yield return null;
                Check(veil.sharedMaterial.GetFloat("_Motion")==0 && stars.sharedMaterial.GetFloat("_Motion")==0,"Reduced effects freeze mist/stardust and reduce star density");
                var before=new MaterialPropertyBlock();veil.GetPropertyBlock(before);visual.Pulse();var after=new MaterialPropertyBlock();veil.GetPropertyBlock(after);
                Check(before.GetFloat("_PulseTime")==after.GetFloat("_PulseTime"),"Reduced effects suppress interaction flashes");
                c.Weather.ToggleReducedEffects();yield return null;
                Check(veil.sharedMaterial.GetFloat("_Motion")==1 && stars.sharedMaterial.GetFloat("_Motion")==1,"Full ambient motion restores without rebuilding meshes");
                visual.Pulse();veil.GetPropertyBlock(after);
                Check(Mathf.Abs(after.GetFloat("_PulseTime")-Time.time)<.05f,"Interaction uses the per-renderer soft pulse");
                Check(frame==go.transform.Find("Carved brass arch").GetComponent<MeshFilter>().sharedMesh,"Toggling effects retains batched geometry");
            }
            finally
            {
                if(c.Weather.ReducedEffects!=reduced)c.Weather.ToggleReducedEffects();
                UnityEngine.Object.Destroy(go);
            }
            yield return null;yield return null;
            foreach(var mesh in meshes)Check(mesh==null,"Portal destroys owned runtime mesh");
            foreach(var material in materials)Check(material==null,"Portal destroys owned runtime material");
        }
        private static MeshFilter[] VisibleStructure(FishingPrototype c)
        {
            var meshes=new List<MeshFilter>();
            var observatory=c.Islands.WorldRoot.GetChild(3).Find(FishingV035Art.ArtRoot+"/ObservatoryExterior");
            if(observatory!=null)meshes.AddRange(observatory.GetComponentsInChildren<MeshFilter>());
            var wind=c.Realms.ExitPortal(0).parent.Find(FishingRealmWorld.GeometryRootName+"/SanctuaryWind");
            if(wind!=null && wind.gameObject.activeInHierarchy)meshes.AddRange(wind.GetComponentsInChildren<MeshFilter>());
            return meshes.ToArray();
        }
        private static void CaptureApproach(FishingPrototype c,Transform portal,Vector3 approach,string label)
        {
            var eye=approach+Vector3.up*1.65f;
            var visual=portal.GetComponentInChildren<FishingPortalVisual>();
            var blockers=VisibleStructure(c);
            foreach(float x in new[]{-.66f,0,.66f})foreach(float y in new[]{.65f,1.4f,visual.Height-.12f})
                Check(ClearView(c,eye,portal.TransformPoint(new Vector3(x,y,0)),blockers),"Actual standing approach has a clear view of the doorway: "+label+" x="+x+" y="+y);
            var target=portal.TransformPoint(Vector3.up*(visual.Height*.53f+.10f));
            c.viewCamera.fieldOfView=100;
            c.viewCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            FishingV016Validation.Capture(c,"Docs/Images/V037",label);
        }
        private static void CapturePortal(FishingPrototype c,Transform portal,Vector3 approach,string label)
        {
            var visual=portal.GetComponentInChildren<FishingPortalVisual>(true);
            var facing=Vector3.ProjectOnPlane(approach-portal.position,Vector3.up).normalized;
            if(facing.sqrMagnitude<.5f)facing=portal.forward;
            var centre=portal.TransformPoint(Vector3.up*(visual.Height*.53f+.10f));
            var blockers=VisibleStructure(c);
            int blocked=0,cropped=0;
            foreach(float distance in new[]{3.3f,2.6f,4.2f})
            foreach(float angle in new[]{0f,-15f,15f,-40f,40f,180f,165f,195f})
            foreach(float height in new[]{1.8f,2.7f,3.5f})
            {
                // This is an isolated art-review camera, independent of the playable approach.
                var eye=portal.position+Quaternion.AngleAxis(angle,Vector3.up)*facing*distance+Vector3.up*height;
                if(!ClearView(c,eye,centre,blockers)){blocked++;continue;}
                bool clear=true;
                foreach(float x in new[]{-.66f,.66f})foreach(float y in new[]{.42f,visual.Height-.12f})
                    clear&=ClearView(c,eye,portal.TransformPoint(new Vector3(x,y,0)),blockers);
                foreach(float y in new[]{.65f,1.1f,visual.Height+.32f})clear&=ClearView(c,eye,portal.TransformPoint(Vector3.up*y),blockers);
                if(!clear){blocked++;continue;}
                var camera=c.viewCamera;camera.fieldOfView=distance<3?82:72;
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(centre-eye));
                bool framed=true;var bounds=visual.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                for(int corner=0;corner<8;corner++)
                {
                    var point=visual.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));
                    var v=camera.WorldToViewportPoint(point);framed&=v.z>.1f && v.x>.015f && v.x<.985f && v.y>.015f && v.y<.985f;
                }
                if(!framed){cropped++;continue;}
                Check(true,"Unobstructed art-review camera shows centre, corners and full carved frame: "+label+" distance="+distance+" height="+height+" angle="+angle);
                FishingV016Validation.Capture(c,"Docs/Images/V037",label);return;
            }
            throw new InvalidOperationException("No clear art-review view of portal: "+label+" blocked="+blocked+" cropped="+cropped);
        }
        private static bool ClearView(FishingPrototype c,Vector3 eye,Vector3 target,MeshFilter[] blockers)
        {
            var delta=target-eye;
            foreach(var hit in Physics.RaycastAll(eye,delta.normalized,delta.magnitude-.06f,~(1<<4),QueryTriggerInteraction.Ignore))
                if(hit.collider!=c.Shore.Body && !hit.transform.IsChildOf(c.Player.transform) && !FishingShore.NonSolidSurface(hit.collider))return false;
            // Decorative rings and beams may be absent from the simplified collision mesh.
            foreach(var filter in blockers)
            {
                var renderer=filter.GetComponent<Renderer>();
                if(renderer==null || !renderer.enabled || !renderer.bounds.IntersectRay(new Ray(eye,delta.normalized)))continue;
                var mesh=filter.sharedMesh;if(mesh==null)continue;
                // Without Read/Write we cannot test triangles; count a hit on its bounds as blocked (stricter, never looser).
                if(!mesh.isReadable){if(renderer.bounds.IntersectRay(new Ray(eye,delta.normalized),out float hitAt) && hitAt<delta.magnitude-.06f)return false;continue;}
                var vertices=mesh.vertices;var triangles=mesh.triangles;
                var from=filter.transform.InverseTransformPoint(eye);var ray=filter.transform.InverseTransformVector(delta);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=vertices[triangles[i]];var ab=vertices[triangles[i+1]]-a;var ac=vertices[triangles[i+2]]-a;
                    var p=Vector3.Cross(ray,ac);float determinant=Vector3.Dot(ab,p);
                    if(Mathf.Abs(determinant)<.000001f)continue;
                    float inverse=1/determinant;var offset=from-a;float u=Vector3.Dot(offset,p)*inverse;
                    if(u<0 || u>1)continue;
                    var q=Vector3.Cross(offset,ab);float v=Vector3.Dot(ray,q)*inverse;
                    if(v<0 || u+v>1)continue;
                    float t=Vector3.Dot(ac,q)*inverse;
                    if(t>.001f && t<1-.06f/delta.magnitude)return false;
                }
            }
            return true;
        }
        private static void Inspect(Transform target)
        {
            var visual=target.GetComponentInChildren<FishingPortalVisual>(true);
            Check(visual!=null && visual.Height>2,"Storybook portal exists: "+target.parent.name+" / "+target.name);
            var renderers=visual.GetComponentsInChildren<MeshRenderer>(true);
            Check(renderers.Length==3,"Portal retains three batched surfaces");
            var particles=visual.GetComponentsInChildren<ParticleSystem>(true);
            Check(visual.GetComponentsInChildren<Collider>(true).Length==0 && particles.Length==1 && particles[0].main.maxParticles<=FishingPortalVisual.DustBudget && FishingPortalVisual.DustBudget<=40,"Doorway remains passable; one gold dust emitter within the PICO budget");
            var frame=visual.transform.Find("Carved brass arch").GetComponent<MeshFilter>().sharedMesh;
            Check(frame.bounds.max.y>visual.Height+.4f,"Crescent crown rises above the carved arch");
            Check(frame.vertexCount<20000 && frame.triangles.Length/3<12000,"Complete carved arch fits the fixed geometry budget: "+frame.vertexCount+" vertices / "+frame.triangles.Length/3+" triangles");
            var colors=new HashSet<Color>(frame.colors);Check(colors.Count>=8,"Stone courses, bronze inlay, gilt, foliage and realm charm have distinct swatches");
            bool apertureClear=true;float front=float.NegativeInfinity,back=float.PositiveInfinity;
            foreach(var vertex in frame.vertices)
            {
                CheckFinite(vertex);
                if(vertex.y>.22f && vertex.y<1.4f)
                {
                    if(Mathf.Abs(vertex.x)<=.64f)apertureClear=false;
                    front=Mathf.Max(front,vertex.z);back=Mathf.Min(back,vertex.z);
                }
            }
            Check(apertureClear && front>.16f && back<-.16f,"Both stone jambs have real depth and leave the walking aperture clear");
            var veil=visual.transform.Find("Visible flowing light curtain").GetComponent<MeshRenderer>();
            Check(veil.sharedMaterial.shader.name=="BoatAttack/FishingPortal" && veil.sharedMaterial.shader.isSupported && veil.sharedMaterial.renderQueue>=3000,"Mist uses supported translucent portal shader");
            Check(veil.sharedMaterial.HasProperty("_Motion") && veil.sharedMaterial.GetFloat("_Mode")==0,"Mist supports reduced effects independently from stardust");
            var dust=visual.transform.Find("Drifting gold stardust").GetComponent<MeshFilter>().sharedMesh;
            Check(dust.vertexCount==128 && dust.triangles.Length==192,"Stardust is bounded to 32 tiny GPU quads");
            var vertices=dust.vertices;
            bool small=true,alongJambs=true;
            for(int j=0;j<32;j++)
            {
                var center=(vertices[j*4]+vertices[j*4+3])*.5f;
                CheckFinite(center);
                if((vertices[j*4+3]-vertices[j*4]).magnitude>=.072f)small=false;
                if(center.y<visual.Height*.6f && Mathf.Abs(center.x)<=.8f)alongJambs=false;
            }
            Check(small && alongJambs,"Tiny fireflies follow the outline instead of filling the opening");
        }
        private static void CheckFinite(Vector3 v)
        {
            if(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))throw new InvalidOperationException("Non-finite portal geometry");
        }
    }
}
