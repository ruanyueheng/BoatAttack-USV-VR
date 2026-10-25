using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV035Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V035");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V035] CHECK: "+label);}
        private static FieldInfo Field(string name)=>typeof(FishingHandGames).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance);
        private static void Capture(FishingPrototype c,string name)=>FishingV016Validation.Capture(c,Path.GetFullPath("Logs/V035"),name);
        public static IEnumerator Checks(FishingPrototype c)
        {
            Directory.CreateDirectory("Logs/V035");
            var previous=FishingV034Validation.Checks(c);
            try{while(previous.MoveNext())yield return previous.Current;}finally{(previous as IDisposable)?.Dispose();}
            Check(FishingHandGames.StartHint(true)=="E 动手试试" && FishingHandGames.StartHint(false)=="右手柄 A 动手试试","Start labels identify the actual input device");
            var xrCanvas=(Canvas)Field("_canvas").GetValue(c.HandGames);var xrTitle=(UnityEngine.UI.Text)Field("_title").GetValue(c.HandGames);
            Check(xrCanvas.GetComponent<FishingUIThemeBindings>()!=null && xrTitle.font==FishingUITheme.Heading && xrTitle.GetComponent<UnityEngine.UI.Outline>()!=null,"XR handcraft paper follows the day/night palette and its brush-script title has a glyph outline");
            for(int game=0;game<2;game++)
            {
                var station=c.HandGames.Station(game);
                Check(c.Shore.Relocate(c.HandGames.Approach(game),Quaternion.LookRotation(-station.forward)),"Hands-on approach "+game);
                c.View.ResetLook();yield return null;c.PresentCharacterFrame();
                if(game==0)
                {
                    c.Shore.SetCrouch(true);
                    for(int frame=0;frame<15;frame++)yield return null;
                    c.PresentCharacterFrame();
                }
                c.viewCamera.transform.rotation=Quaternion.LookRotation(station.TransformPoint(new Vector3(0,1.1f,0))-c.viewCamera.transform.position);
                Check(c.HandGames.Begin(game),"Real handcraft can begin "+game);
                for(int i=0;i<15;i++)yield return null;
                Check(!c.Shore.Crouched && Vector3.Distance(c.viewCamera.transform.position,c.Shore.Character.EyePosition)<.002f
                    && c.viewCamera.transform.position.y>station.position.y+1.30f,"Tabletop camera follows the settled standing eye after a crouched start "+game);
                var shells=(Transform[])Field("_shells").GetValue(c.HandGames);
                var grains=(Transform[])Field("_grains").GetValue(c.HandGames);
                Vector3 shellStart=shells[0].localPosition;bool falling=false;float maxGripError=0;
                if(game==0)for(int i=0;i<6;i++)for(int j=0;j<12;j++)c.HandGames.Task.Sample(i%2==0?-.1f:.1f,0,.02f);
                else for(int i=0;i<110;i++){float a=i*Mathf.PI/36;c.HandGames.Task.Sample(Mathf.Cos(a)*.1f,Mathf.Sin(a)*.1f,.02f);}
                for(int frame=0;frame<24;frame++)
                {
                    Field("_motion").SetValue(c.HandGames,game==0?new Vector2(.1f+Mathf.Cos(frame*.7f)*.055f,Mathf.Sin(frame*.7f)*.065f):new Vector2(Mathf.Cos(frame*.7f)*.1f,Mathf.Sin(frame*.7f)*.1f));
                    Field("_motionSpeed").SetValue(c.HandGames,1f);Field("_visualFrame").SetValue(c.HandGames,-1);
                    c.PresentCharacterFrame();
                    if(game==1)Check(Vector3.Dot(station.Find("WinderBody").forward,Vector3.ProjectOnPlane(c.viewCamera.transform.position-station.position,Vector3.up).normalized)>.98f,"Winding plane faces the player throughout the complete turn");
                    maxGripError=Mathf.Max(maxGripError,Vector3.Distance(c.Hands.Left.Palm.position,c.Shore.Character.PalmPosition(true)),Vector3.Distance(c.Hands.Right.Palm.position,c.Shore.Character.PalmPosition(false)));
                    if(game==0)falling|=grains.Any(t=>t.gameObject.activeSelf);
                    if(frame%6==0)Capture(c,(game==0?"sieve":"winder")+"-motion-"+frame.ToString("D2"));
                    yield return null;
                }
                c.PresentCharacterFrame();var character=c.Shore.Character;
                Check(maxGripError<.04f,"Both moving grips remain reachable through the complete motion "+game+" max error="+maxGripError);
                Check(character.FirstPersonSkin.enabled && !character.FirstPersonSkin.forceRenderingOff && character.FirstPersonSkin.sharedMesh==character.TabletopArmsMesh,"Source arms remain visible after the final presentation pass "+game);
                foreach(bool left in new[]{true,false})
                {
                    var goal=(left?c.Hands.Left:c.Hands.Right).Palm.position;var actual=character.PalmPosition(left);var viewport=c.viewCamera.WorldToViewportPoint(actual);
                    Check(Vector3.Distance(goal,actual)<.04f,"Source palm reaches its moving attachment "+game+" left="+left+" error="+Vector3.Distance(goal,actual));
                    Check(viewport.z>.25f && viewport.x>.01f && viewport.x<.99f && viewport.y>0 && viewport.y<1,"Working hand stays in the real camera view "+game+" left="+left+" "+viewport);
                }
                var tool=game==0?c.HandGames.ActiveTool:station.Find("WinderBody");
                Check(new Vector2(tool.localPosition.x,tool.localPosition.z).magnitude<.08f,"Instrument stays over the centre of its table "+game);
                if(game==0)
                {
                    Check(falling && Vector3.Distance(shellStart,shells[0].localPosition)>.001f,"Visible shells tumble and real sand particles fall while shaking");
                    var tray=(Transform)Field("_tray").GetValue(c.HandGames);var sand=(Transform)Field("_caughtSand").GetValue(c.HandGames);
                    Check(tray.localPosition.x==0 && tray.localPosition.z==0 && sand.gameObject.activeSelf && sand.localScale.x>.1f,"Receiver remains centred and accumulates the sifted material");
                }
                else
                {
                    var line=(LineRenderer)Field("_thread").GetValue(c.HandGames);
                    Check(line.positionCount==12 && Vector3.Distance(line.GetPosition(0),line.GetPosition(11))>.08f,"Continuous flax runs from the supply coil to the turning spindle");
                }
                c.HandGames.Cancel();yield return null;
                Check(!grains.Any(t=>t.gameObject.activeSelf) && character.FirstPersonSkin.sharedMesh!=character.TabletopArmsMesh,"Cancel clears particles and restores the normal body "+game);
            }
        }
    }
}
