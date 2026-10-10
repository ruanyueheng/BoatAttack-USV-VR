using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV016Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V016");
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException(label);Debug.Log("[V016] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            var evidence=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/v016-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(evidence);
            c.ReturnToHarbor();for(int i=0;i<120;i++)yield return null;
            c.PresentCharacterFrame();Check(!c.Hands.Rod.gameObject.activeInHierarchy,"Navigation hides the rod");
            Capture(c,evidence,"01-navigation");
            var hull=c.Player.GetComponent<MeshCollider>();
            Check(hull!=null && hull.convex && !hull.isTrigger && hull.sharedMesh.bounds.size.x<2.1f,"Physical compact hull matches the boat");
            Check(c.Player.boatRenderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3<6500,"Visible skiff triangle budget");
            var mesh=c.Player.boatRenderer.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var colors=mesh.colors;
            float motorZ=0;int motorVertices=0;
            for(int i=0;i<vertices.Length;i++)if(Mathf.Abs(vertices[i].x)<.22f && vertices[i].y>.78f && colors[i].r<.2f)
            {motorZ+=vertices[i].z;motorVertices++;}
            Check(motorVertices>0 && motorZ/motorVertices < -1.6f,"Visible outboard is astern, matching the engine's thrust point");
            Check(Vector3.Angle(c.Player.transform.up,Vector3.up)<20,"Boat remains upright after sustained floating; tilt="+Vector3.Angle(c.Player.transform.up,Vector3.up));
            Check(c.FishBox.lossyScale.x<.5f && FishingDetailArt.NetRadius<.3f,"Human-scale crate and net");
            Check(Object.FindObjectsOfType<BuoyManager>().Length==0,"Race particle buoys removed from fishing scene");
            int markers=0,reefs=0;
            foreach(var collider in Object.FindObjectsOfType<Collider>())
            {
                if(collider.name=="Navigation buoy")
                {
                    markers++;Check(!collider.isTrigger,"Navigation marker has solid collision");
                    var bounds=collider.bounds;
                    Check(Physics.Raycast(bounds.center+Vector3.right*(bounds.extents.x+2),Vector3.left,out var hit,4)
                        && hit.collider==collider,"Navigation approach hits its real buoy collider");
                }
                if(collider.name=="Faceted limestone")reefs++;
            }
            Check(markers==c.fishingSpots.Length && reefs==9,"One marker per ground and nine physical reefs");
            var marker=Object.FindObjectsOfType<CapsuleCollider>();
            var buoy=Array.Find(marker,m=>m.name=="Navigation buoy");var rb=c.Player.engine.RB;
            rb.position=buoy.transform.position-Vector3.forward*4+Vector3.up*.35f;rb.rotation=Quaternion.identity;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
            Check(rb.SweepTest(Vector3.forward,out var approach,8) && approach.collider==buoy,"Actual boat hull sweep is blocked by the navigation buoy");
            c.ReturnToHarbor();
            c.MoveToSpotForAutomation(0);c.PresentCharacterFrame();
            Check(!c.Hands.Rod.gameObject.activeInHierarchy,"Arriving without choosing fishing keeps rod stowed");
            c.Interact();for(int i=0;i<15;i++)yield return null;
            c.PresentCharacterFrame();Check(c.Anchored && c.Hands.Rod.gameObject.activeInHierarchy,"Choosing fishing equips rod");
            Capture(c,evidence,"02-equipped");
            var crank=c.Hands.Crank;var wrist=c.Hands.Left.transform.rotation;
            for(int i=0;i<12;i++)
            {
                crank.localRotation=Quaternion.AngleAxis(i*30,Vector3.right);c.PresentCharacterFrame();
                Check(Vector3.Distance(c.Shore.Character.PalmPosition(true),c.Hands.Left.Palm.position)<.035f,"Reel grip reached at angle "+i*30);
                Check(Vector3.Distance(c.Hands.Left.Palm.position,c.Hands.Knob.position)<.035f,"Left palm stays on knob at angle "+i*30);
                Check(Vector3.Distance(c.Hands.Knob.position,FishingContactGeometry.ClosestPoint(crank,c.Hands.Knob.position))<.019f,"Knob attachment lies on the rendered handle at angle "+i*30);
                Check(Quaternion.Angle(wrist,c.Hands.Left.transform.rotation)<1,"Free-spinning knob does not rotate the wrist");
                if(i%3==0)Capture(c,evidence,"03-crank-"+i);
            }
            Check(c.Cast(),"Production cast starts landing fixture");
            for(int i=0;i<1000 && c.Session.Phase==CatchPhase.Waiting;i++)c.Session.Tick(.02f,false);
            Check(c.Hook(),"Production hook");
            for(int i=0;i<6000 && c.Session.Phase==CatchPhase.Fighting;i++)c.Session.Tick(.02f,c.Session.Tension<.6f);
            Check(c.Session.Phase==CatchPhase.Landing,"Production fight reaches net stage");
            c.PresentCharacterFrame();
            Capture(c,evidence,"04-landing");
            Check(c.Hands.Rod.gameObject.activeInHierarchy && c.Hands.Right.Visible,"Right hand retains rod while left hand nets");
            Check(Vector3.Distance(c.Shore.Character.PalmPosition(false),c.Hands.Right.Palm.position)<.035f,"Right palm actually holds rod during landing; error="+Vector3.Distance(c.Shore.Character.PalmPosition(false),c.Hands.Right.Palm.position)+" reach="+c.Shore.Character.ArmReach(false)+" shoulderDistance="+Vector3.Distance(c.Shore.Character.ShoulderPosition(false),c.Hands.Right.Palm.position));
            Check(Vector3.Distance(c.Shore.Character.PalmPosition(true),c.Hands.Left.Palm.position)<.035f,"Left palm actually reaches net handle");
            Check(c.Hands.CanReach(true,c.Hands.Left.Palm.position,-.005f),"Net grip remains within real arm reach");
            Check(c.Scoop(),"Scoop available");for(int i=0;i<45 && !c.HasPendingCatch;i++)c.CatchRig.StepDesktop(.02f);
            c.PresentCharacterFrame();Check(c.HasPendingCatch && !c.Hands.Rod.gameObject.activeInHierarchy,"Net catch stows rod before fish inspection");
            Capture(c,evidence,"05-net-catch");Check(c.ResolveCatch(true),"Compact box accepts caught fish through existing transaction");
            c.LeaveStation();c.PresentCharacterFrame();Check(!c.Hands.Rod.gameObject.activeInHierarchy,"Leaving station stows rod");
            c.ToggleView();for(int i=0;i<60;i++)yield return null;
            c.PresentCharacterFrame();
            Check(Vector3.Angle(c.Player.transform.up,Vector3.up)<20,"Boat remains upright after netting; tilt="+Vector3.Angle(c.Player.transform.up,Vector3.up));
            Capture(c,evidence,"06-skiff");
            Debug.Log("[V016] EVIDENCE: "+evidence);
        }
        private static void Capture(FishingPrototype c,string directory,string label)
        {
            var camera=c.viewCamera;var target=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
            var rt=new RenderTexture(1600,900,24);var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.aspect=1600f/900;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,label+".png"),image.EncodeToPNG());}
            finally{camera.targetTexture=target;camera.aspect=aspect;RenderTexture.active=active;rt.Release();Object.Destroy(rt);Object.Destroy(image);}
        }
    }
}
