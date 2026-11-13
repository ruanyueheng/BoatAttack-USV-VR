using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    /// <summary>实时剧情演出：每段在真实场景位置抽 5 帧拼成联系表（Logs/V044/story），检查字幕、光照还原与旧档解锁。</summary>
    public static class FishingV044StoryValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V044-STORY");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-STORY] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"Only the isolated automation profile is used");
            string evidence=Path.GetFullPath("Logs/V044/story");Directory.CreateDirectory(evidence);
            var p=c.Prologue;var lore=c.Career.Lore;
            // 旧档（v0.43 及更早）只存 chartFound/shrines/grandTreasureFound，解锁完全由这些既有字段推导，不新增存档字段。
            Check(p.Unlocked(FishingPrologue.Sequence.Prologue) && !p.Unlocked(FishingPrologue.Sequence.Finale) && p.Latest==FishingPrologue.Sequence.Prologue,"A fresh save can only replay the prologue");
            var sun=RenderSettings.sun;Check(sun!=null,"Scene has a main light for film moods");
            foreach(FishingPrologue.Sequence sequence in Enum.GetValues(typeof(FishingPrologue.Sequence)))
            {
                var stage=Stage(c,sequence);while(stage.MoveNext())yield return stage.Current;
                if(p.Open)p.Finish();
                Check(p.BeginForCapture(sequence),"Opens "+sequence);
                var subtitle=p.CinemaScreen.transform.Find("Subtitle").GetComponent<Text>();
                var frames=new Texture2D[5];
                for(int k=0;k<5;k++)
                {
                    // 在“镜头 × 前/后机位”中均匀抽 5 帧；按镜头比例采样，延长后仍涵盖两个机位。
                    int halves=Mathf.RoundToInt(p.Duration/FishingPrologue.ShotSeconds)*2;int h=Mathf.Min(halves-1,Mathf.RoundToInt(k*(halves-1)/4f));
                    int shot=h/2;float local=FishingPrologue.ShotSeconds*(h%2==0?.45f:.86f);
                    p.SeekForCapture(shot*FishingPrologue.ShotSeconds+local);yield return null;
                    Check(subtitle.cachedTextGenerator.lineCount<=2,"Caption fits two lines "+sequence+" shot "+shot);
                    var sunRotation=sun.transform.rotation;var sunColor=sun.color;var fog=RenderSettings.fogColor;
                    p.CinemaCamera.Render();
                    Check(sun.transform.rotation==sunRotation && sun.color==sunColor && RenderSettings.fogColor==fog,"Film mood restores the world light "+sequence);
                    string file=Path.Combine(evidence,(int)sequence+"-"+sequence+"-"+k+".png");
                    // 按实机 16:9 画布截取（批处理窗口比例不定）。
                    p.CinemaScreen.renderMode=RenderMode.WorldSpace;((RectTransform)p.CinemaScreen.transform).sizeDelta=new Vector2(1280,720);Canvas.ForceUpdateCanvases();
                    FishingXRPresentationValidation.CaptureCanvas(p.CinemaScreen,file);
                    frames[k]=new Texture2D(2,2);frames[k].LoadImage(File.ReadAllBytes(file));
                }
                Sheet(frames,Path.Combine(evidence,"sheet-"+(int)sequence+"-"+sequence+".png"));
                p.Finish();yield return null;
                Check(!p.Open,"Finishing restores control "+sequence);
            }
            Check(p.Unlocked(FishingPrologue.Sequence.Finale) && p.Latest==FishingPrologue.Sequence.Finale,"Fixture progress unlocks every chapter for journal replay");
            Debug.Log("[V044-STORY] sheets "+evidence);
        }
        private static IEnumerator Berth(FishingPrototype c,Vector3 berth)
        {
            if(c.Realms.Inside){c.Realms.Abort();yield return null;}
            if(c.Ashore)c.Shore.BoardImmediately();
            c.ReturnToHarbor();yield return null;
            var rb=c.Player.engine.RB;var p=berth+Vector3.up*.7f;rb.position=p;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(p,Quaternion.identity);Physics.SyncTransforms();
            float until=Time.time+.5f;while(Time.time<until)yield return null;
            Check(c.Shore.TryDisembark(),"Landing for story staging");yield return null;
        }
        private static IEnumerator Stage(FishingPrototype c,FishingPrologue.Sequence sequence)
        {
            var career=c.Career;
            switch(sequence)
            {
                case FishingPrologue.Sequence.Prologue:case FishingPrologue.Sequence.Chart:
                {
                    var go=Berth(c,c.Home.Berth);while(go.MoveNext())yield return go.Current;
                    var desk=c.Home.InteractionPoint(2);
                    c.Shore.Relocate(desk+Vector3.ProjectOnPlane(c.Home.Origin-desk,Vector3.up).normalized*.9f,Quaternion.LookRotation(Vector3.ProjectOnPlane(desk-c.Home.Origin,Vector3.up)));
                    if(sequence==FishingPrologue.Sequence.Chart){career.AcceptMayorship();career.DiscoverChart();}
                    break;
                }
                case FishingPrologue.Sequence.Finale:
                {
                    if(c.Realms.Inside){c.Realms.Abort();yield return null;}
                    for(int i=0;i<4;i++){career.CompleteShrine(i);career.ClaimShrineReward(i);}
                    career.CraftVoyageCompass(CraftStation.Workbench);career.OpenGrandTreasure();
                    var go=Berth(c,FishingCoastalPresentation.Berth(0));while(go.MoveNext())yield return go.Current;
                    var chest=c.Voyage.TreasureChest.position;
                    c.Shore.Relocate(c.Voyage.TreasureApproach,Quaternion.LookRotation(Vector3.ProjectOnPlane(chest-c.Voyage.TreasureApproach,Vector3.up)));
                    break;
                }
                default:
                {
                    int island=sequence-FishingPrologue.Sequence.Wind;
                    var go=Berth(c,FishingIslandWorld.Berth(island));while(go.MoveNext())yield return go.Current;
                    var approach=c.Realms.PortalApproach(island);
                    Check(c.Shore.Relocate(approach,Quaternion.identity),"Supported clear portal approach for realm "+island+" at "+approach);
                    Physics.SyncTransforms();
                    bool target=c.Realms.TryTarget(out int targetId,out var targetObject);
                    Check(target && targetId==island+8,"Reachable unobstructed entrance for realm "+island+"; feet="+c.Shore.Position+" target="+targetId+" / "+targetObject);
                    Check(c.Realms.Enter(island),"Enter realm "+island);
                    while(c.Realms.Transitioning)yield return null;yield return null;
                    var echo=c.Realms.Device(3).position;var stand=echo+Vector3.ProjectOnPlane(c.Shore.Position-echo,Vector3.up).normalized*1.6f;
                    c.Shore.Relocate(stand,Quaternion.LookRotation(Vector3.ProjectOnPlane(echo-stand,Vector3.up)));
                    career.CompleteShrine(island);
                    break;
                }
            }
            for(int i=0;i<10;i++)yield return null;
        }
        private static void Sheet(Texture2D[] frames,string path)
        {
            int w=640,h=frames[0].height*640/frames[0].width;var sheet=new Texture2D(w*3,h*2,TextureFormat.RGB24,false);
            var clear=new Color32[sheet.width*sheet.height];for(int i=0;i<clear.Length;i++)clear[i]=new Color32(12,18,28,255);sheet.SetPixels32(clear);
            for(int k=0;k<frames.Length;k++)
            {
                var f=frames[k];int ox=(k%3)*w,oy=(1-k/3)*h;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)sheet.SetPixel(ox+x,oy+y,f.GetPixelBilinear((x+.5f)/w,(y+.5f)/h));
                UnityEngine.Object.Destroy(f);
            }
            sheet.Apply();File.WriteAllBytes(path,sheet.EncodeToPNG());UnityEngine.Object.Destroy(sheet);
        }
    }
}
