using System;
using System.Collections;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV033SettingsValidation
    {
        private static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);Debug.Log("[V033-SETTINGS] CHECK: "+name);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.HasIsolatedProfile,"Settings checks use an isolated profile and do not write user preferences");
            var music=c.gameObject.AddComponent<AudioSource>();var ambient=c.gameObject.AddComponent<AudioSource>();var effects=c.gameObject.AddComponent<AudioSource>();
            c.RegisterAudioSource(music,FishingAudioCategory.Music,.2f);c.RegisterAudioSource(ambient,FishingAudioCategory.Ambient,.3f);c.RegisterAudioSource(effects,FishingAudioCategory.Effects,.4f);
            try
            {
                c.SetSetting("Master",.35f);Check(Mathf.Abs(AudioListener.volume-.35f)<.001f,"Master volume reaches actual listener");
                c.SetSetting("Music",0);c.SetSetting("Ambient",.5f);c.SetSetting("Effects",.25f);
                Check(music.volume==0 && Mathf.Abs(ambient.volume-.15f)<.001f && Mathf.Abs(effects.volume-.1f)<.001f,"Music zero and separate ambient/effects gains are applied to real sources");
                c.ToggleQuiet();Check(AudioListener.volume==0 && music.mute && ambient.mute && effects.mute,"Quiet switch mutes all source classes immediately");
                c.ToggleQuiet();Check(Mathf.Abs(AudioListener.volume-.35f)<.001f && !music.mute,"Unmuting restores master without resetting music zero");
                var low=FishingViewRig.ScaleLookDelta(new Vector2(50,20),.25f,false);var high=FishingViewRig.ScaleLookDelta(new Vector2(50,20),2.5f,false);
                Check(Vector2.Distance(high,low*10)<.001f,"Mouse sensitivity scales actual pixel delta consistently by ten");
                var inverse=FishingViewRig.ScaleLookDelta(new Vector2(50,20),.25f,true);Check(inverse.x==low.x && inverse.y==-low.y,"Invert affects only vertical look");
                c.SetSetting("Sensitivity",float.NaN);Check(c.MouseSensitivity==1,"Invalid sensitivity rejected without changing saved state");
                if(c.Paused)c.TogglePause();c.ReturnToHarbor();yield return null;
                if(!c.FirstPerson)c.View.Toggle();c.View.ApplyView(true,true);
                c.View.ResetLook();c.SetSetting("Sensitivity",.25f);c.View.ApplyBoatLookDelta(new Vector2(40,10));c.View.ApplyView(true,true);
                var rotation=c.viewCamera.transform.rotation;c.View.ResetLook();c.SetSetting("Sensitivity",2.5f);c.View.ApplyBoatLookDelta(new Vector2(40,10));c.View.ApplyView(true,true);
                Check(Quaternion.Angle(rotation,c.viewCamera.transform.rotation)>8,"Boat camera rotation actually changes with sensitivity");
                c.OpenStartMenu();c.SetSetting("Music",0);c.ContinueFromMenu();
                Check(c.MusicVolume==0 && music.volume==0,"Menu-to-game settings do not reset music volume");
                c.SetSetting("Master",0);Check(AudioListener.volume==0,"Master zero is actual silence");
                c.RestoreAudioSettings();Check(Mathf.Abs(music.volume-1)<.001f && !music.mute,"Audio restoration returns borrowed source settings");c.ApplyAudioSettings();
                c.SetSetting("Quality",0);Check(QualitySettings.shadows==UnityEngine.ShadowQuality.Disable,"Quality button applies real shadow setting");
                c.SetSetting("Quality",2);c.SetSetting("FOV",82);c.View.ApplyView(true,true);Check(Mathf.Abs(c.viewCamera.fieldOfView-82)<.01f,"First-person FOV reaches camera projection");
            }
            finally
            {
                if(c.QuietMode)c.ToggleQuiet();foreach(var key in new[]{"Master","Music","Ambient","Effects","Sensitivity"})c.SetSetting(key,1);
                c.SetSetting("FOV",FishingViewRig.DesktopFieldOfView);c.SetSetting("Quality",2);
                UnityEngine.Object.Destroy(music);UnityEngine.Object.Destroy(ambient);UnityEngine.Object.Destroy(effects);
            }
        }
    }
}
