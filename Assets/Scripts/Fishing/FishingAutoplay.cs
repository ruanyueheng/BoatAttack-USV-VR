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
        private float _started,_captureAt,_landingCaptureAt;
        private bool _captured, _thirdCaptured, _landingCaptured, _pufferCaptured;
        private string _captureDirectory;
        private void Start()
        {
            Completed=false;Failure=null;_started=Time.realtimeSinceStartup;
            Application.logMessageReceived+=CaptureError;
            _captureDirectory=Argument("-fishing-capture");
            if(!string.IsNullOrEmpty(_captureDirectory))Directory.CreateDirectory(_captureDirectory);
            _stage=-2;_captureAt=Time.realtimeSinceStartup+2f;
        }
        private void Update()
        {
            if(Completed || Failure!=null)return;
            try
            {
                if(Time.realtimeSinceStartup-_started>210f)throw new Exception("Fishing automation timed out, phase "+Controller.Session.Phase);
                switch(_stage)
                {
                    case -2:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        ValidateDeparture();
                        ValidatePresentation();
                        StartCoroutine(Capture("00-first-person-driving.png"));
                        _captureAt=Time.realtimeSinceStartup+1f;_stage=-1;break;
                    case -1:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        Controller.MoveToSpotForAutomation(0);Controller.Interact();_stage=0;break;
                    case 0:
                        if(!Controller.Anchored)throw new Exception("Could not enter fishing station. Spot distance="+Controller.SpotDistance+" harbor="+Controller.HarborDistance+" speed="+Controller.Speed+" message="+Controller.Message);
                        if(!Controller.View.OwnsCamera)throw new Exception("Desktop test unexpectedly found active XR.");
                        var phase=Controller.Session.Phase;int inventory=Controller.Career.Count;bool view=Controller.FirstPerson;
                        Controller.ToggleView();Controller.View.ApplyView();
                        if(Controller.FirstPerson==view || phase!=Controller.Session.Phase || inventory!=Controller.Career.Count)throw new Exception("View switch changed gameplay state.");
                        Controller.ToggleView();Controller.View.ApplyView();
                        if(Controller.FirstPerson!=view || Mathf.Abs(Mathf.DeltaAngle(Controller.viewCamera.transform.eulerAngles.z,0))>0.05f)throw new Exception("View did not preserve level horizon.");
                        Time.timeScale=0.5f;Controller.TogglePause();var cameraPose=Controller.viewCamera.transform.position;Controller.View.ApplyView();
                        if(!Controller.Paused || Controller.viewCamera.transform.position!=cameraPose)throw new Exception("Pause moved view.");
                        Controller.TogglePause();
                        if(Controller.Paused || Time.timeScale!=0.5f)throw new Exception("Resume did not restore prior time scale.");
                        Time.timeScale=1f;
                        if(_fishCount==0)ValidateCameraObstruction();
                        Controller.ToggleJournal();
                        if(!Controller.Paused || !Controller.JournalOpen)throw new Exception("Journal did not pause simulation.");
                        Controller.ToggleJournal();
                        if(Controller.Paused || Controller.JournalOpen || Time.timeScale!=1f)throw new Exception("Journal did not restore simulation.");
                        if(_fishCount>0)Controller.CycleBait();
                        if(!Controller.Cast())throw new Exception("Could not cast.");
                        var bait=Controller.Bait;Controller.CycleBait();
                        if(Controller.Bait!=bait)throw new Exception("Bait changed during an active cast.");
                        _stage=1;break;
                    case 1:
                        if(Controller.Session.Phase==CatchPhase.Bite)Controller.Hook();
                        Controller.AutomationReel=Controller.Session.Tension<0.6f;
                        if(Controller.Session.Phase==CatchPhase.Fighting && !_captured && Time.realtimeSinceStartup-_started>5f)
                        {
                            if(Controller.NetDeployed)throw new Exception("Net must be stowed while fighting.");
                            _captured=true;StartCoroutine(Capture("01-first-person.png"));
                        }
                        if(Controller.Session.Phase==CatchPhase.Fighting && _fishCount==1 && !_thirdCaptured && Controller.Session.Distance<Controller.Session.MaxDistance-3f)
                        {_thirdCaptured=true;StartCoroutine(Capture("03-third-person.png"));}
                        if(Controller.Session.Phase==CatchPhase.Fighting && _fishCount==2 && !_pufferCaptured && Controller.Session.Distance<Controller.Session.MaxDistance-3f)
                        {_pufferCaptured=true;StartCoroutine(Capture("04-puffer.png"));}
                        if(Controller.Session.Phase==CatchPhase.Landing && _fishCount==0 && !_landingCaptured)
                        {
                            if(_landingCaptureAt==0f)_landingCaptureAt=Time.realtimeSinceStartup+0.45f;
                            if(Time.realtimeSinceStartup<_landingCaptureAt)return;
                            if(!Controller.NetDeployed)throw new Exception("Net did not deploy for landing.");
                            _landingCaptured=true;StartCoroutine(Capture("02-landing.png"));return;
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
                            if(_fishCount==1)StartCoroutine(Capture("02b-catch.png"));
                            _captureAt=Time.realtimeSinceStartup+1.2f;_stage=2;
                        }
                        break;
                    case 2:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        if(_fishCount<3)
                        {
                            Controller.LeaveStation();Controller.MoveToSpotForAutomation(_fishCount);Controller.Interact();
                            if((_fishCount==1 && Controller.FirstPerson)||(_fishCount==2 && !Controller.FirstPerson))Controller.ToggleView();
                            _stage=0;
                        }
                        else{Controller.ReturnToHarbor();if(Controller.FirstPerson)Controller.ToggleView();_captureAt=Time.realtimeSinceStartup+2f;_stage=3;}
                        break;
                    case 3:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        int value=Controller.Career.HoldValue;
                        if(value<48 || !Controller.Career.OrderReady)throw new Exception("Mixed-species order not ready.");
                        int payout=Controller.DeliverOrder();
                        if(payout<=24 || Controller.Career.Count!=1 || Controller.Career.OrdersCompleted!=1)throw new Exception("Order delivery failed.");
                        if(Controller.DeliverOrder()!=0)throw new Exception("Duplicate order paid twice.");
                        int remaining=Controller.Sell();
                        if(payout+remaining!=value+24 || Controller.Career.Count!=0)throw new Exception("Order/sale total failed.");
                        if(Controller.Sell()!=0)throw new Exception("Duplicate sale paid twice.");
                        if(!Controller.BuyUpgrade() || Controller.Career.RodLevel!=1)throw new Exception("Upgrade failed.");
                        var reloaded=FishingSaveStore.Load(Controller.SavePath,out var note);
                        if(reloaded.Coins!=Controller.Career.Coins || reloaded.RodLevel!=1 || reloaded.Count!=0 || reloaded.OrdersCompleted!=1 || reloaded.Catches!=3 || reloaded.DiscoveryMask!=Controller.Career.DiscoveryMask || note.Length>0)throw new Exception("Save round trip failed.");
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
        private void ValidateDeparture()
        {
            var c=Controller;
            Debug.Log("[FISHING] Departure: configured="+c.spawnPosition+" transform="+c.Player.transform.position+" rigidbody="+c.Player.engine.RB.position+" harbor="+c.harbor+" yaw="+c.Player.transform.eulerAngles.y);
            if(FishingPrototype.FlatDistance(c.Player.transform.position,c.spawnPosition)>3f || !c.AtHarbor)
                throw new Exception("Boat did not stay at its configured harbor spawn before departure.");
            var camera=c.viewCamera.transform;
            foreach(var hit in Physics.SphereCastAll(camera.position,0.25f,camera.forward,12f,~(1<<4),QueryTriggerInteraction.Ignore))
                if(!hit.collider.transform.IsChildOf(c.Player.transform) && hit.collider.GetComponentInParent<WaterSystem.Water>()==null)
                    throw new Exception("Initial first-person departure is blocked by "+hit.collider.name+" at "+hit.distance+"m.");
            Debug.Log("[FISHING] Departure checks: stable harbor spawn and 12m forward camera clearance.");
        }
        private void ValidateCameraObstruction()
        {
            // Reproduce a newly occluded third-person view, not just its final settled position.
            var c=Controller;
            if(!c.FirstPerson)throw new Exception("Camera obstruction test requires the first-person station.");
            var yaw=Quaternion.Euler(0,c.Player.transform.eulerAngles.y,0);
            var origin=c.viewCamera.transform.position-yaw*new Vector3(0.05f,2f,-0.55f);
            var pivot=origin+yaw*new Vector3(2.7f,0.8f,1f);
            c.ToggleView();c.View.ApplyView();
            var offset=c.viewCamera.transform.position-pivot;
            if(offset.magnitude<3f)throw new Exception("Camera obstruction fixture has insufficient clearance.");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.name="Temporary camera regression wall";
                wall.transform.SetPositionAndRotation(pivot+offset*0.5f,Quaternion.LookRotation(offset));
                wall.transform.localScale=new Vector3(20f,20f,0.3f);
                Physics.SyncTransforms();
                c.View.ApplyView();
                if(Vector3.Distance(c.viewCamera.transform.position,pivot)>offset.magnitude*0.5f-0.25f)
                    throw new Exception("Camera smoothing left the view behind a newly encountered wall.");
                Debug.Log("[FISHING] Camera obstruction check: newly blocked view stays in front of the wall immediately.");
            }
            finally
            {
                wall.GetComponent<Collider>().enabled=false;Destroy(wall);
                c.ToggleView();c.View.ApplyView();
            }
        }
        private void ValidatePresentation()
        {
            // These are geometry/layout regressions, not a substitute for reviewing the rendered pictures.
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1366,768),new Vector2Int(2560,1080),new Vector2Int(1280,1024)})
            {
                float scale=FishingHUD.LayoutScale(size.x,size.y);
                if(1600f*scale>size.x+0.01f || 900f*scale>size.y+0.01f)throw new Exception("HUD safe area exceeds "+size);
            }
            var sign=Controller.transform.Find("Tide and Fin floating fish market/Market name");
            if(sign==null || Quaternion.Angle(sign.localRotation,Quaternion.identity)>0.01f)throw new Exception("Market sign is facing backwards.");
            int fishMeshes=0;
            foreach(var filter in Controller.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;
                if(mesh==null || !mesh.name.StartsWith("Fishing v0.2 mesh "))continue;
                fishMeshes++;
                foreach(var normal in mesh.normals)
                    if(float.IsNaN(normal.sqrMagnitude) || normal.sqrMagnitude<0.5f)throw new Exception("Fish contains invalid/cancelled surface normals.");
            }
            if(fishMeshes<3)throw new Exception("Three modeled species must be present in market display.");
            var scenery=Controller.GetComponent<FishingScenery>();
            if(scenery==null || scenery.CombinedRenderers>40 || scenery.CombinedVertices>60000)throw new Exception("V03 added scenery exceeded its static geometry budget.");
            if(Controller.GetComponent<FishingWorldInstruments>()==null || FishingWorldInstruments.ReferenceDistance<2f)throw new Exception("World instrument comfort setup missing.");
            int target=Controller.NavigationTarget;for(int i=0;i<4;i++)Controller.CycleNavigation();
            if(Controller.NavigationTarget!=target)throw new Exception("Navigation cycle failed.");
            Debug.Log("[FISHING] Presentation checks: 4 viewport safe areas, readable sign orientation, fish surface normals.");
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
            yield return Capture("05-harbor.png");
            Controller.ToggleJournal();
            if(!Controller.Paused || !Controller.JournalOpen)throw new Exception("Journal capture state failed.");
            if(!string.IsNullOrEmpty(_captureDirectory))yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("06-journal.png");
            Controller.ToggleJournal();Controller.TogglePause();
            if(!string.IsNullOrEmpty(_captureDirectory))yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("07-comfort-settings.png");
            Controller.TogglePause();Controller.ToggleInstruments();
            if(!Controller.FirstPerson)Controller.ToggleView();
            if(!string.IsNullOrEmpty(_captureDirectory))yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("08-world-instruments.png");
            Controller.ToggleInstruments();
            if(!string.IsNullOrEmpty(_captureDirectory))yield return new WaitForSecondsRealtime(1.5f);
            Completed=true;Debug.Log("[FISHING] PASS: 3 species, both views, immediate camera obstruction recovery, level horizon, pause/time-scale recovery, net staging, mesh normals, HUD safe areas, inventory, three baits, order delivery, discovery persistence, journal pause, navigation, geometry budget, sale, upgrade, save and backup recovery. Automation did not test real input or VR.");
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
