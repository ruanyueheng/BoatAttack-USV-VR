using System;
using System.Collections;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV059Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V059");
        public static void RunHandcraft()=>FishingV011Validation.Begin("V059-HANDCRAFT");
        private static void Check(bool ok,string message)
        {if(!ok)throw new InvalidOperationException(message);Debug.Log("[V059] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);
            if(c.Paused)c.TogglePause();if(c.QuietMode)c.ToggleQuiet();
            yield return null;
            foreach(var run in new[]{AudioChecks(c),FishingV059SieveValidation.Checks(c),
                FishingV059WorkpieceValidation.Checks(c),FishingV059AnimalsValidation.Checks(c)})
            {
                try{while(run.MoveNext())yield return run.Current;}
                finally{(run as IDisposable)?.Dispose();}
            }
        }
        public static IEnumerator HandcraftChecks(FishingPrototype c)
        {
            foreach(var run in new[]{FishingV059SieveValidation.Checks(c),FishingV059WorkpieceValidation.Checks(c)})
            {try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}}
        }
        private static IEnumerator AudioChecks(FishingPrototype c)
        {
            var audio=c.WorldAudio;float effects=c.EffectsVolume,ambient=c.AmbientVolume;
            try
            {
                c.SetSetting("Effects",1);c.SetSetting("Ambient",1);
                audio.enabled=false;audio.enabled=true;yield return null;
                foreach(string cue in new[]{"SawCut","ForgeHammer","RabbitCall","FoxCall","TortoiseCall","SeaTurtleCall","GullCall"})
                {
                    var clip=FishingWorldAudio.Clip(cue);
                    Check(clip!=null && clip.length>.1f && clip.length<4,"Real short recorded asset is available: "+cue);
                    bool craft=cue=="SawCut" || cue=="ForgeHammer";
                    var at=c.ExplorerPosition+Vector3.up;
                    Check(audio.Play(cue,at,.9f,craft?FishingAudioCategory.Effects:FishingAudioCategory.Ambient),"Local cue plays: "+cue);
                    var source=audio.Sources.FirstOrDefault(s=>s.clip==clip && s.isPlaying);
                    Check(source!=null && source.spatialBlend==1 && Vector3.Distance(source.transform.position,at)<.001f
                        && source.volume>(craft?.49f:.33f),"Cue has audible local gain without a 2D/UI route: "+cue);
                    c.SetSetting(craft?"Effects":"Ambient",0);
                    Check(source.volume==0,"Existing channel setting silences the active cue: "+cue);
                    c.SetSetting(craft?"Effects":"Ambient",1);
                    audio.enabled=false;audio.enabled=true;
                    yield return null;
                }
                c.TogglePause();yield return null;
                Check(!audio.Play("ForgeHammer",c.ExplorerPosition) && !audio.Play("SawCut",c.ExplorerPosition)
                    && !audio.Play("RabbitCall",c.ExplorerPosition,1,FishingAudioCategory.Ambient),"Paused world blocks tools and animal calls");
                c.TogglePause();yield return null;
                c.ToggleQuiet();yield return null;
                Check(!audio.Play("SawCut",c.ExplorerPosition) && !audio.Play("FoxCall",c.ExplorerPosition,1,FishingAudioCategory.Ambient),"Quiet mode blocks tools and animal calls");
                c.ToggleQuiet();yield return null;
            }
            finally
            {
                if(c.Paused)c.TogglePause();if(c.QuietMode)c.ToggleQuiet();
                c.SetSetting("Effects",effects);c.SetSetting("Ambient",ambient);
                audio.enabled=false;audio.enabled=true;
            }
        }
    }
}
