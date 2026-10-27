using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BoatAttack.VR;
using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Explicit isolated benchmark: real engine sailing segments, with unmeasured scene transitions.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class FishingPerformanceVoyage : MonoBehaviour
    {
        public const string EnableFlag="-fishing-performance-voyage";
        private const float WarmupSeconds=3,MeasureSeconds=10;
        [Serializable] private sealed class Phase
        {
            public int segment;
            public string name,mode;
            public Vector3 start,end,target;
            public float travelledMetres;
            public bool riflePresented,continuous;
        }
        [Serializable] private sealed class Report
        {
            public string version,description,csv,readyUtc;
            public double runtimeSceneReadySeconds;
            public int width,height;
            public List<Phase> phases;
        }
        private FishingPrototype _c;
        private FishingPerformanceCapture _capture;
        private USVInputRouter _router;
        private bool _routerEnabled,_sailing,_finished,_followingRoute;
        private Vector3 _goal;
        private Vector3[] _continuousRoute;
        private int _nextWaypoint;
        private double _sceneReady;
        private string _sceneReadyUtc;
        private readonly List<Phase> _phases=new List<Phase>();

        public void Begin(FishingPrototype controller,FishingPerformanceCapture capture)
        {
            Require(Array.IndexOf(Environment.GetCommandLineArgs(),EnableFlag)>=0,"Explicit benchmark flag is required.");
            Require(controller!=null && controller.Automated && controller.HasIsolatedProfile && controller.AcceptanceOptions==null,
                "The benchmark needs its own automated profile and cannot share an acceptance driver.");
            Require(!Application.isBatchMode && !XRSettings.isDeviceActive,"This measurement requires a rendered desktop player.");
            _c=controller;_capture=capture;
            Application.logMessageReceived+=CaptureError;
            StartCoroutine(Run());
        }
        private static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("[PERFORMANCE-VOYAGE] "+message);}
        private IEnumerator Run()
        {
            yield return null;
            _router=USVInputRouter.EnsureInstance();_routerEnabled=_router.enabled;_router.enabled=false;
            float deadline=Time.realtimeSinceStartup+20;
            while(!_c.CoastalDepthReady && Time.realtimeSinceStartup<deadline)yield return null;
            Require(_c.CoastalDepthReady,"World shoreline capture did not become ready.");
            _sceneReady=Time.realtimeSinceStartupAsDouble;_sceneReadyUtc=DateTime.UtcNow.ToString("O");
            Debug.Log("[PERFORMANCE-VOYAGE] READY: runtime_scene_seconds="+_sceneReady.ToString("F4",System.Globalization.CultureInfo.InvariantCulture));
            Require(Screen.width==1920 && Screen.height==1080,"Use a 1920 x 1080 player for the fixed comparison.");
            Require(_c.Career.Backpack.TryAdd(FieldItem.HuntingRifle,1),"The isolated rifle fixture must fit in the backpack.");
            _c.Weather.Climate.Hold();
            if(_c.FirstPerson==false)_c.ToggleView();
            Require(_c.Career.Calendar.AdvanceToHour(12),"The continuous first-approach clock must start at noon.");
            if(_c.Career.Calendar.Automatic)_c.Career.Calendar.ToggleAutomatic();
            yield return SailContinuous(_c.HomeNavigationIndex,"continuous-harbor-home-day",100);
            yield return SailContinuous(_c.IslandNavigationIndex(0),"continuous-home-island-0-day",140);
            for(int night=0;night<2;night++)
            {
                Require(_c.Career.Calendar.AdvanceToHour(night==0?12:21),"The fixed day/night clock must advance.");
                for(int area=0;area<6;area++)
                {
                    _capture.Segment=0;_sailing=false;
                    if(_c.Ashore)_c.Shore.BoardImmediately();
                    Equip(FieldItem.Empty);
                    Vector3 berth,outward;int navigation;string label;
                    if(area==0)
                    {
                        berth=_c.harbor;outward=Vector3.ProjectOnPlane(_c.spawnPosition-_c.harbor,Vector3.up).normalized;
                        navigation=0;label="harbor";
                    }
                    else if(area==1){berth=_c.Home.Berth;outward=Vector3.back;navigation=_c.HomeNavigationIndex;label="home-coast";}
                    else{int island=area-2;berth=FishingIslandWorld.Berth(island);outward=Vector3.forward;navigation=_c.IslandNavigationIndex(island);label="island-"+island;}
                    var start=berth+outward*100;_goal=berth+outward*40;start.y=_goal.y=0;
                    Require(_c.WaterRoute.ClearSegment(start,_goal),"Fixed sailing segment is obstructed: "+label);
                    _c.TeleportForPerformance(start+Vector3.up*.5f,Quaternion.LookRotation(-outward));_c.SelectNavigation(navigation);
                    yield return new WaitForSeconds(WarmupSeconds);
                    var phase=NewPhase(label+(night==0?"-day":"-night"),"production-engine sailing; scene transition excluded");
                    _sailing=true;
                    yield return new WaitForSeconds(MeasureSeconds);
                    _sailing=false;EndPhase(phase);
                    Require(phase.travelledMetres>=5,"Actual engine sailing did not advance at least 5m: "+phase.name);
                    Require(_c.WaterRoute.ClearPoint(_c.Player.transform.position),"The measured sailing segment ended outside clear water: "+phase.name);
                }
                for(int armed=0;armed<2;armed++)
                {
                    _capture.Segment=0;_sailing=false;
                    if(_c.Ashore)_c.Shore.BoardImmediately();
                    _c.TeleportForPerformance(_c.Home.Berth+Vector3.up*.2f,Quaternion.identity);
                    Require(_c.Shore.TryDisembark(),"The benchmark must use the real home landing.");
                    Require(_c.Shore.Relocate(_c.Home.Origin+new Vector3(0,.22f,-3.5f),Quaternion.Euler(0,180,0)),"Home fixture footing is blocked.");
                    Equip(armed==0?FieldItem.Empty:FieldItem.HuntingRifle);_c.View.ResetLook();
                    yield return new WaitForSeconds(WarmupSeconds);
                    Require(_c.RifleHunting.Presented==(armed==1),"The measured hand/AK presentation does not match the fixture.");
                    var phase=NewPhase("home-"+(armed==0?"empty":"rifle")+(night==0?"-day":"-night"),"stationary real shore character and held-tool rendering");
                    yield return new WaitForSeconds(MeasureSeconds);
                    EndPhase(phase);
                }
            }
            Require(_phases.Count==18,"The benchmark must complete two continuous first approaches and all sixteen fixed segments.");
            _finished=true;_capture.StopAndWrite();
            Require(!string.IsNullOrEmpty(_capture.OutputPath) && File.Exists(_capture.OutputPath),"Performance CSV was not saved.");
            var report=new Report{version=FishingPrototype.Version,width=Screen.width,height=Screen.height,csv=_capture.OutputPath,phases=_phases,runtimeSceneReadySeconds=_sceneReady,readyUtc=_sceneReadyUtc,
                description="Two continuous daytime first approaches sail from the original harbor spawn to home and then wind island using the production route, Engine and Rigidbody; planning and first approach frames are included, without teleports or warmup. Sixteen separate steady cases retain twelve near-shore sailing and four home empty-hand/AK cases; only their setup teleports and three-second warmups have segment_id=0. This does not certify physical keyboard input, all-world navigation, cold OS caches or headset performance."};
            File.WriteAllText(Path.ChangeExtension(_capture.OutputPath,"tour.json"),JsonUtility.ToJson(report,true));
            Debug.Log("[PERFORMANCE-VOYAGE] PASS: CSV="+_capture.OutputPath);
            if(!Application.isEditor)Application.Quit(0);
            enabled=false;
        }
        private IEnumerator SailContinuous(int navigation,string label,float timeout)
        {
            Require(!_c.Ashore && !_c.Anchored && !_c.Paused,"Continuous sailing must begin in the existing afloat boat.");
            Equip(FieldItem.Empty);
            var phase=NewPhase(label,"continuous production route and engine; planning/first approach included; no teleport/warmup",true);
            _c.SelectNavigation(navigation);_c.WaterRoute.RequestPlan();
            float deadline=Time.realtimeSinceStartup+timeout;
            while(_c.WaterRoute.Planning)
            {
                Require(Time.realtimeSinceStartup<deadline,"Continuous route planning timed out: "+label);
                yield return null;
            }
            var points=_c.WaterRoute.Points;
            Require(points.Count>=2 && FishingPrototype.FlatDistance(points[points.Count-1],_c.NavigationPosition)<=8.1f,
                "The continuous approach needs a complete safe production route: "+label+" / "+_c.WaterRoute.Failure);
            _continuousRoute=new Vector3[points.Count];for(int i=0;i<points.Count;i++)_continuousRoute[i]=points[i];
            phase.target=_continuousRoute[_continuousRoute.Length-1];_nextWaypoint=1;_followingRoute=_sailing=true;
            while(FishingPrototype.FlatDistance(_c.Player.transform.position,phase.target)>8)
            {
                Require(Time.realtimeSinceStartup<deadline,"Continuous engine sailing timed out: "+label);
                yield return null;
            }
            _followingRoute=_sailing=false;EndPhase(phase);
            Require(phase.travelledMetres>=50,"Continuous sailing did not cover its real first approach: "+label);
            Require(_c.WaterRoute.ClearPoint(_c.Player.transform.position),"The continuous approach must finish in clear water: "+label);
        }
        private Phase NewPhase(string label,string mode,bool continuous=false)
        {
            var phase=new Phase{segment=_phases.Count+1,name=label,mode=mode,start=_c.ExplorerPosition,riflePresented=_c.RifleHunting.Presented,continuous=continuous};
            _phases.Add(phase);_capture.Segment=phase.segment;
            Debug.Log("[PERFORMANCE-VOYAGE] SEGMENT "+phase.segment+": "+label);
            return phase;
        }
        private void EndPhase(Phase phase)
        {
            phase.end=_c.ExplorerPosition;phase.travelledMetres=FishingPrototype.FlatDistance(phase.start,phase.end);_capture.Segment=0;
        }
        private void Equip(FieldItem item)
        {
            for(int slot=0;slot<FishingBackpack.HotbarCount;slot++)
                if(_c.Career.Backpack.At(slot).item==item){_c.SelectFieldSlot(slot);return;}
            throw new InvalidOperationException("[PERFORMANCE-VOYAGE] Fixture item is missing from hotbar: "+item);
        }
        private void FixedUpdate()
        {
            if(!_sailing || _c==null || _c.Paused || _c.Ashore)return;
            if(_followingRoute)
            {
                while(_nextWaypoint<_continuousRoute.Length-1 && FishingPrototype.FlatDistance(_c.Player.transform.position,_continuousRoute[_nextWaypoint])<2)
                    _nextWaypoint++;
                _goal=_continuousRoute[_nextWaypoint];
            }
            var delta=Vector3.ProjectOnPlane(_goal-_c.Player.transform.position,Vector3.up);
            float turn=Vector3.SignedAngle(Vector3.ProjectOnPlane(_c.Player.transform.forward,Vector3.up),delta,Vector3.up);
            float throttle=_followingRoute?Mathf.Clamp01(delta.magnitude/25)*Mathf.Clamp01(1-Mathf.Abs(turn)/60):delta.magnitude<8?0:Mathf.Clamp01(delta.magnitude/25);
            _c.Player.engine.Accelerate(throttle);
            _c.Player.engine.Turn(Mathf.Clamp(turn/30,-1,1));
        }
        private void CaptureError(string message,string stack,LogType type)
        {
            if(_finished || (type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert))return;
            _finished=true;_sailing=false;_capture?.StopAndWrite();
            Debug.LogError("[PERFORMANCE-VOYAGE] FAIL: "+message);
            if(!Application.isEditor)Application.Quit(1);
            enabled=false;
        }
        private void OnDisable()
        {
            _sailing=false;Application.logMessageReceived-=CaptureError;
            if(_router!=null)_router.enabled=_routerEnabled;
        }
    }
}
