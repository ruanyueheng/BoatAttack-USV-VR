using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV043Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V043");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V043] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"Save tests only write an isolated profile");
            var legacy=new FishingCareer().Snapshot();legacy.version=18;legacy.coins=87;
            legacy.hunting.availableDays=new List<int>(new int[8]);legacy.hunting.availableMinutes=new List<double>(new double[8]);
            var slot=new FishingSlotData{career=legacy,resume=new FishingResumeData(),runId="v041-compatibility",savedUtc=DateTime.UtcNow.ToString("O")};
            string oldJson=JsonUtility.ToJson(slot,true),auditBase=c.SavePath+".compatibility";
            string audit=FishingSlotStore.PathFor(auditBase,0,FishingSlotKind.Manual);
            Directory.CreateDirectory(Path.GetDirectoryName(audit));File.WriteAllText(audit,oldJson);
            var info=FishingSlotStore.Read(auditBase,0,FishingSlotKind.Manual);
            Check(info.Valid && !info.FromBackup && info.Data.career.coins==87,"Production slot reader accepts an unchanged schema18 eight-entry save");
            Check(File.ReadAllText(audit)==oldJson,"Reading the old save never rewrites its source");
            Check(FishingCareer.TryRestore(info.Data.career,out var upgraded),"Old career migration succeeds");
            FishingSlotStore.Write(auditBase,0,FishingSlotKind.Manual,upgraded,slot.resume,slot.runId);
            Check(File.ReadAllText(audit+".bak")==oldJson && FishingSlotStore.Read(auditBase,0,FishingSlotKind.Manual).Data.career.hunting.availableDays.Count==FishingHunting.SpawnCount,"Saving upgrades the new primary and preserves the original backup");
            File.WriteAllText(audit,"{broken");
            info=FishingSlotStore.Read(auditBase,0,FishingSlotKind.Manual);
            Check(info.Valid && info.FromBackup && info.Data.career.version==18,"A genuine old backup still recovers when the primary is broken");

            // An explicit local backup directory permits auditing real receipts without opening the live profile.
            string archived=Environment.GetEnvironmentVariable("FISHING_SAVE_AUDIT_DIR");int audited=0;
            if(!string.IsNullOrEmpty(archived))
            {
                Check(Path.IsPathRooted(archived) && Directory.Exists(archived),"Explicit archived-save directory exists");
                foreach(string path in Directory.GetFiles(archived,"boatattack-fishing*"))
                {
                    string json=File.ReadAllText(path);
                    if(json.Contains("\"format\""))
                    {
                        File.WriteAllText(audit,json);info=FishingSlotStore.Read(auditBase,0,FishingSlotKind.Manual);
                        Check(info.Valid && !info.FromBackup,"Archived slot remains readable: "+Path.GetFileName(path));
                        if(info.Data.career.version==18)oldJson=json;
                    }
                    else Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(json),out _),"Archived legacy progress remains readable: "+Path.GetFileName(path));
                    Check(File.ReadAllText(path)==json,"Archived source stays unchanged: "+Path.GetFileName(path));audited++;
                }
                Check(audited>0,"Audited actual archived saves: "+audited);
            }
            var expected=JsonUtility.FromJson<FishingSlotData>(oldJson);
            c.OpenStartMenu();c.OpenSaveMenu("load");
            File.WriteAllText(FishingSlotStore.PathFor(c.SavePath,0,FishingSlotKind.Manual),oldJson);c.RefreshSaveSlots();
            c.RequestSlotAction(0,"load");if(c.SaveConfirmation.Length>0)c.ConfirmSaveAction();yield return null;
            Check(!c.StartMenuOpen && c.ActiveSaveSlot==0 && c.Career.Coins==expected.career.coins
                && c.Career.Calendar.Day==expected.career.calendar.day,"The real game load menu restores the old slot and its progress");
            Check(c.Career.Snapshot().hunting.availableDays.Count==FishingHunting.SpawnCount,"Loaded legacy wildlife is ready for current saves");
        }
    }
}
