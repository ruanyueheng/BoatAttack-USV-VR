using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public static class FishingHuntingValidation
    {
        public static void Run()=>FishingV011Validation.Begin("HUNTING");
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Debug.Log("[HUNTING] CHECK: "+message);}
        private static void Feet(FishingPrototype c,Vector3 position,Vector3 target)
        {
            c.Shore.Body.enabled=false;c.Shore.Actor.SetPositionAndRotation(position,Quaternion.LookRotation(Vector3.ProjectOnPlane(target-position,Vector3.up)));
            c.Shore.Body.enabled=true;Physics.SyncTransforms();c.View.ResetLook();c.PresentCharacterFrame();
            var delta=target-c.Shore.Character.EyePosition;
            float pitch=Mathf.Atan2(-delta.y,Vector3.ProjectOnPlane(delta,Vector3.up).magnitude)*Mathf.Rad2Deg;
            c.View.ApplyShoreLookDelta(new Vector2(0,-pitch/.12f));
        }
        private static void Capture(FishingPrototype c,string name)
        {
            Directory.CreateDirectory("Logs/Hunting");
            var camera=c.viewCamera;var old=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
            var rt=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.aspect=1280f/720;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/Hunting/"+name+".png",image.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(image);}
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Hunting!=null && c.Hunting.Rifle!=null,"supplied rifle prefab resolves");
            for(int i=0;i<8;i++)c.Career.CompleteShoreTask(i);c.Career.ClaimShoreRewards();
            int coins=c.Career.Coins;
            Check(!c.TackleShop.BuyRifle() && c.Career.Coins==coins,"remote purchase denied without charge");
            var rb=c.Player.engine.RB;var shop=c.TackleShop;
            rb.position=shop.mooringPoint.position;rb.rotation=shop.transform.rotation;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"shop landing through production control");
            Feet(c,shop.purchasePoint.position+Vector3.up*.06f,shop.purchasePoint.position+shop.transform.forward);
            for(int i=0;i<15;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);yield return null;}
            Check(c.ToggleShop(),"shop opens at actual counter");yield return null;
            Check(shop.BuyRifle() && c.Career.Coins==coins-FishingCareer.HuntingRiflePrice && c.Career.Backpack.Equipped==FieldItem.HuntingRifle,"counter purchase deducts exact price and equips hotbar");
            Check(!shop.BuyRifle(),"duplicate purchase denied");
            Check(!c.Hunting.TryFire(),"shop blocks shooting");shop.Close();yield return null;
            c.ReturnToHarbor();var site=c.Shore.Sites[0];rb.position=site.Berth+Vector3.up;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"hunting starts on real shore");
            c.FieldWorld.enabled=false;
            var animal=c.FieldWorld.Animals.First(a=>a.Kind==LandAnimalKind.Rabbit);
            Check(c.FieldWorld.TryLocation(animal.Feet,3,180,out var ground),"dry position near real rabbit");
            Feet(c,ground+Vector3.up*.06f,animal.Hitbox.bounds.center);
            for(int i=0;i<35;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}
            Debug.Log("[HUNTING] POSE: presented="+c.Hunting.Presented+" obstruction="+c.Hunting.Obstructed+" gun="+c.viewCamera.transform.InverseTransformPoint(c.Hunting.Rifle.position)+" L="+c.viewCamera.WorldToViewportPoint(c.Hands.Left.Palm.position)+" R="+c.viewCamera.WorldToViewportPoint(c.Hands.Right.Palm.position));
            Check(c.Hunting.Presented && !c.Hunting.Obstructed,"rifle visible in free space");
            Check(Vector3.Distance(c.Shore.Character.PalmPosition(true),c.Hands.Left.Palm.position)<.035f && Vector3.Distance(c.Shore.Character.PalmPosition(false),c.Hands.Right.Palm.position)<.035f,"both real hands reach independent rifle grips");
            Capture(c,"first-person-rifle");
            c.Shore.SetCrouch(true);for(int i=0;i<30;i++)yield return null;c.PresentCharacterFrame();
            Check(c.Hunting.Presented && !c.Hunting.Obstructed,"crouched rifle retains safe grip");Capture(c,"crouched-rifle");
            c.Shore.SetCrouch(false);for(int i=0;i<30;i++)yield return null;
            c.ToggleView();for(int i=0;i<8;i++)yield return null;c.PresentCharacterFrame();
            Check(c.Hunting.Presented,"third person keeps the same weapon and hand rig");Capture(c,"third-person-rifle");
            c.ToggleView();yield return null;
            foreach(float aspect in new[]{16f/9,5f/4,21f/9})
            {
                c.viewCamera.aspect=aspect;c.PresentCharacterFrame();
                Check(c.Hunting.Presented && !c.Hunting.Obstructed,"rifle stays usable at aspect "+aspect);
            }
            c.viewCamera.ResetAspect();
            foreach(float pitch in new[]{-30f,0f,30f})
            {
                c.View.ResetLook();c.View.ApplyShoreLookDelta(new Vector2(0,-pitch/.12f));
                for(int frame=0;frame<4;frame++){c.PresentCharacterFrame();yield return null;}
                Check(c.Hunting.Presented && !c.Hunting.Obstructed,"open-space pitch keeps gun usable "+pitch+" reason="+c.Hunting.ObstructionReason);
                Check(Vector3.Distance(c.Shore.Character.PalmPosition(true),c.Hands.Left.Palm.position)<.04f
                    && Vector3.Distance(c.Shore.Character.PalmPosition(false),c.Hands.Right.Palm.position)<.04f,"grips remain reachable at pitch "+pitch);
                Capture(c,"pitch-"+pitch);
            }
            Feet(c,c.Shore.Position,animal.Hitbox.bounds.center);for(int i=0;i<6;i++)yield return null;c.PresentCharacterFrame();
            Capture(c,"first-person-rifle");
            c.TogglePause();Check(!c.Hunting.TryFire(),"pause blocks shooting");c.TogglePause();yield return null;
            // Place an ordinary solid wall on the eye/target segment. Target must survive.
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Hunting validation wall";
            var eye=c.Shore.Character.EyePosition;var target=animal.Hitbox.bounds.center;
            wall.transform.SetPositionAndRotation(Vector3.Lerp(eye,target,.55f),Quaternion.LookRotation(target-eye));wall.transform.localScale=new Vector3(2,2,.16f);Physics.SyncTransforms();
            c.PresentCharacterFrame();c.Hunting.TryFire();Check(!animal.Dead,"solid wall blocks animal hit");
            wall.transform.position=eye+c.viewCamera.transform.forward*.20f;Physics.SyncTransforms();c.PresentCharacterFrame();
            Check(c.Hunting.Obstructed && !c.Hunting.TryFire(),"close wall lowers or hides gun and prevents muzzle clipping shot");Capture(c,"wall-clearance");
            UnityEngine.Object.Destroy(wall);yield return null;
            float until=Time.time+FishingHunting.ShotInterval+.05f;while(Time.time<until)yield return null;
            c.PresentCharacterFrame();Check(c.Hunting.TryFire() && animal.Dead,"one actual muzzle ray kills one rabbit");
            int shots=c.Hunting.ShotsFired;Check(!c.Hunting.TryFire() && c.Hunting.ShotsFired==shots,"held/repeated input cannot bypass single-shot cooldown");
            Check(c.Career.HuntedAnimal(animal.Id)!=null && !animal.Hitbox.enabled,"dead animal stops movement and shooting collider");
            var saved=FishingSaveStore.Load(c.SavePath,out var warning);Check(warning.Length==0 && saved.HuntedAnimal(animal.Id)!=null,"kill persisted to isolated save on disk");
            Check(!c.Hunting.CollectAnimal(animal.Id),"loot cannot be collected remotely");
            var corpse=animal.Feet;
            Check(c.Shore.Ground(corpse+Vector3.back*.45f,out ground),"corpse approach ground");
            Feet(c,ground+Vector3.up*.07f,animal.Root.GetComponent<Renderer>().bounds.center);
            for(int i=0;i<35;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}
            int meat=c.Career.Backpack.Count(FieldItem.Meat),fur=c.Career.Backpack.Count(FieldItem.Fur);
            Debug.Log("[HUNTING] LOOT PREFLIGHT: "+c.InspectWorldInteractionStart());
            Check(c.RequestWorldInteraction(),"E starts corpse pickup through contact action");
            for(int i=0;i<180 && c.WorldActionBusy;i++)yield return null;
            Check(c.Career.Backpack.Count(FieldItem.Meat)==meat+1 && c.Career.Backpack.Count(FieldItem.Fur)==fur+1,"corpse contact grants one meat and one fur");
            Check(!c.Hunting.CollectAnimal(animal.Id) && !animal.Root.gameObject.activeSelf,"claimed corpse disappears and never grants twice");
            saved=FishingSaveStore.Load(c.SavePath,out warning);Check(saved.HuntedAnimal(animal.Id).collected,"loot receipt persisted on disk");
            c.FieldWorld.enabled=true;
        }
    }
}
