using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV026SwimmingValidation
    {
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException("V026 swimming: "+label);Debug.Log("[V026-SWIMMING] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            string evidence=Path.GetFullPath("Logs/v026-swimming-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            c.ReturnToHarbor();
            var rb=c.Player.engine.RB;rb.position=FishingIslandWorld.Berth(0)+Vector3.up*.7f;rb.rotation=Quaternion.identity;
            rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
            for(int i=0;i<20;i++)yield return null;
            Check(c.Shore.TryDisembark(),"production island landing starts traversal fixture");
            var jetty=FishingIslandWorld.Center(0)+new Vector3(0,1.12f,33);
            Check(c.Shore.Relocate(jetty,Quaternion.identity),"real jetty has supported clearance");
            if(c.FirstPerson)c.ToggleView();
            var ch=c.Shore.Character;
            ch.Pose(true,0,false,true,false,true);
            SkinnedMeshRenderer legs=null;
            foreach(var skin in ch.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.name=="Beach_Legs")legs=skin;
            Check(legs!=null,"source leg mesh supplies actual waist vertices");
            var baked=new Mesh();var waist=new List<int>();
            try
            {
                legs.BakeMesh(baked);var vertices=baked.vertices;float top=float.MinValue;
                for(int i=0;i<vertices.Length;i++){vertices[i]=ch.transform.InverseTransformPoint(legs.transform.TransformPoint(vertices[i]));top=Mathf.Max(top,vertices[i].y);}
                for(int i=0;i<vertices.Length;i++)if(vertices[i].y>=top-.012f)waist.Add(i);
                Check(waist.Count>=3,"waist measurement follows rendered source surface");
                var dryHead=Center(ch.HeadSkin,null,baked);var dryHip=Center(legs,waist,baked);var eye=ch.EyePosition;
                var rootPosition=ch.transform.position;var rootRotation=ch.transform.rotation;
                foreach(float speed in new[]{1.45f,2.7f,0f})
                {
                    ch.Pose(true,0,false,false,false,true);ch.ApplyTraversalPose(true,false,speed,false);
                    var head=Center(ch.HeadSkin,null,baked);var hip=Center(legs,waist,baked);
                    float angle=Mathf.Asin(Mathf.Abs((head-hip).normalized.y))*Mathf.Rad2Deg;
                    Check(angle<(speed>0?25:42),"rendered head/waist axis follows water at speed "+speed+"; angle="+angle.ToString("F2"));
                    Check(Vector3.Dot(head-hip,ch.transform.forward)>.3f,"head leads waist in swimming direction");
                    Check(Vector3.Distance(ch.EyePosition,eye)<.00001f && ch.transform.position==rootPosition
                        && Quaternion.Angle(ch.transform.rotation,rootRotation)<.001f,"swim overlay keeps camera anchor and motor upright");
                    ch.ApplyHandTargets(null,null);
                    Check(Vector3.Distance(head,Center(ch.HeadSkin,null,baked))<.0001f,"final hand stage preserves swimming geometry");
                }
                ch.Pose(true,0,false,true,false,true);
                Check(Vector3.Distance(dryHead,Center(ch.HeadSkin,null,baked))<.002f && Vector3.Distance(dryHip,Center(legs,waist,baked))<.002f,
                    "leaving swimming restores complete dry-land geometry without accumulated rotation");
                ch.ApplyTraversalPose(true,false,1.45f,true);
                foreach(bool left in new[]{true,false})
                {
                    var target=left?c.Hands.Left:c.Hands.Right;
                    target.SetPose(eye+ch.transform.right*(left?-.3f:.3f)+ch.transform.forward*.4f-Vector3.up*.25f,ch.transform.rotation,.4f);
                }
                ch.ApplyHandTargets(c.Hands.Left,c.Hands.Right,true);
                Check(Vector3.Distance(ch.PalmPosition(true),c.Hands.Left.Palm.position)<.002f
                    && Vector3.Distance(ch.PalmPosition(false),c.Hands.Right.Palm.position)<.002f,"tracked palms retain their independent world goals after full-body swimming tilt");
                c.Hands.Left.Disable();c.Hands.Right.Disable();
                c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.right);
                for(int i=0;i<85;i++)c.Shore.Move(Vector2.up,true,false,false,.05f);
                Check(c.Shore.Swimming,"walking off the real jetty enters swimming");c.PresentCharacterFrame();
                float liveAngle=Mathf.Asin(Mathf.Abs((Center(ch.HeadSkin,null,baked)-Center(legs,waist,baked)).normalized.y))*Mathf.Rad2Deg;
                Check(liveAngle<25,"production presentation keeps horizontal mesh posture; angle="+liveAngle.ToString("F2"));
                Check(ch.EyePosition.y>-.1f && ch.EyePosition.y<.65f,"buoyancy settles eye near the water surface instead of standing a metre above it; eye="+ch.EyePosition.y.ToString("F2"));
                var focus=(Center(ch.HeadSkin,null,baked)+Center(legs,waist,baked))*.5f;
                Capture(c,evidence,"swimming-side",focus,ch.transform.right*4+Vector3.up*.35f);
                Capture(c,evidence,"swimming-oblique",focus,ch.transform.right*3-ch.transform.forward*2+Vector3.up*1.1f);
                if(!c.FirstPerson)c.ToggleView();c.PresentCharacterFrame();
                Check(Vector3.Distance(c.viewCamera.transform.position,ch.EyePosition)<.002f,"first-person camera remains on calibrated eye anchor");
                var cameraRotation=c.viewCamera.transform.rotation;
                ch.Pose(true,0,false,false,true,true);ch.ApplyTraversalPose(true,false,0,false,true);
                Check(Quaternion.Angle(cameraRotation,c.viewCamera.transform.rotation)<.001f,"resting and paused swim pose never pitches the camera");
                for(int i=0;i<180 && c.Shore.Swimming;i++)
                {
                    var forward=Vector3.ProjectOnPlane(jetty-c.Shore.Position,Vector3.up);
                    c.viewCamera.transform.rotation=Quaternion.LookRotation(forward);
                    c.Shore.Move(Vector2.up,true,true,false,.05f);
                }
                Check(!c.Shore.Swimming && c.Shore.Ground(c.Shore.Position,out _) && c.Shore.Clearance(c.Shore.Position),"swim plus jump climbs back onto the actual low jetty");
                c.PresentCharacterFrame();
                Check(Mathf.Abs((Center(ch.HeadSkin,null,baked)-Center(legs,waist,baked)).normalized.y)>.9f,"landed character returns upright");
            }
            finally
            {
                UnityEngine.Object.Destroy(baked);c.Hands.Left.Disable();c.Hands.Right.Disable();c.ReturnToHarbor();c.PresentCharacterFrame();
            }
            Debug.Log("[V026-SWIMMING] EVIDENCE: "+evidence);
        }
        private static Vector3 Center(SkinnedMeshRenderer skin,List<int> selected,Mesh mesh)
        {
            skin.BakeMesh(mesh);var vertices=mesh.vertices;var sum=Vector3.zero;
            if(selected==null){foreach(var v in vertices)sum+=skin.transform.TransformPoint(v);return sum/vertices.Length;}
            foreach(int i in selected)sum+=skin.transform.TransformPoint(vertices[i]);return sum/selected.Count;
        }
        private static void Capture(FishingPrototype c,string directory,string name,Vector3 focus,Vector3 offset)
        {
            var camera=c.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;
            try{camera.SetPositionAndRotation(focus+offset,Quaternion.LookRotation(-offset));FishingV016Validation.Capture(c,directory,name);}
            finally{camera.SetPositionAndRotation(position,rotation);}
        }
    }
}
