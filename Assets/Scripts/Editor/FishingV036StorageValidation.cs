using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV036StorageValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V036-STORAGE");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V036-STORAGE] CHECK: "+label);}
        public static void DataChecks()
        {
            var bag=new FishingBackpack(false);var chest=new FishingBackpack(false);
            Check(bag.TryAdd(FieldItem.Shell,64) && chest.TryAdd(FieldItem.Shell,63),"Stack merge fixture");
            Check(bag.TransferTo(chest,0) && bag.Count(FieldItem.Shell)==0 && chest.Count(FieldItem.Shell)==127,"Deposit merges then uses the next empty slot without losing items");
            Check(chest.TransferTo(bag,0) && bag.Count(FieldItem.Shell)==64 && chest.Count(FieldItem.Shell)==63,"Withdraw uses the same conserved transaction");
            Check(!bag.TransferTo(bag,0) && !bag.TransferTo(null,0) && !bag.TransferTo(chest,-1) && !bag.TransferTo(chest,36),"Invalid transfers leave ownership unchanged");
            var full=new FishingBackpack(false);Check(full.TryAdd(FieldItem.HuntingRifle,36),"Full chest fixture");
            Check(!bag.TransferTo(full,0) && bag.Count(FieldItem.Shell)==64 && full.Count(FieldItem.HuntingRifle)==36,"A full destination keeps the complete source stack");
            var career=new FishingCareer();Check(career.Backpack.TryAdd(FieldItem.HuntingRifle,35) && career.HomeStorage.TryAdd(FieldItem.Shell,64),"Full backpack fixture");
            Check(!career.HomeStorage.TransferTo(career.Backpack,0) && career.HomeStorage.Count(FieldItem.Shell)==64,"A full backpack cannot delete a chest withdrawal");
            Check(career.Backpack.TransferTo(career.HomeStorage,1) && career.HomeStorage.TransferTo(career.Backpack,0),"Depositing a tool frees room to withdraw the stored stack");
            Check(career.CanHarvestHomeFruit(0) && !career.TryHarvestHomeFruit(0) && career.CanHarvestHomeFruit(0),"A full bag leaves fruit harvest uncommitted");
            Check(career.Backpack.TransferTo(career.HomeStorage,2) && career.TryHarvestHomeFruit(0) && !career.TryHarvestHomeFruit(0),"Ground pickup commits one fruit receipt per tree per day");
            string path=Path.Combine(Application.temporaryCachePath,"v036-storage-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                FishingSaveStore.Save(path,career);var restored=FishingSaveStore.Load(path,out var warning);
                Check(warning.Length==0 && restored.HomeStorage.Count(FieldItem.HuntingRifle)==2 && restored.Backpack.Count(FieldItem.Shell)==64,"Real JSON save preserves both sides of storage");
                Check(!restored.CanHarvestHomeFruit(0) && !restored.TryHarvestHomeFruit(0) && restored.Backpack.Count(FieldItem.IslandFruit)==1,"Reloading cannot duplicate a harvested fruit");
                restored.Calendar.RestUntilMorning();Check(restored.CanHarvestHomeFruit(0) && restored.TryHarvestHomeFruit(0),"A new calendar day renews the tree");
                var snapshot=career.Snapshot();snapshot.homeStorage[1].count=0;
                Check(career.HomeStorage.Count(FieldItem.HuntingRifle)==2,"Storage snapshots do not alias live stacks");
                snapshot=career.Snapshot();snapshot.homeFruitDays[0]=career.Calendar.Day+1;Check(!FishingCareer.TryRestore(snapshot,out _),"Future fruit receipts are rejected");
                snapshot=career.Snapshot();snapshot.homeStorage[1].count=65;Check(!FishingCareer.TryRestore(snapshot,out _),"Over-limit chest stacks are rejected");
                var legacy=new FishingCareer().Snapshot();legacy.version=14;legacy.homeStorage=null;legacy.homeFruitDays=null;
                Check(FishingCareer.TryRestore(legacy,out var upgraded) && upgraded.HomeStorage.UsedSlots==0 && upgraded.CanHarvestHomeFruit(0),"V14 saves upgrade with an empty chest and fresh orchard");
            }
            finally{foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);}
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            DataChecks();var original=c.Career.Snapshot();string output=Path.GetFullPath("Logs/V036/Storage");Directory.CreateDirectory(output);
            try
            {
                c.ReturnToHarbor();yield return null;c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                typeof(FishingPrototype).GetMethod("PrepareAcceptanceHome",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{FishingAcceptanceCase.HomeInterior});yield return null;
                Check(c.Home.StorageChest!=null && c.Home.InteractionRoot(6)==c.Home.StorageChest,"A real chest model is registered as a home interaction");
                Check(c.Shore.Relocate(c.Home.Origin+new Vector3(3.1f,.2f,-1.35f),Quaternion.Euler(0,90,0)),"Storage stance clears furniture and workshop stations");
                c.viewCamera.transform.LookAt(c.Home.InteractionPoint(6));yield return null;
                Check(c.Career.Backpack.TryAdd(FieldItem.IslandFruit,3),"Storage UI fixture owns fruit");
                Check(c.Home.InteractAt(6) && c.InventoryOpen,"Production E interaction opens the bidirectional storage panel");
                Check(c.Home.TransferStorage(true,1) && c.Career.HomeStorage.Count(FieldItem.IslandFruit)==3,"Production panel deposits the selected backpack stack");
                Check(c.Home.TransferStorage(false,0) && c.Career.Backpack.Count(FieldItem.IslandFruit)==3,"Production panel withdraws the selected chest stack");
                c.Home.TransferStorage(true,1);yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(output,"desktop-bidirectional-storage.png"));
                for(int frame=0;frame<6;frame++)yield return null;
                c.ToggleInventory();FishingV016Validation.Capture(c,output,"home-storage-chest");
            }
            finally{if(c.InventoryOpen)c.ToggleInventory();c.XRNotebook.Close();c.ReturnToHarbor();c.RestoreShopTestProfile(original);}
        }
    }
}
