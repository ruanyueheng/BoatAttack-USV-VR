using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace BoatAttack.Fishing
{
    /// <summary>Explicit command-line-only test driver. Never enabled in a normal game.</summary>
    public sealed class FishingAutoplay : MonoBehaviour
    {
        public FishingPrototype Controller;
        public static bool Completed { get; private set; }
        public static string Failure { get; private set; }
        private int _stage,_fishCount;
        private float _started,_captureAt;
        private bool _captured;
        private string _captureDirectory;
        private void Start()
        {
            Completed=false;Failure=null;_started=Time.realtimeSinceStartup;
            Application.logMessageReceived+=CaptureError;
            _captureDirectory=Argument("-fishing-capture");
            if(!string.IsNullOrEmpty(_captureDirectory))Directory.CreateDirectory(_captureDirectory);
            Controller.MoveToSpotForAutomation(0);Controller.Interact();
        }
        private void Update()
        {
            if(Completed || Failure!=null)return;
            try
            {
                if(Time.realtimeSinceStartup-_started>150f)throw new Exception("Fishing automation timed out, phase "+Controller.Session.Phase);
                switch(_stage)
                {
                    case 0:
                        if(!Controller.Anchored)throw new Exception("Could not enter fishing station. Spot distance="+Controller.SpotDistance+" harbor="+Controller.HarborDistance+" speed="+Controller.Speed+" message="+Controller.Message);
                        if(!Controller.Cast())throw new Exception("Could not cast.");_stage=1;break;
                    case 1:
                        if(Controller.Session.Phase==CatchPhase.Bite)Controller.Hook();
                        Controller.AutomationReel=Controller.Session.Tension<0.6f;
                        if(Controller.Session.Phase==CatchPhase.Fighting && !_captured && Time.realtimeSinceStartup-_started>5f)
                        {
                            _captured=true;StartCoroutine(Capture("01-fishing.png"));
                        }
                        if(Controller.Session.Phase==CatchPhase.Landing && Controller.Session.CanLand)
                        {
                            int before=Controller.Career.Count;
                            if(!Controller.Scoop() || Controller.Career.Count!=before+1)throw new Exception("Scoop did not commit inventory immediately.");
                            if(Controller.Scoop() || Controller.Career.Count!=before+1)throw new Exception("Repeated scoop duplicated a catch.");
                        }
                        if(Controller.Session.Phase==CatchPhase.Escaped)throw new Exception(Controller.Session.Failure);
                        if(Controller.Career.Count>_fishCount)
                        {
                            _fishCount++;
                            // Capture before the following stage teleports to the next area.
                            if(_fishCount==1)StartCoroutine(Capture("02-catch.png"));
                            _captureAt=Time.realtimeSinceStartup+1.2f;_stage=2;
                        }
                        break;
                    case 2:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        if(_fishCount<3)
                        {
                            Controller.LeaveStation();Controller.MoveToSpotForAutomation(_fishCount);Controller.Interact();_stage=0;
                        }
                        else{Controller.ReturnToHarbor();_captureAt=Time.realtimeSinceStartup+2f;_stage=3;}
                        break;
                    case 3:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        int value=Controller.Career.HoldValue;
                        if(Controller.Sell()!=value || value<48 || Controller.Career.Count!=0)throw new Exception("Sale failed.");
                        if(Controller.Sell()!=0)throw new Exception("Duplicate sale paid twice.");
                        if(!Controller.BuyUpgrade() || Controller.Career.RodLevel!=1)throw new Exception("Upgrade failed.");
                        var reloaded=FishingSaveStore.Load(Controller.SavePath,out var note);
                        if(reloaded.Coins!=Controller.Career.Coins || reloaded.RodLevel!=1 || reloaded.Count!=0 || note.Length>0)throw new Exception("Save round trip failed.");
                        // Force a backup and verify recovery in the isolated test save, never player progress.
                        FishingSaveStore.Save(Controller.SavePath,Controller.Career);
                        File.WriteAllText(Controller.SavePath,"corrupt test payload");
                        reloaded=FishingSaveStore.Load(Controller.SavePath,out note);
                        if(reloaded.RodLevel!=1 || note.Length==0)throw new Exception("Backup recovery failed.");
                        _captureAt=Time.realtimeSinceStartup+2f;_stage=4;break;
                    case 4:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        StartCoroutine(Finish());_stage=5;break;
                }
            }
            catch(Exception ex){Failure=ex.ToString();Debug.LogError("[FISHING] FAIL: "+Failure);if(!Application.isEditor)Application.Quit(1);}
        }
        private IEnumerator Capture(string name)
        {
            if(string.IsNullOrEmpty(_captureDirectory))yield break;
            // WaitForEndOfFrame is unsupported in batchmode; capture is for the real Windows player.
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_captureDirectory,name));
            Debug.Log("[FISHING] Screenshot requested: "+name);
        }
        private IEnumerator Finish()
        {
            yield return Capture("03-harbor.png");
            if(!string.IsNullOrEmpty(_captureDirectory))yield return new WaitForSecondsRealtime(1.5f);
            Completed=true;Debug.Log("[FISHING] PASS: 3 species caught, inventory, sale, upgrade, save and backup recovery. Automation did not test real input or VR.");
            if(!Application.isEditor)Application.Quit(0);
        }
        private void CaptureError(string message,string stack,LogType type)
        {
            if(Completed || Failure!=null || (type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert))return;
            Failure=message;
            if(!Application.isEditor)Application.Quit(1);
        }
        private void OnDestroy(){Application.logMessageReceived-=CaptureError;}
        private static string Argument(string key)
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0 && i+1<args.Length && !args[i+1].StartsWith("-")?args[i+1]:null;
        }
    }
}
