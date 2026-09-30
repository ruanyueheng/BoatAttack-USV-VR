using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace BoatAttack.Fishing
{
    /// <summary>Explicit, isolated graphics-player fixtures. Screenshots include the real IMGUI, never physical-input evidence.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class FishingFieldCapture : MonoBehaviour
    {
        private string _output;private bool _probe,_failed;private FishingPrototype _c;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-fishing-field-capture")>=0)new GameObject("Opt-in field screenshot fixtures").AddComponent<FishingFieldCapture>();
        }
        private IEnumerator Start()
        {
            if(Application.isBatchMode || SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null){Fail("A graphics backbuffer is required");yield break;}
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-field-capture-output");
            if(index<0 || index+1>=args.Length || !Path.IsPathRooted(args[index+1])){Fail("An explicit rooted output directory is required");yield break;}
            _output=args[index+1];Directory.CreateDirectory(_output);
            for(int i=0;i<300;i++){_c=FindObjectOfType<FishingPrototype>();if(_c!=null && _c.FieldWorld!=null)break;yield return null;}
            if(_c==null || _c.FieldWorld==null || !_c.HasIsolatedProfile){Fail("No isolated field scene");yield break;}
            yield return new WaitForSecondsRealtime(1);
            var bag=_c.Career.Backpack;bag.TryAdd(FieldItem.Binoculars,1);bag.TryAdd(FieldItem.Lantern,1);bag.TryAdd(FieldItem.Shell,18);bag.TryAdd(FieldItem.Driftwood,12);
            bag.Move(4,12);bag.Observe(LandAnimalKind.Rabbit);bag.Observe(LandAnimalKind.Fox);
            _c.ToggleInventory();yield return Capture("01-inventory");if(_failed)yield break;_c.ToggleInventory();
            var rb=_c.Player.engine.RB;var site=_c.Shore.Sites[0];rb.position=site.Berth+Vector3.up*.5f;rb.rotation=Quaternion.identity;rb.velocity=Vector3.zero;_c.Player.transform.position=rb.position;Physics.SyncTransforms();
            if(!_c.Shore.TryDisembark()){Fail("Landing fixture failed");yield break;}
            var animal=_c.FieldWorld.Animals[0];
            _c.FieldWorld.TryLocation(animal.Home,3.5f,180,out var feet);
            _c.Shore.Body.enabled=false;_c.Shore.Actor.position=feet+Vector3.up*.03f;_c.Shore.Body.enabled=true;
            _c.Shore.Actor.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(animal.Feet-feet,Vector3.up));_c.View.ResetLook();_c.View.ApplyShoreLookDelta(new Vector2(0,-130));
            _c.SelectFieldSlot(0);yield return Capture("02-field-exploration");if(_failed)yield break;
            _c.SelectFieldSlot(1);_c.UseFieldItem();yield return Capture("03-binoculars");if(_failed)yield break;
            _c.SelectFieldSlot(2);_c.UseFieldItem();
            var climate=_c.Weather.Climate;climate.Hold();for(int i=0;i<64;i++)climate.Tick(.25f,false);climate.Select(BayWeather.Sunset);for(int i=0;i<64;i++)climate.Tick(.25f,false);
            yield return Capture("04-lantern");if(_failed)yield break;
            _c.ToggleView();_c.Shore.SetCrouch(true);yield return Capture("05-crouch-third-person");if(_failed)yield break;
            Debug.Log("[FIELD-CAPTURE] PASS: five full-HUD synthetic fixture screenshots; physicalInputTested=false");Application.Quit(0);
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.4f);_probe=true;yield return new WaitForEndOfFrame();
            Texture2D image=null;
            try
            {
                image=ScreenCapture.CaptureScreenshotAsTexture();
                if(image==null)throw new InvalidOperationException("Missing graphics backbuffer");
                var pixel=image.GetPixel(12,image.height-13);
                bool valid=pixel.r>.85f && pixel.g<.15f && pixel.b>.85f;
                File.WriteAllBytes(Path.Combine(_output,(valid?"":"INVALID-")+name+".png"),image.EncodeToPNG());
                if(!valid)throw new InvalidOperationException("Full HUD proof marker missing");
                Debug.Log("[FIELD-CAPTURE] IMAGE: "+name+" "+image.width+"x"+image.height+" fullHudProbe=true");
            }
            catch(Exception e){Fail(e.Message);}
            finally{_probe=false;if(image!=null)Destroy(image);}
        }
        private void OnGUI(){if(_probe){var old=GUI.color;GUI.color=Color.magenta;GUI.DrawTexture(new Rect(8,8,8,8),Texture2D.whiteTexture);GUI.color=old;}}
        private void Fail(string message){_failed=true;Debug.LogError("[FIELD-CAPTURE] FAIL: "+message);Application.Quit(1);}
    }
}
