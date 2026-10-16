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
            Check(c.RifleHunting!=null && c.RifleHunting.Rifle!=null,"supplied rifle prefab resolves");
            var trace=(LineRenderer)typeof(FishingRifleHunting).GetField("_trace",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(c.RifleHunting);
            Check(trace.sharedMaterial.shader==c.PaletteShader,"Shot trace reuses the packaged palette shader instead of a stripped string-only shader");
            ValidateIcons();
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
            Check(!c.RifleHunting.TryFire(),"shop blocks shooting");shop.Close();yield return null;
            c.ReturnToHarbor();var site=c.Shore.Sites[0];rb.position=site.Berth+Vector3.up;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"hunting starts on real shore");
            c.FieldWorld.enabled=false;
            var animal=c.FieldWorld.Animals.First(a=>a.Kind==LandAnimalKind.Rabbit);
            Check(c.FieldWorld.TryLocation(animal.Feet,3,180,out var ground),"dry position near real rabbit");
            Feet(c,ground+Vector3.up*.06f,animal.Hitbox.bounds.center);
            for(int i=0;i<35;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}
            Debug.Log("[HUNTING] POSE: presented="+c.RifleHunting.Presented+" obstruction="+c.RifleHunting.Obstructed+" gun="+c.viewCamera.transform.InverseTransformPoint(c.RifleHunting.Rifle.position)+" L="+c.viewCamera.WorldToViewportPoint(c.Hands.Left.Palm.position)+" R="+c.viewCamera.WorldToViewportPoint(c.Hands.Right.Palm.position));
            Check(c.RifleHunting.Presented && !c.RifleHunting.Obstructed,"rifle visible in free space");
            Check(Vector3.Distance(c.Shore.Character.PalmPosition(true),c.Hands.Left.Palm.position)<.035f && Vector3.Distance(c.Shore.Character.PalmPosition(false),c.Hands.Right.Palm.position)<.035f,"both real hands reach independent rifle grips");
            Capture(c,"first-person-rifle");
            c.Shore.SetCrouch(true);for(int i=0;i<30;i++)yield return null;c.PresentCharacterFrame();
            Check(c.RifleHunting.Presented && !c.RifleHunting.Obstructed,"crouched rifle retains safe grip");Capture(c,"crouched-rifle");
            c.Shore.SetCrouch(false);for(int i=0;i<30;i++)yield return null;
            c.ToggleView();for(int i=0;i<8;i++)yield return null;c.PresentCharacterFrame();
            Check(c.RifleHunting.Presented,"third person keeps the same weapon and hand rig");Capture(c,"third-person-rifle");
            c.ToggleView();yield return null;
            foreach(float aspect in new[]{16f/9,5f/4,21f/9})
            {
                c.viewCamera.aspect=aspect;c.PresentCharacterFrame();
                Check(c.RifleHunting.Presented && !c.RifleHunting.Obstructed,"rifle stays usable at aspect "+aspect);
            }
            c.viewCamera.ResetAspect();
            foreach(float pitch in new[]{-30f,0f,30f})
            {
                c.View.ResetLook();c.View.ApplyShoreLookDelta(new Vector2(0,-pitch/.12f));
                for(int frame=0;frame<4;frame++){c.PresentCharacterFrame();yield return null;}
                Check(c.RifleHunting.Presented && !c.RifleHunting.Obstructed,"open-space pitch keeps gun usable "+pitch+" reason="+c.RifleHunting.ObstructionReason);
                Check(Vector3.Distance(c.Shore.Character.PalmPosition(true),c.Hands.Left.Palm.position)<.04f
                    && Vector3.Distance(c.Shore.Character.PalmPosition(false),c.Hands.Right.Palm.position)<.04f,"grips remain reachable at pitch "+pitch);
                Capture(c,"pitch-"+pitch);
            }
            Feet(c,c.Shore.Position,animal.Hitbox.bounds.center);for(int i=0;i<6;i++)yield return null;c.PresentCharacterFrame();
            Capture(c,"first-person-rifle");
            c.TogglePause();Check(!c.RifleHunting.TryFire(),"pause blocks shooting");c.TogglePause();yield return null;
            typeof(FishingXRNotebook).GetProperty("IsOpen").SetValue(c.XRNotebook,true);
            try{Check(!c.RifleHunting.TryFire(),"notebook blocks shooting even through a direct tool call");}
            finally{c.XRNotebook.Close();}
            // Place an ordinary solid wall on the eye/target segment. Target must survive.
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Hunting validation wall";
            var eye=c.Shore.Character.EyePosition;var target=animal.Hitbox.bounds.center;
            wall.transform.SetPositionAndRotation(Vector3.Lerp(eye,target,.55f),Quaternion.LookRotation(target-eye));wall.transform.localScale=new Vector3(2,2,.16f);Physics.SyncTransforms();
            c.PresentCharacterFrame();c.RifleHunting.TryFire();Check(!animal.Dead,"solid wall blocks animal hit");
            wall.transform.position=eye+c.viewCamera.transform.forward*.20f;Physics.SyncTransforms();c.PresentCharacterFrame();
            Check(c.RifleHunting.Obstructed && !c.RifleHunting.TryFire(),"close wall lowers or hides gun and prevents muzzle clipping shot");Capture(c,"wall-clearance");
            UnityEngine.Object.Destroy(wall);yield return null;
            float until=Time.time+FishingRifleHunting.ShotInterval+.05f;while(Time.time<until)yield return null;
            c.PresentCharacterFrame();Check(c.RifleHunting.TryFire() && animal.Dead,"one actual muzzle ray kills one rabbit");
            int shots=c.RifleHunting.ShotsFired;Check(!c.RifleHunting.TryFire() && c.RifleHunting.ShotsFired==shots,"held/repeated input cannot bypass single-shot cooldown");
            Check(c.Career.HuntedAnimal(animal.Id)!=null && !animal.Hitbox.enabled,"dead animal stops movement and shooting collider");
            var saved=FishingSaveStore.Load(c.SavePath,out var warning);Check(warning.Length==0 && saved.HuntedAnimal(animal.Id)!=null,"kill persisted to isolated save on disk");
            Check(!c.RifleHunting.CollectAnimal(animal.Id),"loot cannot be collected remotely");
            var corpse=animal.Feet;
            Check(c.Shore.Ground(corpse+Vector3.back*.45f,out ground),"corpse approach ground");
            Feet(c,ground+Vector3.up*.07f,animal.Root.GetComponent<Renderer>().bounds.center);
            for(int i=0;i<35;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}
            int meat=c.Career.Backpack.Count(FieldItem.RabbitMeat),fur=c.Career.Backpack.Count(FieldItem.RabbitFur);
            Debug.Log("[HUNTING] LOOT PREFLIGHT: "+c.InspectWorldInteractionStart());
            if(!c.InspectWorldInteractionStart().CanBegin)
            {
                Check(!c.RequestWorldInteraction(),"New smaller corpse cannot be collected beyond actual arm reach");
                Check(c.Shore.Ground(corpse+Vector3.back*.20f,out ground),"Closer ground exists beside the smaller supplied rabbit");
                Feet(c,ground+Vector3.up*.07f,animal.Root.GetComponent<Renderer>().bounds.center);
                for(int i=0;i<35;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}
            }
            Check(c.RequestWorldInteraction(),"E starts corpse pickup through contact action");
            for(int i=0;i<180 && c.WorldActionBusy;i++)yield return null;
            Check(c.Career.Backpack.Count(FieldItem.RabbitMeat)==meat+1 && c.Career.Backpack.Count(FieldItem.RabbitFur)==fur+1,"corpse contact grants one meat and one fur");
            Check(!c.RifleHunting.CollectAnimal(animal.Id) && !animal.Root.gameObject.activeSelf,"claimed corpse disappears and never grants twice");
            saved=FishingSaveStore.Load(c.SavePath,out warning);Check(saved.HuntedAnimal(animal.Id).collected,"loot receipt persisted on disk");
            c.Shore.SetCrouch(false);for(int i=0;i<25;i++)yield return null;
            c.Ecology.enabled=false;
            var gull=c.RifleHunting.Targets.First(a=>a.Kind==LandAnimalKind.Gull);
            gull.Root.gameObject.SetActive(true);
            bool clearAir=false;Vector3 air=Vector3.zero;
            for(int angle=0;angle<360;angle+=30)
            {
                var candidate=c.Shore.Character.EyePosition+Quaternion.Euler(0,angle,0)*Vector3.forward*6+Vector3.up*3;
                if(!c.Shore.ClearSight(c.Shore.Character.EyePosition,candidate) || Physics.CheckSphere(candidate,.7f,~(1<<4),QueryTriggerInteraction.Ignore))continue;
                air=candidate;clearAir=true;break;
            }
            Check(clearAir,"gull shot fixture has genuinely clear air above the current coastline");
            gull.Root.SetPositionAndRotation(air,Quaternion.identity);Physics.SyncTransforms();
            Feet(c,c.Shore.Position,gull.Hitbox.bounds.center);for(int i=0;i<10;i++)yield return null;c.PresentCharacterFrame();
            Check(gull.Hitbox.enabled && !gull.Dead,"existing ecological gull is registered as a live target");
            var gullWall=GameObject.CreatePrimitive(PrimitiveType.Cube);gullWall.name="Gull wall validation";
            gullWall.transform.position=Vector3.Lerp(c.viewCamera.transform.position,gull.Hitbox.bounds.center,.6f);gullWall.transform.localScale=Vector3.one*2;Physics.SyncTransforms();
            c.RifleHunting.TryFire();Check(!gull.Dead,"wall blocks gull as well as terrestrial animals");UnityEngine.Object.Destroy(gullWall);yield return null;
            until=Time.time+FishingRifleHunting.ShotInterval+.05f;while(Time.time<until)yield return null;
            c.PresentCharacterFrame();bool gullShot=c.RifleHunting.TryFire();
            Debug.Log("[HUNTING] GULL SHOT fired="+gullShot+" dead="+gull.Dead+" reason="+c.RifleHunting.ObstructionReason+" message="+c.Message+" eye="+c.viewCamera.transform.position+" aim="+c.viewCamera.transform.forward+" target="+gull.Hitbox.bounds.center);
            Check(gullShot && gull.Dead,"one real muzzle hit downs the original gull");
            Check(c.Shore.Ground(new Vector3(gull.Feet.x,0,gull.Feet.z),out var gullGround) && Mathf.Abs(gullGround.y-gull.Feet.y)<.05f,"gull remains lie on recoverable dry ground");
            c.Ecology.enabled=true;var resting=gull.Root.position;for(int i=0;i<20;i++)yield return null;
            Check(Vector3.Distance(resting,gull.Root.position)<.001f && gull.Root.gameObject.activeSelf,"ecology flight and visibility updates do not revive the corpse");
            var props=new MaterialPropertyBlock();gull.Root.GetComponent<Renderer>().GetPropertyBlock(props);
            Check(props.GetVector("_FinMotion").sqrMagnitude==0,"dead gull stops shader wing flapping");
            Check(c.Shore.Ground(gull.Feet+Vector3.back*.5f,out ground),"gull remains approach exists");Feet(c,ground+Vector3.up*.06f,gull.Root.GetComponent<Renderer>().bounds.center);
            for(int i=0;i<25;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}
            Capture(c,"gull-remains");
            var collectCarcass=typeof(FishingXRNotebook).GetMethod("CollectCarcass",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var contactPalm=FishingContactGeometry.ClosestPoint(gull.Root,gull.Root.GetComponent<Renderer>().bounds.center+Vector3.up*10)+Vector3.up*.08f;
            Check(!(bool)collectCarcass.Invoke(c.XRNotebook,new object[]{contactPalm+Vector3.up}),"XR carcass pickup rejects a distant palm without consuming remains");
            typeof(FishingXRNotebook).GetProperty("IsOpen").SetValue(c.XRNotebook,true);
            try{Check(!(bool)collectCarcass.Invoke(c.XRNotebook,new object[]{contactPalm}),"XR carcass pickup cannot leak through an open menu");}
            finally{c.XRNotebook.Close();}
            Check((bool)collectCarcass.Invoke(c.XRNotebook,new object[]{contactPalm}) && c.Career.Backpack.Count(FieldItem.GullMeat)==1 && c.Career.Backpack.Count(FieldItem.GullFeather)==1,"XR near-palm corpse contact yields separately named meat and feathers once");
            Check(!(bool)collectCarcass.Invoke(c.XRNotebook,new object[]{contactPalm}) && c.Career.Backpack.Count(FieldItem.GullMeat)==1,"XR corpse contact cannot grant the same loot twice");
            saved=FishingSaveStore.Load(c.SavePath,out warning);Check(saved.HuntedAnimal(gull.Id).collected && saved.Backpack.Count(FieldItem.GullFeather)==1,"gull corpse and feather claim survive disk reload");
            Check(!gull.Root.gameObject.activeSelf && !gull.Hitbox.enabled,"pickup immediately clears corpse visual and hit collider");
            double remaining=c.Career.HuntedAnimal(gull.Id).respawnSeconds;
            Check(remaining>299 && remaining<=FishingAnimalLoot.RespawnSeconds,"respawn countdown starts at pickup rather than at death");
            c.Career.Calendar.RestUntilMorning();c.Career.TickHuntedRespawns(1,true);c.RifleHunting.RefreshAnimals();
            Check(!gull.Root.gameObject.activeSelf && c.Career.HuntedAnimal(gull.Id).respawnSeconds==remaining,"sleep and pause do not respawn the collected gull");
            for(int i=0;i<299;i++)c.Career.TickHuntedRespawns(1);c.RifleHunting.RefreshAnimals();
            Check(!gull.Root.gameObject.activeSelf,"gull is not recycled early");
            c.Career.TickHuntedRespawns(1);c.RifleHunting.RefreshAnimals();
            Check(gull.Root.gameObject.activeSelf && gull.Hitbox.enabled && !gull.Dead && !c.RifleHunting.CollectAnimal(gull.Id),"new living gull returns without stale corpse loot");
            gull.Root.GetComponent<Renderer>().GetPropertyBlock(props);
            Check(props.GetVector("_FinMotion").sqrMagnitude>0,"respawned gull restores wing animation");
            FishingSaveStore.Save(c.SavePath,c.Career);saved=FishingSaveStore.Load(c.SavePath,out warning);
            Check(warning=="" && saved.HuntedAnimal(gull.Id)==null,"finished respawn persists without reviving old receipts");
            ValidateSuspendedRecovery(c,gull);
            c.FieldWorld.enabled=true;
            ValidateNotebookIcons(c);
        }
        private static void ValidateSuspendedRecovery(FishingPrototype c,FishingFieldWorld.Animal animal)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"suspend recovery test only touches an isolated save");
            var original=c.Career;var careerProperty=typeof(FishingPrototype).GetProperty("Career");
            var automatedProperty=typeof(FishingPrototype).GetProperty("Automated");
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            try
            {
                Check(FishingCareer.TryRestore(original.Snapshot(),out var fixture),"suspend recovery fixture restores current career");
                careerProperty.SetValue(c,fixture);
                Check(fixture.KillLandAnimal(animal.Id,animal.Kind,animal.Feet.x,animal.Feet.y,animal.Feet.z,0)
                    && fixture.CollectHuntedAnimal(animal.Id),"suspend fixture starts a new claimed corpse timer");
                for(int i=0;i<250;i++)fixture.TickHuntedRespawns(1);
                automatedProperty.SetValue(c,false);
                typeof(FishingPrototype).GetMethod("OnApplicationFocus",flags).Invoke(c,new object[]{false});
                var saved=FishingSaveStore.Load(c.SavePath,out var warning);
                Check(warning=="" && saved.HuntedAnimal(animal.Id).respawnSeconds==50,"focus loss saves the remaining 50 seconds without another transaction");
            }
            finally
            {
                automatedProperty.SetValue(c,true);careerProperty.SetValue(c,original);
                FishingSaveStore.Save(c.SavePath,original);c.RifleHunting.RefreshAnimals();
            }
        }
        private static void ValidateNotebookIcons(FishingPrototype c)
        {
            // Exercise the real notebook UI without claiming headset tracking coverage.
            var notebook=c.XRNotebook;
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var page=typeof(FishingXRNotebook).GetField("_page",flags);
            var offset=typeof(FishingXRNotebook).GetField("_offset",flags);
            var refresh=typeof(FishingXRNotebook).GetMethod("Refresh",flags);
            var icons=(UnityEngine.UI.RawImage[])typeof(FishingXRNotebook).GetField("_icons",flags).GetValue(notebook);
            try
            {
                foreach(int number in new[]{1,2})
                {
                    page.SetValue(notebook,number);
                    bool basics=(bool)typeof(FishingXRNotebook).GetField("_craftBasics",flags).GetValue(notebook);
                    var items=number==1?c.Career.Backpack.Snapshot().Select(s=>s.item).ToArray():FishingRecipes.ForStation(c.WorkshopWorld.Context,basics).Select(r=>r.Output).ToArray();
                    for(int first=0;first<items.Length;first+=6)
                    {
                        offset.SetValue(notebook,first);refresh.Invoke(notebook,null);
                        for(int i=0;i<6 && first+i<items.Length;i++)
                        {
                            var item=items[first+i];if(item==FieldItem.Empty)continue;
                            Check(icons[i].gameObject.activeSelf && icons[i].texture==(FishingV023Art.Icon(item) ?? FishingItemIcons.Get(item)),"XR notebook page "+number+" displays actual item icon "+item);
                        }
                    }
                }
            }
            finally { page.SetValue(notebook,0);offset.SetValue(notebook,0);notebook.Close(); }
        }
        private static void ValidateIcons()
        {
            var items=Enum.GetValues(typeof(FieldItem)).Cast<FieldItem>().Where(i=>i!=FieldItem.Empty).ToArray();
            var sheet=new Texture2D(8*144,((items.Length+7)/8)*144,TextureFormat.RGB24,false);
            var background=Enumerable.Repeat(new Color(.035f,.07f,.095f),sheet.width*sheet.height).ToArray();sheet.SetPixels(background);
            var hashes=new System.Collections.Generic.HashSet<int>();
            for(int n=0;n<items.Length;n++)
            {
                var source=FishingV023Art.Icon(items[n]);Check(source!=null,"runtime item icon exists for "+FishingBackpack.Name(items[n]));
                var icon=new Texture2D(128,128,TextureFormat.RGBA32,false);var active=RenderTexture.active;var target=RenderTexture.GetTemporary(128,128,0,RenderTextureFormat.ARGB32);
                try{Graphics.Blit(source,target);RenderTexture.active=target;icon.ReadPixels(new Rect(0,0,128,128),0,0);icon.Apply();}
                finally{RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);}
                var pixels=icon.GetPixels();Check(pixels.Count(p=>p.a>.1f)>80,"nonempty drawn icon "+items[n]);
                int hash=17;foreach(var pixel in icon.GetPixels32())unchecked{hash=hash*31+pixel.GetHashCode();}hashes.Add(hash);
                var composed=pixels.Select(p=>Color.Lerp(new Color(.035f,.07f,.095f),p,p.a)).ToArray();
                sheet.SetPixels(n%8*144+8,sheet.height-(n/8+1)*144+8,128,128,composed);
                UnityEngine.Object.DestroyImmediate(icon);
            }
            Check(hashes.Count>=items.Length-3,"icons use distinct recognizable silhouettes rather than a text fallback");
            Directory.CreateDirectory("Logs/HuntingLoot");sheet.Apply();File.WriteAllBytes("Logs/HuntingLoot/icon-sheet.png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);
        }
    }
}
