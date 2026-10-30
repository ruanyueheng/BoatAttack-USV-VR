using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BoatAttack.EditorTools
{
    public static class FishingNpcFacingValidation
    {
        public static void Run()=>FishingV011Validation.Begin("NPC-FACING");
        public static IEnumerator Checks(FishingPrototype c)
        {
            if(!c.Automated || !c.HasIsolatedProfile)throw new InvalidOperationException("Facing checks require the isolated automation profile.");
            string evidence=Path.GetFullPath("Logs/V052/NpcFacing");Directory.CreateDirectory(evidence);
            c.Residents.FreezeSchedule=true;
            for(int f=0;f<10;f++)yield return null;
            foreach(int id in new[]{0,1,2,3,5})
            {
                var npc=c.Residents.Person(id);var brain=c.Residents.Brain(id);var animator=npc.GetComponentInChildren<Animator>();
                brain.enabled=false;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var graph=(PlayableGraph)typeof(FishingResidentBrain).GetField("_graph",flags).GetValue(brain);
                var mixer=(AnimationMixerPlayable)typeof(FishingResidentBrain).GetField("_mixer",flags).GetValue(brain);
                foreach(var clip in new[]{"Idle","Talk","Fix","Interact","Walk"})
                {
                    brain.Play(clip,0,9999);
                    int active=(int)typeof(FishingResidentBrain).GetField("_active",flags).GetValue(brain);
                    mixer.SetInputWeight(active,1);mixer.SetInputWeight(1-active,0);graph.Evaluate(.8f);
                    var shoulders=animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position-animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                    var feet=animator.GetBoneTransform(HumanBodyBones.LeftToes).position-animator.GetBoneTransform(HumanBodyBones.LeftFoot).position
                        +animator.GetBoneTransform(HumanBodyBones.RightToes).position-animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
                    Vector3 eyes=Vector3.zero;int n=0;
                    foreach(var skin in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        if(!skin.name.Contains("LOD0"))continue;
                        var baked=new Mesh();skin.BakeMesh(baked);var vertices=baked.vertices;var uv=baked.uv;
                        for(int i=0;i<vertices.Length;i++)if(uv[i].x>=.75f && uv[i].x<=.875f && uv[i].y>=0 && uv[i].y<=.125f){eyes+=skin.transform.TransformPoint(vertices[i]);n++;}
                        UnityEngine.Object.Destroy(baked);
                    }
                    if(n==0)throw new InvalidOperationException("Resident eyes are missing "+id);
                    var face=Vector3.ProjectOnPlane(eyes/n-animator.GetBoneTransform(HumanBodyBones.Head).position,Vector3.up).normalized;
                    var toes=Vector3.ProjectOnPlane(feet,Vector3.up).normalized;
                    var chest=Vector3.Cross(shoulders,Vector3.up).normalized;
                    Debug.Log("[NPC-FACING] id="+id+" clip="+clip+" chest="+chest.ToString("F3")+" face="+face.ToString("F3")+" toes="+toes.ToString("F3")+" forward="+npc.forward.ToString("F3")
                        +" faceToes="+Vector3.Dot(face,toes)+" faceForward="+Vector3.Dot(face,npc.forward));
                    if(Vector3.Dot(face,npc.forward)<.5f || (clip!="Fix" && Vector3.Dot(face,toes)<.4f))
                        throw new InvalidOperationException("Resident face/feet point backwards: "+id+" "+clip);
                    yield return null;
                    var target=npc.position+Vector3.up*1.1f;var eye=target+npc.forward*2.2f+Vector3.up*.2f;
                    c.viewCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
                    FishingV016Validation.Capture(c,evidence,"resident-"+id+"-"+clip);
                }
                brain.enabled=true;
            }
        }
    }
}
