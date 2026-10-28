using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Diagnostic only (-fishing-hitch-tour): visits every area/event for the first time in a player and logs each frame over 33 ms with the step that caused it.</summary>
    public sealed class FishingHitchTour : MonoBehaviour
    {
        public const string Flag="-fishing-hitch-tour";
        public static void AttachIfRequested(FishingPrototype c)
        {
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),Flag)>=0)c.gameObject.AddComponent<FishingHitchTour>();
        }
        private string _step="startup";
        private readonly StringBuilder _log=new StringBuilder();
        private readonly Dictionary<string,(int count,float worst)> _byStep=new Dictionary<string,(int,float)>();
        private int _gc0;
        // Main-thread markers; only Development players report them (release players read 0).
        private static readonly string[] Markers={"GC.Collect","Loading.ReadObject","Loading.AwakeFromLoad","Shader.CreateGPUProgram","CreateGPUProgram","Shader.Parse","Instantiate","Resources.Load","Physics.Simulate","Physics.BakeCollisionMeshes","Mesh.Bake Scaled Mesh PhysX CollisionData","Update.ScriptRunBehaviourUpdate","PreLateUpdate.ScriptRunBehaviourLateUpdate","FixedUpdate.ScriptRunBehaviourFixedUpdate","Inl_RenderCameraStack","Gfx.WaitForPresentOnGfxThread","GarbageCollectAssetsProfile","AudioClip.LoadAudioData","Texture.AwakeFromLoad","Mesh.UploadMeshData","GUI.Repaint","PlayerLoop","EarlyUpdate.UpdatePreloading","Loading.UpdatePreloading","Application.Integrate Assets in Background","EarlyUpdate.UpdateStreamingManager","FixedUpdate.PhysicsFixedUpdate","Physics.Processing","PostLateUpdate.FinishFrameRendering","PreLateUpdate.DirectorUpdateAnimationBegin","Animators.Update","PostLateUpdate.UpdateAudio","AudioManager.Update","PostLateUpdate.PlayerSendFrameComplete","FrameEvents.NewInputSystemUpdate","Update.ScriptRunDelayedTasks","Update.ScriptRunDelayedDynamicFrameRate","Coroutines.RunDelayed"};
        private readonly List<Unity.Profiling.ProfilerRecorder> _recorders=new List<Unity.Profiling.ProfilerRecorder>();
        private void OnEnable(){foreach(var m in Markers)_recorders.Add(Unity.Profiling.ProfilerRecorder.StartNew(new Unity.Profiling.ProfilerCategory("Any"),m));}
        private void OnDisable(){foreach(var rec in _recorders)rec.Dispose();_recorders.Clear();}
        private void Update()
        {
            float ms=Time.unscaledDeltaTime*1000;int gc=System.GC.CollectionCount(0);
            if(ms>33)
            {
                _log.Append(Time.frameCount).Append(',').Append(_step).Append(',').Append(ms.ToString("F1")).Append(',').Append(gc-_gc0);
                // Markers report the previous frame, which is the slow one when Update runs right after it.
                for(int i=0;i<_recorders.Count;i++){double t=_recorders[i].LastValue*1e-6;if(t>=5)_log.Append(',').Append(Markers[i]).Append('=').Append(t.ToString("F0"));}
                _log.Append('\n');
                _byStep.TryGetValue(_step,out var s);_byStep[_step]=(s.count+1,Mathf.Max(s.worst,ms));
            }
            _gc0=gc;
        }
        private IEnumerator Wait(float seconds){for(float t=0;t<seconds;t+=Time.unscaledDeltaTime)yield return null;}
        private IEnumerator Start()
        {
            var c=GetComponent<FishingPrototype>();
            var teleport=typeof(FishingPrototype).GetMethod("Teleport",BindingFlags.Instance|BindingFlags.NonPublic);
            yield return Wait(8);
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-fishing-warmup-all")>=0)
            {_step="warmup-all";var watch=System.Diagnostics.Stopwatch.StartNew();Shader.WarmupAllShaders();Debug.Log("[HITCH-TOUR] WarmupAllShaders ms="+watch.ElapsedMilliseconds);yield return null;}
            _step="title-finish";if(c.StartMenuOpen)c.FinishTitleVoyage();yield return Wait(4);
            if(c.Paused)c.TogglePause();
            for(int island=0;island<4;island++)
            {
                _step="sail-arrive-island"+island;c.ReturnToHarbor();yield return null;
                teleport.Invoke(c,new object[]{FishingIslandWorld.Berth(island),Quaternion.Euler(0,180,0)});yield return Wait(4);
                _step="disembark-island"+island;if(!c.Shore.TryDisembark())continue;yield return Wait(4);
                _step="enter-realm"+island;
                if(c.Shore.Relocate(c.Realms.PortalApproach(island),Quaternion.identity) && c.Realms.Enter(island))
                {
                    yield return Wait(5);_step="exit-realm"+island;c.Realms.Exit();yield return Wait(4);
                }
            }
            foreach(SeaEventKind kind in System.Enum.GetValues(typeof(SeaEventKind)))
            {
                _step="sea-"+kind;c.ReturnToHarbor();yield return null;c.SeaEvents.Force(kind);yield return null;
                var at=c.SeaEvents.Center+new Vector3(0,0,-18);at.y=c.Player.transform.position.y;
                teleport.Invoke(c,new object[]{at,Quaternion.identity});yield return Wait(5);
            }
            _step="night";c.ReturnToHarbor();c.Career.Calendar.AdvanceToHour(22);c.Weather.ApplyEnvironment();c.Nightscape.Apply(0);yield return Wait(4);
            _step="rain";for(int i=0;i<48;i++)c.Weather.Climate.Tick(.25f,false);
            if(c.Weather.Climate.Current==BayWeather.Rain || c.Weather.Climate.Select(BayWeather.Rain)){for(int i=0;i<48;i++)c.Weather.Climate.Tick(.25f,false);c.Weather.ApplyEnvironment();}
            yield return Wait(4);
            _step="done";
            var summary=new StringBuilder("step,hitches_over_33ms,worst_ms\n");
            foreach(var kv in _byStep)summary.Append(kv.Key).Append(',').Append(kv.Value.count).Append(',').Append(kv.Value.worst.ToString("F1")).Append('\n');
            var file=Path.Combine(Application.persistentDataPath,"hitch-tour.txt");
            File.WriteAllText(file,summary+"\nframe,step,ms,gc0,markers>=5ms\n"+_log);
            Debug.Log("[HITCH-TOUR] "+file+"\n"+summary);Application.Quit();
        }
    }
}
