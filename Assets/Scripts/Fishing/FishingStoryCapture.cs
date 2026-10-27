using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>[Claude v0.41] 仅命令行开启：逐段播放全部剧情过场并截图（临时存档），用于审阅 CG 效果。</summary>
    public sealed class FishingStoryCapture : MonoBehaviour
    {
        public const string EnableFlag="-fishing-story-capture";
        private string _output;
        public static void Attach(FishingPrototype c)
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,EnableFlag);
            if(i<0 || i+1>=args.Length || !Path.IsPathRooted(args[i+1]))return;
            var capture=c.gameObject.AddComponent<FishingStoryCapture>();capture._output=args[i+1];capture.StartCoroutine(capture.Run(c));
        }
        private IEnumerator Run(FishingPrototype c)
        {
            Directory.CreateDirectory(_output);
            yield return new WaitForSecondsRealtime(8);
            if(c.StartMenuOpen)c.SendMessage("CloseStartMenuForCapture",SendMessageOptions.DontRequireReceiver);
            foreach(FishingPrologue.Sequence sequence in Enum.GetValues(typeof(FishingPrologue.Sequence)))
            {
                if(c.Acceptance!=null)
                {
                    var scenes=new[]{FishingAcceptanceCase.FreePlay,FishingAcceptanceCase.HomeInterior,FishingAcceptanceCase.WindSanctuary,FishingAcceptanceCase.ForestSanctuary,FishingAcceptanceCase.WreckSanctuary,FishingAcceptanceCase.StarSanctuary,FishingAcceptanceCase.ArchIslet};
                    c.Acceptance.SelectCase(scenes[(int)sequence]);
                    float deadline=Time.realtimeSinceStartup+20;
                    while(c.AcceptanceFixturePreparing || c.Realms.Transitioning)
                    {if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Story fixture setup: "+sequence);yield return null;}
                    if(sequence==FishingPrologue.Sequence.Finale)
                    {
                        c.Career.AcceptMayorship();c.Career.DiscoverChart();
                        for(int island=0;island<4;island++){c.Career.CompleteShrine(island);if(!c.Career.ClaimShrineReward(island))throw new InvalidOperationException("Story reward fixture rejected");}
                        if(!c.Career.CraftVoyageCompass(CraftStation.Workbench) || c.Career.OpenGrandTreasure()==0)throw new InvalidOperationException("Story treasure fixture rejected");
                    }
                    yield return new WaitForSecondsRealtime(.8f);
                }
                if(!c.Prologue.BeginForCapture(sequence)){Debug.LogError("[STORY-CAPTURE] cannot begin "+sequence);continue;}
                int shots=Mathf.RoundToInt(c.Prologue.Duration/FishingPrologue.ShotSeconds);
                for(int shot=0;shot<shots;shot++)
                {
                    c.Prologue.SeekForCapture(shot*FishingPrologue.ShotSeconds+4.6f);
                    yield return null;yield return null;yield return new WaitForEndOfFrame();
                    var file=Path.Combine(_output,$"{(int)sequence}-{sequence}-{shot}.png");
                    var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(file,texture.EncodeToPNG());Destroy(texture);
                    Debug.Log("[STORY-CAPTURE] "+file);
                }
                c.Prologue.Finish();yield return null;
            }
            Debug.Log("[STORY-CAPTURE] PASS");
            Application.Quit(0);
        }
    }
}
