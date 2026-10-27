using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV042Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V042");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V042] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"Only the isolated automation profile is used");
            Directory.CreateDirectory("Logs/V042/world");
            Check(c.TackleShop.GetComponentInChildren<FishingShopDisplay>().DetailedCounter,"Detailed counter preserves the existing shop and its collider");
            var layout=new FishingHUD.FocusLayout(900,false,CatchPhase.Idle);
            Check(layout.Objective.x>1200 && !layout.Objective.Overlaps(layout.Toolbar) && !layout.Objective.Overlaps(layout.Action),"Objective uses the right margin without covering controls or the center");
            Check(FishingHUD.BlocksToolPointer(new Vector2(1400,900-160),1600,900),"Objective paper blocks firing through its UI");
            Check(c.Voyage.TreasureFocus.y>c.Voyage.TreasureChest.position.y+.97f,"Finale focuses on the chest above its table");
            var character=c.Shore.Character;var body=character.BodySkin;
            var bodyShadow=body.shadowCastingMode;bool bodyHidden=body.forceRenderingOff,handsHidden=character.FirstPersonSkin.forceRenderingOff;
            character.SetFilmPresentation(true);
            Check(body.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.On && !body.forceRenderingOff && character.FirstPersonSkin.forceRenderingOff,"Cinema shows the complete actor and hides the duplicate local arms");
            character.SetFilmPresentation(false);
            Check(body.shadowCastingMode==bodyShadow && body.forceRenderingOff==bodyHidden && character.FirstPersonSkin.forceRenderingOff==handsHidden,"Cinema restores the original local renderer states");
            float[] lengths={39,19.5f,13,13,13,13,26};int sequenceIndex=0;
            foreach(FishingPrologue.Sequence sequence in Enum.GetValues(typeof(FishingPrologue.Sequence)))
            {
                var clip=Resources.Load<AudioClip>("FishingV042/Story/"+sequence);
                Check(clip!=null && Mathf.Abs(clip.length-lengths[sequenceIndex++])<.05f,"Baked score matches the film length: "+sequence);
                if(c.Prologue.Open)c.Prologue.Finish();
                var watch=System.Diagnostics.Stopwatch.StartNew();
                Check(c.Prologue.Begin(sequence),"Production story entry opens "+sequence);watch.Stop();
                if(sequence==FishingPrologue.Sequence.Chart)
                {
                    var camera=c.Prologue.CinemaCamera;
                    var renderers=character.GetComponentsInChildren<Renderer>();var shadows=new UnityEngine.Rendering.ShadowCastingMode[renderers.Length];var hidden=new bool[renderers.Length];
                    for(int i=0;i<renderers.Length;i++){shadows[i]=renderers[i].shadowCastingMode;hidden[i]=renderers[i].forceRenderingOff;}
                    camera.Render();
                    for(int i=0;i<renderers.Length;i++)Check(renderers[i].shadowCastingMode==shadows[i] && renderers[i].forceRenderingOff==hidden[i],"Camera render restores actor state: "+renderers[i].name);
                }
                Debug.Log("[V042] STORY-BEGIN ms="+watch.Elapsed.TotalMilliseconds.ToString("F2")+" sequence="+sequence);
                c.Prologue.Advance(false);c.Prologue.Advance(false);c.Prologue.Advance(true);
                Check(!c.Prologue.Open,"Continue/skip restore control for "+sequence);
                yield return null;
            }
            var caches=new List<FishingFieldWorld.Pickup>();
            foreach(var pickup in c.FieldWorld.Pickups)if(pickup.Cache)caches.Add(pickup);
            Check(caches.Count==8,"Eight appended caches exist in the actual world");
            foreach(var cache in caches)
            {
                Check(cache.Id>=8 && cache.Id<16 && cache.Lid!=null && cache.Root.GetComponent<BoxCollider>()!=null,"Cache has a stable ID, hinge and solid body "+cache.Id);
                Check(c.Shore.Ground(cache.Feet,out var floor) && Mathf.Abs(floor.y-cache.Feet.y)<.1f,"Cache rests on real ground "+cache.Id);
            }
            c.ReturnToHarbor();var rb=c.Player.engine.RB;var berth=FishingIslandWorld.Berth(0)+Vector3.up*.6f;
            rb.position=berth;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
            for(int i=0;i<16;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Use the actual island landing for cache interaction");
            var target=caches[1];bool stood=false;
            for(int i=0;i<12;i++)
            {
                var seed=target.Feet+Quaternion.Euler(0,i*30,0)*Vector3.forward*1.4f;
                if(!c.Shore.Ground(seed,out var feet) || !c.Shore.Clearance(feet))continue;
                if(!c.Shore.Relocate(feet,Quaternion.LookRotation(Vector3.ProjectOnPlane(target.Feet-feet,Vector3.up))))continue;
                stood=true;break;
            }
            Check(stood,"Cache has a supported standing approach");
            for(int i=0;i<16;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);yield return null;}
            if(!c.FirstPerson)c.ToggleView();FishingV036FarmValidation.Aim(c,target.Root.position+Vector3.up*.3f);c.PresentCharacterFrame();
            var eye=c.Shore.Character.EyePosition;var aim=target.Root.position+Vector3.up*.3f;
            Check(c.Shore.ClearSight(eye,aim,target.Root),"Sight accepts the target chest's own collider");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position=Vector3.Lerp(eye,aim,.45f);wall.transform.localScale=new Vector3(.8f,.8f,.8f);Physics.SyncTransforms();
                Check(!c.Shore.ClearSight(eye,aim,target.Root),"Target ownership never ignores an intervening wall");
            }
            finally{UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();}
            FishingV016Validation.Capture(c,"Logs/V042/world","wild-cache-closed");
            Check(!c.FieldWorld.PickupById(target.Id,FieldItem.GuardianPearl,target.Count),"A forged reward cannot replace cache contents");
            Check(c.FieldWorld.PickupById(target.Id,target.Item,target.Count),"Actual reachable cache grants its recorded materials");
            Check(!c.FieldWorld.PickupById(target.Id,target.Item,target.Count),"The same opening cannot pay twice");
            for(float until=Time.realtimeSinceStartup+.35f;Time.realtimeSinceStartup<until;)yield return null;
            Check(Quaternion.Angle(target.ClosedLid,target.Lid.localRotation)>10,"Cache lid opens around its authored hinge");
            FishingV016Validation.Capture(c,"Logs/V042/world","wild-cache-opening");
            for(float until=Time.realtimeSinceStartup+.7f;Time.realtimeSinceStartup<until;)yield return null;
            Check(!target.Root.gameObject.activeSelf,"Opened cache finishes its animation and stays claimed");
            var saved=JsonUtility.FromJson<FishingSaveData>(File.ReadAllText(c.SavePath));
            Check(FishingCareer.TryRestore(saved,out var loaded) && loaded.Backpack.Collected(target.Id),"Production save reload preserves the new receipt");
        }
    }
}
