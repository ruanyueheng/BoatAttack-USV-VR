using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingCombatValidation
    {
        public static void Run()=>FishingV011Validation.Begin("COMBAT");
        private static void Check(bool ok,string text)
        {if(!ok)throw new InvalidOperationException("[COMBAT] "+text);Debug.Log("[COMBAT] CHECK: "+text);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.Combat!=null,"Combat runs only in a temporary isolated career");
            if(c.StartMenuOpen)c.FinishTitleVoyage();
            c.CloseHomeChart(true);if(c.InventoryOpen)c.ToggleInventory();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();
            c.ReturnToHarbor();yield return null;
            Check(!c.Combat.TryInteract() && !c.Combat.InDangerZone,"Harbor is safe and cannot start a remote challenge");
            for(int i=0;i<8;i++)c.Career.CompleteShoreTask(i);c.Career.ClaimShoreRewards();
            Check(c.Career.BuyHuntingRifle(),"Fixture buys the real rifle through the domain transaction");
            for(int slot=0;slot<FishingBackpack.HotbarCount;slot++)if(c.Career.Backpack.At(slot).item==FieldItem.HuntingRifle)c.SelectFieldSlot(slot);
            var rb=c.Player.engine.RB;var berth=FishingCoastalPresentation.Berth(1);
            rb.position=berth+Vector3.up*.4f;rb.rotation=Quaternion.Euler(0,180,0);rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"The optional challenge island is reachable through real landing");
            var board=c.Combat.transform.Find("礁影试炼告示 · 自选挑战");Check(board!=null,"The challenge has a visible world interaction");
            var approach=board.position+Vector3.forward*1.8f;
            approach.y=0;
            Check(c.Shore.Ground(approach,out var floor) && c.Shore.Relocate(floor,Quaternion.Euler(0,180,0)),"The challenge board has a supported standing approach");
            c.View.ResetLook();for(int i=0;i<10;i++){c.PresentCharacterFrame();yield return null;}
            Check(c.Combat.NearbyHint!=null && c.RequestWorldInteraction() && c.Combat.Challenging,"E starts the optional encounter at its physical board");
            c.Combat.enabled=false;
            BoxCollider boss=null;
            foreach(var hitbox in c.Combat.GetComponentsInChildren<BoxCollider>())if(c.Combat.IsTarget(hitbox) && hitbox.gameObject.name=="礁蟹守卫")boss=hitbox;
            Check(boss!=null,"The rendered guardian exposes the same physical target used by the rifle");
            var firingPoint=c.Combat.Arena+new Vector3(5,0,10);
            Check(c.Shore.Ground(firingPoint,out var firingFloor) && c.Shore.Relocate(firingFloor,Quaternion.LookRotation(new Vector3(-5,0,-9))),"The firing position clears the solid challenge board");
            c.View.ResetLook();c.PresentCharacterFrame();
            FishingV036FarmValidation.Aim(c,boss.bounds.center);
            c.RifleHunting.ReadDesktopInput(false,true);
            for(int i=0;i<20;i++){c.RifleHunting.ReadDesktopInput(false,true);c.PresentCharacterFrame();yield return null;}
            Check(c.RifleHunting.Aiming && c.RifleHunting.AimBlend>.8f,"Held aim smoothly enters the rifle sight view");
            Check(!c.FirstPerson || c.viewCamera.fieldOfView<c.FieldOfView*.9f,"First-person aiming narrows the world field of view");
            Capture(c,"guardian-aim");
            int before=c.RifleHunting.ShotsFired;
            int bossBefore=c.Combat.GuardianHealth;
            float until=Time.time+.75f;
            while(Time.time<until){c.RifleHunting.ReadDesktopInput(true,true);c.PresentCharacterFrame();yield return null;}
            Debug.Log("[COMBAT] FIRE: shots="+(c.RifleHunting.ShotsFired-before)+" aiming="+c.RifleHunting.Aiming+" presented="+c.RifleHunting.Presented+" obstruction="+c.RifleHunting.ObstructionReason+" hands="+c.Hands.CanPresentTools+" equipped="+c.Career.Backpack.Equipped+" swimming="+c.Shore.Swimming+" busy="+c.WorldActionBlocksGameplay);
            Check(c.RifleHunting.ShotsFired>=before+4,"A held trigger fires a continuous burst rather than one shot");
            Check(c.Combat.GuardianHealth<bossBefore,"Actual muzzle raycasts from the held firing burst damage the physical guardian");
            c.RifleHunting.ReadDesktopInput(false,false);
            c.TogglePause();int paused=c.RifleHunting.ShotsFired;
            c.RifleHunting.ReadDesktopInput(true,true);
            Check(!c.RifleHunting.TryFire() && c.RifleHunting.ShotsFired==paused,"Pause blocks both held fire and direct shooting");
            c.TogglePause();yield return null;
            var eye=c.Shore.Character.EyePosition;var contact=boss.bounds.center;
            Check(!c.Combat.Hit(boss,0,eye,contact),"Invalid damage cannot modify encounter health");
            for(int i=0;i<18;i++)c.Combat.Hit(boss,25,eye,contact);
            Check(!c.Combat.Challenging && c.Career.GuardianWins==1 && c.Career.Backpack.Count(FieldItem.GuardianPearl)==1,"Defeating the physical guardian grants its unique first-win material");
            int coins=c.Career.Coins;
            Check(!c.Combat.Hit(boss,25,eye,contact) && c.Career.Coins==coins && !c.Career.ClaimGuardianReward(),"A defeated target and repeated claims cannot duplicate rewards");
            var saved=FishingSaveStore.Load(c.SavePath,out _);
            Check(saved.GuardianWins==1 && saved.Backpack.Count(FieldItem.GuardianPearl)==1,"Actual JSON save and reload preserve the encounter receipt");
            c.Combat.enabled=true;c.Career.Calendar.AdvanceToHour(21);
            for(int i=0;i<3;i++)yield return null;
            BoxCollider night=null;
            foreach(var hitbox in c.Combat.GetComponentsInChildren<BoxCollider>())if(c.Combat.IsTarget(hitbox) && hitbox.gameObject.name=="夜行礁蟹")night=hitbox;
            Check(night!=null && c.Combat.InDangerZone,"Night creatures appear on the marked optional reef");
            var nearNight=night.transform.position+Vector3.forward*3;
            nearNight.y=0;
            Check(c.Shore.Ground(nearNight,out var nightFloor) && c.Shore.Relocate(nightFloor,Quaternion.Euler(0,180,0)),"A real night encounter has a supported approach");
            var rings=c.Combat.GetComponentsInChildren<LineRenderer>();LineRenderer warning=null;
            float warningDeadline=Time.time+5;
            while(warning==null && Time.time<warningDeadline)
            {foreach(var ring in rings)if(ring.name=="蓄力危险圈" && ring.enabled)warning=ring;yield return null;}
            Check(warning!=null,"A live night enemy visibly telegraphs its attack");
            c.Career.Calendar.AdvanceToHour(12);yield return null;yield return null;
            Debug.Log("[COMBAT] NIGHT: hour="+c.Career.Calendar.Hour+" paused="+c.Paused+" ring="+warning.enabled+" night="+night.gameObject.activeSelf+" challenging="+c.Combat.Challenging+" health="+c.Combat.Health+" zone="+c.Combat.InDangerZone);
            Check(!warning.enabled && !night.gameObject.activeSelf,"Daylight hides both the night creature and its independent warning ring");
            c.Career.Calendar.AdvanceToHour(21);yield return null;yield return null;
            Check(c.Combat.Hit(night,75,c.Shore.Character.EyePosition,night.bounds.center) && !c.Career.CanChallengeNight,"Defeating a real night target grants material and starts its active-time cooldown");
            for(int i=0;i<3;i++)yield return null;
            Check(!c.Combat.IsTarget(night) && c.Career.Snapshot().combat.nightDefeats==1,"The night target cannot immediately respawn or pay twice during cooldown");
            c.ReturnToHarbor();yield return null;
            Check(!c.Combat.Challenging && !c.Combat.InDangerZone,"Returning to harbor safely ends optional combat");
            Capture(c,"combat-harbor-safe");
        }
        internal static void Capture(FishingPrototype c,string name)
        {
            Directory.CreateDirectory("Logs/Combat");var camera=c.viewCamera;var old=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
            var rt=RenderTexture.GetTemporary(1600,900,24);var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.aspect=1600f/900;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("Logs/Combat/"+name+".png",image.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(image);}
        }
    }
}
