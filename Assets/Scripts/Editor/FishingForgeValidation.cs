using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public static class FishingForgeValidation
    {
        public static void Run()=>FishingV011Validation.Begin("FORGE");
        private static void Check(bool ok,string label){if(!ok)throw new Exception(label);Debug.Log("[FORGE] CHECK: "+label);}
        private static void Feet(FishingPrototype c,Vector3 position,Vector3 target)
        {
            c.Shore.Body.enabled=false;c.Shore.Actor.SetPositionAndRotation(position,Quaternion.LookRotation(Vector3.ProjectOnPlane(target-position,Vector3.up)));
            c.Shore.Body.enabled=true;Physics.SyncTransforms();c.View.ResetLook();c.PresentCharacterFrame();
            var delta=target-c.Shore.Character.EyePosition;float pitch=Mathf.Atan2(-delta.y,Vector3.ProjectOnPlane(delta,Vector3.up).magnitude)*Mathf.Rad2Deg;
            c.View.ApplyShoreLookDelta(new Vector2(0,-pitch/.12f));
        }
        private static void Capture(FishingPrototype c,string file)
        {
            Directory.CreateDirectory("Logs/Forge");var camera=c.viewCamera;var old=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
            var rt=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.aspect=1280f/720;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/Forge/"+file+".png",image.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(image);}
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            var world=c.WorkshopWorld;var bag=c.Career.Backpack;
            var forge=world.State.Stations.Single(s=>s.kind==CraftStation.Forge && s.permanent);
            var root=world.StationRoot(forge.id);
            Check(root!=null && root.GetComponent<Collider>().enabled,"one public forge is installed on safe shore footing");
            Check(c.Shore.Sites.All(s=>!s.Available || FishingPrototype.FlatDistance(root.position,s.Berth)>=6f),"forge stays clear of moored boat footprint");
            Check(world.State.Stations.All(s=>!s.permanent || s.id==forge.id || FishingPrototype.FlatDistance(root.position,new Vector3(s.x,s.y,s.z))>=3.5f),"forge does not steal the other public station interaction aprons");
            Check(root.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3<4000,"forge geometry stays within prototype budget");
            Check(FishingUIFont.Font.HasCharacter('锻') && FishingItemIcons.Get(FieldItem.Forge)!=null,"forge name and no-text icon are available");
            c.ReturnToHarbor();var rb=c.Player.engine.RB;rb.position=c.Shore.Sites[0].Berth+Vector3.up;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"land through production control");
            var node=world.Nodes.First(n=>n.Item==FieldItem.Stone);
            Check(c.Shore.Ground(node.Feet+Vector3.back*.30f,out var ground),"stone collection approach exists");
            c.Shore.SetCrouch(true);
            Feet(c,ground+Vector3.up*.06f,node.Root.position+Vector3.up*.2f);
            for(int i=0;i<35;i++){c.Shore.Move(Vector2.zero,false,false,true,.02f);c.PresentCharacterFrame();yield return null;}
            int stones=bag.Count(FieldItem.Stone);
            Debug.Log("[FORGE] STONE PREFLIGHT: "+c.InspectWorldInteractionStart());
            Check(c.RequestWorldInteraction(),"E starts real stone collection");
            for(int i=0;i<180 && c.WorldActionBusy;i++)yield return null;
            Check(bag.Count(FieldItem.Stone)==stones+3,"ground stone collection grants forge material");yield return null;
            bag.TryAdd(FieldItem.Stick,2);bag.TryAdd(FieldItem.Fiber,2);
            Check(!world.Craft("forge-stone-pick"),"world shortcut cannot craft outside the open station UI");
            bool approach=false;
            for(int angle=0;angle<360;angle+=30)
            {
                var seed=root.position+Quaternion.Euler(0,angle,0)*Vector3.back*1.55f;
                if(c.Shore.Ground(seed,out ground) && c.Shore.Clearance(ground)){approach=true;break;}
            }
            Check(approach,"forge has a standing interaction apron");
            c.Shore.SetCrouch(false);Feet(c,ground+Vector3.up*.06f,root.position+Vector3.up*.9f);
            for(int i=0;i<35;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}
            Check(world.OpenStation(forge.id) && c.InventoryOpen && world.Context==CraftStation.Forge,"E station route opens the forge recipe page");
            Check(world.Craft("forge-stone-pick") && bag.Count(FieldItem.StonePickaxe)==1 && bag.Count(FieldItem.Stone)==stones,"forge consumes the collected stone and makes an equippable tool");
            bag.TryAdd(FieldItem.IronOre,2);bag.TryAdd(FieldItem.Coal,1);
            Check(world.Craft("forge-iron") && bag.Count(FieldItem.IronIngot)==1,"forge converts mined ore and coal to an ingot");
            Check(!world.Craft("forge-iron"),"repeat click without materials cannot duplicate ingots");
            c.viewCamera.transform.LookAt(root.position+Vector3.up*.85f);Capture(c,"forge-in-game");
            var loaded=FishingSaveStore.Load(c.SavePath,out var warning);
            Check(warning.Length==0 && loaded.Workshop.Stations.Count(s=>s.kind==CraftStation.Forge && s.permanent)==1 && loaded.Backpack.Count(FieldItem.StonePickaxe)==1,"forge and outputs persist to the isolated save");
            Check(!world.Remove(),"public forge is protected from dismantling");
            c.ToggleInventory();world.RefreshFromCareer();yield return null;
            Check(world.State.Stations.Count(s=>s.kind==CraftStation.Forge && s.permanent)==1,"reloading workshop state does not spawn a duplicate forge");
        }
    }
}
