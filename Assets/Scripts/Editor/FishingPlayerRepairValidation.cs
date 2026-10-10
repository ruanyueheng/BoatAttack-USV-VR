using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingPlayerRepairValidation
    {
        public static void Run()=>FishingV011Validation.Begin("PLAYER");
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Debug.Log("[PLAYER] CHECK: "+message);}
        private static void SetFeet(FishingPrototype c,Vector3 feet,Vector3 target)
        {
            c.Shore.Body.enabled=false;c.Shore.Actor.position=feet;
            var forward=Vector3.ProjectOnPlane(target-feet,Vector3.up);
            if(forward.sqrMagnitude>.0001f)c.Shore.Actor.rotation=Quaternion.LookRotation(forward);
            c.Shore.Body.enabled=true;Physics.SyncTransforms();c.View.ResetLook();
            c.PresentCharacterFrame();
            var delta=target-c.Shore.Character.EyePosition;
            float pitch=Mathf.Atan2(-delta.y,Vector3.ProjectOnPlane(delta,Vector3.up).magnitude)*Mathf.Rad2Deg;
            c.View.ApplyShoreLookDelta(new Vector2(0,-Mathf.Clamp(pitch,-65,65)/.12f));
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            c.ReturnToHarbor();var berth=c.Shore.Sites[0].Berth;
            var rb=c.Player.engine.RB;rb.position=berth+Vector3.up;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"shore fixture lands through production control");
            var pickup=c.FieldWorld.Pickups.First(p=>p.Id==1);
            var seed=pickup.Feet+Vector3.back*.55f;
            Check(c.Shore.Ground(seed,out var ground) && c.Shore.Clearance(ground),"pickup approach has clear dry ground");
            SetFeet(c,ground+Vector3.up*.08f,pickup.Root.position);
            for(int i=0;i<40;i++){c.Shore.Move(Vector2.zero,false,false,false,1f/60);c.PresentCharacterFrame();yield return null;}
            c.FieldWorld.Sense();c.WorkshopWorld.Sense();
            var proof=c.InspectWorldInteractionStart();Debug.Log("[PLAYER] PICKUP PRECHECK: "+proof);
            Check(proof.TargetId=="pickup:1","ordinary nearby pickup is selected");
            int count=c.Career.Backpack.Count(pickup.Item);
            bool began=c.RequestWorldInteraction();Debug.Log("[PLAYER] PICKUP BEGAN="+began+" message="+c.Message);
            Check(began,"press E near a low pickup starts the production pickup action");
            for(int i=0;i<180 && c.WorldActionBusy;i++)yield return null;
            Check(c.LastActionSucceeded && c.Career.Backpack.Count(pickup.Item)==count+pickup.Count,"nearby pickup commits exactly once");
            yield return null;
            var joins=CheckJoins(c);while(joins.MoveNext())yield return null;
            c.ReturnToHarbor();c.MoveToSpotForAutomation(0);c.Interact();
            for(int i=0;i<40;i++){c.PresentCharacterFrame();yield return null;}
            var camera=c.viewCamera;
            int after=CentralBodyVertices(c);Capture(camera,"fishing-after.png");
            // Recreate only the old independent rod/hand goals for a same-frame
            // before/after comparison. No production bones or model scales change.
            c.Hands.GetToolFrame(out var origin,out var frame);var hold=Quaternion.Euler(0,frame.eulerAngles.y,0);var rod=c.Hands.Rod;
            rod.SetPositionAndRotation(origin+hold*new Vector3(.05f,.15f,.34f),hold*Quaternion.Euler(-32, -12,0));
            var reel=rod.InverseTransformPoint(c.Hands.Knob.position+rod.TransformVector(new Vector3(-.024f,.012f,-.035f)));
            var palm=new Vector3(.058f,.012f,-.045f);c.Hands.FitToolPose(rod,reel,palm);
            c.Hands.Right.SetFromGrip(rod,palm,Quaternion.LookRotation(Vector3.up,Vector3.right),.86f);
            c.Hands.Left.SetFromGrip(rod,reel,Quaternion.identity,.85f);
            c.Shore.Character.ApplyHandTargets(c.Hands.Left,c.Hands.Right);
            int before=CentralBodyVertices(c);Capture(camera,"fishing-before.png");
            c.PresentCharacterFrame();
            Debug.Log("[PLAYER] FISHING VIEW: central body vertices before="+before+" after="+after);
            Check(before>after && after<before*.5f,"lower fishing grip reduces central body/arm occlusion by more than half");
        }
        private static int CentralBodyVertices(FishingPrototype c)
        {
            var skin=c.Shore.Character.FirstPersonSkin;var mesh=new Mesh();skin.BakeMesh(mesh);int count=0;
            var vertices=mesh.vertices;
            // 统计真正参与本地绘制的索引，不把只投影的全身网格当成可见遮挡。
            foreach(int index in skin.sharedMesh.triangles.Distinct())
            {
                var p=c.viewCamera.WorldToViewportPoint(skin.transform.TransformPoint(vertices[index]));
                if(p.z>c.viewCamera.nearClipPlane && p.x>.3f && p.x<.7f && p.y>.4f && p.y<.9f)count++;
            }
            UnityEngine.Object.Destroy(mesh);return count;
        }
        private static IEnumerator CheckJoins(FishingPrototype c)
        {
            var a=GameObject.CreatePrimitive(PrimitiveType.Cube);var b=GameObject.CreatePrimitive(PrimitiveType.Cube);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                a.name="Player regression low floor";a.transform.position=new Vector3(5000,.9f,4999);a.transform.localScale=new Vector3(4,.6f,2);
                b.name="Player regression raised floor";b.transform.position=new Vector3(5000,1.1f,5002.04f);b.transform.localScale=new Vector3(4,.6f,4);
                wall.name="Player regression solid wall";wall.transform.position=new Vector3(5000,2.6f,5002.5f);wall.transform.localScale=new Vector3(4,2.4f,.2f);wall.SetActive(false);
                Physics.SyncTransforms();c.Shore.SetCrouch(false,true);SetFeet(c,new Vector3(5000,1.22f,4999),new Vector3(5000,1.22f,5002));
                c.View.ResetLook();
                for(int i=0;i<6;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);yield return null;}
                c.viewCamera.transform.rotation=Quaternion.identity;
                for(int i=0;i<35;i++){c.Shore.Walk(Vector2.up,false,.05f);yield return null;}
                Check(c.Shore.Position.z>5001.5f && c.Shore.Position.y>1.35f,"walk across a 4cm floor seam and a 20cm lip");
                wall.SetActive(true);Physics.SyncTransforms();SetFeet(c,new Vector3(5000,1.42f,5001),new Vector3(5000,1.42f,5003));c.viewCamera.transform.rotation=Quaternion.identity;
                for(int i=0;i<35;i++){c.Shore.Walk(Vector2.up,false,.05f);yield return null;}
                Check(c.Shore.Position.z<5002.3f,"seam recovery cannot pass through a full-height wall");
                wall.SetActive(false);Physics.SyncTransforms();
                for(int i=0;i<60;i++){c.Shore.Walk(Vector2.up,false,.05f);yield return null;}
                Check(c.Shore.Position.z<5004.4f && c.Shore.Position.y>.6f,"seam recovery cannot bridge open water beyond the floor edge");
            }
            finally{a.SetActive(false);b.SetActive(false);wall.SetActive(false);UnityEngine.Object.Destroy(a);UnityEngine.Object.Destroy(b);UnityEngine.Object.Destroy(wall);}
        }
        private static void Capture(Camera camera,string name)
        {
            Directory.CreateDirectory("Logs/PlayerRepair");
            var prior=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D image=null;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/PlayerRepair/"+name,image.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)UnityEngine.Object.Destroy(image);}
        }
    }
}
