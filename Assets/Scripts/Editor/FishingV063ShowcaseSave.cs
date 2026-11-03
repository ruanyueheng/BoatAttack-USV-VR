using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>Prepare an explicitly supplied local save copy; never writes to the source or a device.</summary>
    public static class FishingV063ShowcaseSave
    {
        [Serializable] private sealed class AnimalSummary
        {
            public string kind;
            public int beforeHoused,afterHoused,adultMale,adultFemale,habitatLevel;
        }
        [Serializable] private sealed class Manifest
        {
            public string preparedUtc,sourcePath,sourceSha256,backupPath,backupSha256,outputPath,outputSha256;
            public string originalSavedUtc,runId;
            public int existingAnimals,addedAnimals;
            public bool sourceUnchanged,onlyHomeEcologyChanged,existingAnimalsUnchanged,slotHeaderAndResumeUnchanged,readbackValidated;
            public List<AnimalSummary> animals=new List<AnimalSummary>();
        }
        private static void Require(bool ok,string message)
        {if(!ok)throw new InvalidOperationException("[V063-SHOWCASE-SAVE] "+message);}

        public static void Prepare()
        {
            const string flag="-fishing-pico-save-source";
            var args=Environment.GetCommandLineArgs();string source=null;
            for(int i=0;i<args.Length;i++)if(args[i]==flag)
            {
                Require(source==null && i+1<args.Length && !args[i+1].StartsWith("-",StringComparison.Ordinal),"Provide exactly one explicit source path after "+flag);
                source=args[++i];
            }
            Require(!string.IsNullOrWhiteSpace(source) && Path.IsPathRooted(source),"An absolute local source path is required; no player save is inferred.");
            source=Path.GetFullPath(source);Require(File.Exists(source),"The supplied local source file does not exist: "+source);
            var original=File.ReadAllBytes(source);string originalHash=Hash(original);
            var slot=ReadSlot(Encoding.UTF8.GetString(original).TrimStart('\uFEFF'));
            string originalDomain=WithoutEcology(slot.career),originalResume=JsonUtility.ToJson(slot.resume);
            string originalRunId=slot.runId,originalSavedUtc=slot.savedUtc;int originalFormat=slot.format;
            var originalEcology=slot.career.homeEcology.Copy();
            Require(FishingCareer.TryRestore(slot.career,out var career),"The source career failed strict production restore validation.");
            string restoredDomain=WithoutEcology(career.Snapshot());
            career.SetCreativeMode(true);
            try
            {
                Require(career.PrepareAnimalShowcase(),"Cannot prepare all five adult pairs without removing existing animals: total "+career.HomeAnimalCount+
                    "/"+FishingCareer.MaxHomeAnimals+", habitat levels "+string.Join(",",originalEcology.habitatLevels)+
                    ". Existing animals and capacity/blueprint rules are retained.");
            }
            finally{career.SetCreativeMode(false);}
            var prepared=career.Snapshot();
            Require(!career.CreativeMode && WithoutEcology(prepared)==restoredDomain,"Preparation unexpectedly changed a domain outside homeEcology.");
            Require(FishingCareer.TryRestore(prepared,out _),"The prepared career failed strict production restore validation.");
            foreach(var animal in originalEcology.animals)
            {
                var retained=prepared.homeEcology.animals.Find(a=>a.id==animal.id);
                Require(retained!=null && JsonUtility.ToJson(retained)==JsonUtility.ToJson(animal),"Existing animal #"+animal.id+" changed or disappeared.");
            }
            var manifest=new Manifest{preparedUtc=DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture),sourcePath=source,sourceSha256=originalHash,
                originalSavedUtc=originalSavedUtc,runId=originalRunId,existingAnimals=originalEcology.animals.Count,
                addedAnimals=prepared.homeEcology.animals.Count-originalEcology.animals.Count};
            for(int kind=0;kind<5;kind++)
            {
                var summary=new AnimalSummary{kind=((LandAnimalKind)kind).ToString(),habitatLevel=prepared.homeEcology.habitatLevels[kind]};
                foreach(var animal in originalEcology.animals)if(animal.housed && (int)animal.kind==kind)summary.beforeHoused++;
                foreach(var animal in prepared.homeEcology.animals)if(animal.housed && (int)animal.kind==kind)
                {
                    summary.afterHoused++;
                    if(career.Calendar.Day-animal.birthDay>=FishingCareer.AnimalAdultDays)
                    {if(animal.female)summary.adultFemale++;else summary.adultMale++;}
                }
                Require(summary.afterHoused>=2 && summary.adultMale>=1 && summary.adultFemale>=1,
                    "Each habitat requires at least two housed animals including an adult male and female: "+summary.kind);
                manifest.animals.Add(summary);
            }
            // Preserve the supplied career envelope exactly; only transplant the verified ecology.
            slot.career.homeEcology=prepared.homeEcology;
            Require(WithoutEcology(slot.career)==originalDomain,"The output changed original serialized career data outside homeEcology.");
            Require(slot.format==originalFormat && slot.runId==originalRunId && slot.savedUtc==originalSavedUtc
                && JsonUtility.ToJson(slot.resume)==originalResume,"The original slot header or resume changed.");
            var outputBytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(slot,true));
            var roundtrip=ReadSlot(Encoding.UTF8.GetString(outputBytes));
            Require(FishingCareer.TryRestore(roundtrip.career,out var loaded),"The serialized output failed strict production restore validation.");
            Require(JsonUtility.ToJson(loaded.HomeEcology)==JsonUtility.ToJson(prepared.homeEcology),"Output ecology changed during the save round trip.");
            Require(WithoutEcology(roundtrip.career)==originalDomain && roundtrip.runId==originalRunId && roundtrip.savedUtc==originalSavedUtc
                && roundtrip.format==originalFormat && JsonUtility.ToJson(roundtrip.resume)==originalResume,"The serialized output changed preserved data.");
            Require(Hash(File.ReadAllBytes(source))==originalHash,"The source changed during preparation; no output was committed.");
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/V063/PicoAnimalShowcase",
                DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,8)));
            Directory.CreateDirectory(directory);
            manifest.backupPath=Path.Combine(directory,Path.GetFileName(source)+".original");
            manifest.outputPath=Path.Combine(directory,Path.GetFileName(source));
            WriteNew(manifest.backupPath,original);WriteNew(manifest.outputPath,outputBytes);
            manifest.backupSha256=Hash(File.ReadAllBytes(manifest.backupPath));manifest.outputSha256=Hash(File.ReadAllBytes(manifest.outputPath));
            Require(manifest.backupSha256==originalHash && manifest.outputSha256==Hash(outputBytes),"Output or original-backup integrity verification failed.");
            ReadSlot(File.ReadAllText(manifest.outputPath));
            manifest.sourceUnchanged=Hash(File.ReadAllBytes(source))==originalHash;
            Require(manifest.sourceUnchanged,"The source changed before manifest finalization.");
            manifest.onlyHomeEcologyChanged=manifest.existingAnimalsUnchanged=manifest.slotHeaderAndResumeUnchanged=manifest.readbackValidated=true;
            WriteNew(Path.Combine(directory,"manifest.json"),Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest,true)));
            Debug.Log("[V063-SHOWCASE-SAVE] PASS added="+manifest.addedAnimals+"; source retained; OUTPUT="+manifest.outputPath);
        }
        private static FishingSlotData ReadSlot(string json)
        {
            var slot=JsonUtility.FromJson<FishingSlotData>(json);
            Require(slot!=null && slot.format==1 && slot.career!=null && slot.career.homeEcology!=null,"The input is not a supported slot save with home ecology.");
            Require(!string.IsNullOrWhiteSpace(slot.runId) && slot.runId.Length<=128,"The slot runId is invalid.");
            Require(DateTime.TryParseExact(slot.savedUtc,"O",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var utc) && utc.Kind==DateTimeKind.Utc,"The slot savedUtc is not valid UTC.");
            Require(FishingSlotStore.IsValidResume(slot.resume,out string error),"Invalid resume: "+error);
            Require(FishingCareer.TryRestore(slot.career,out _),"The slot career failed strict production restore validation.");
            return slot;
        }
        private static string WithoutEcology(FishingSaveData data)
        {
            var ecology=data.homeEcology;
            try{data.homeEcology=null;return JsonUtility.ToJson(data);}
            finally{data.homeEcology=ecology;}
        }
        private static string Hash(byte[] bytes)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",string.Empty).ToLowerInvariant();}
        private static void WriteNew(string path,byte[] bytes)
        {using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))file.Write(bytes,0,bytes.Length);}
    }
}
