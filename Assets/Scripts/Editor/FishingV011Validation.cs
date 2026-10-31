using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoatAttack.EditorTools
{
    public static class FishingMapValidation { public static void Run()=>FishingV011Validation.Begin("MAP"); }
    public static class FishingInteractionValidation { public static void Run()=>FishingV011Validation.Begin("INTERACTION"); }

    /// <summary>共享串行验收驱动。只在显式批处理的隔离存档中进入现有场景，不重建或保存场景。</summary>
    [InitializeOnLoad]
    public static class FishingV011Validation
    {
        private const string Key="Fishing.V011Validation.";
        private static IEnumerator _checks;
        private static double _next;
        private static int _lastCheckFrame=-1;
        static FishingV011Validation()
        {
            EditorApplication.update+=Tick;
            Application.logMessageReceived+=(message,stack,type)=>
            {
                if(SessionState.GetBool(Key+"active",false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert))SessionState.SetString(Key+"error",message);
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key+"active",false))return;
                SessionState.SetBool(Key+"active",false);
                string error=SessionState.GetString(Key+"error","");
                if(!SessionState.GetBool(Key+"finished",false) && error=="")error="检查未完整执行";
                string marker="["+SessionState.GetString(Key+"suite","")+"]";
                if(error=="")Debug.Log(marker+" PASS: v"+FishingPrototype.Version+" 源场景/运行时断言完成；不等同于人工画面、实体输入或头显验收。");
                else Debug.LogError(marker+" FAIL: "+error);
                EditorApplication.Exit(error==""?0:1);
            };
        }
        public static void Begin(string suite)
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("请通过显式批处理入口使用隔离验收存档。");
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("地图/人物/交互验收需要图形设备，不得使用 -nographics。");
            SessionState.SetString(Key+"suite",suite);SessionState.SetString(Key+"error","");SessionState.SetBool(Key+"finished",false);SessionState.SetBool(Key+"active",true);
            SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+600);
            try
            {
                if(suite=="MAP")ValidateDemoNavigationAsset();
                string importError=SessionState.GetString(Key+"error","");
                if(importError!="")throw new InvalidOperationException(importError);
                FishingScriptBinding.ValidateScene();EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);EditorApplication.EnterPlaymode();
            }
            catch(Exception ex)
            {
                Debug.LogError("["+suite+"] FAIL: "+ex);
                SessionState.SetBool(Key+"active",false);EditorApplication.Exit(1);
            }
        }
        private static uint HeaderUInt32(byte[] bytes,int offset)
            =>((uint)bytes[offset]<<24)|((uint)bytes[offset+1]<<16)|((uint)bytes[offset+2]<<8)|bytes[offset+3];
        private static void ValidateDemoNavigationAsset()
        {
            const string asset="Assets/scenes/demo_Island/NavMesh-NavMesh 1.asset";
            const string expectedGuid="0208fa9d8b15e4ddaae8e6511229855e";
            string path=Path.Combine(Application.dataPath,"scenes/demo_Island/NavMesh-NavMesh 1.asset");
            byte[] header;long actualSize;
            using(var stream=File.OpenRead(path))
            using(var reader=new BinaryReader(stream)){actualSize=stream.Length;header=reader.ReadBytes(48);}
            if(header.Length<20)throw new InvalidDataException("导航资源头被截断："+asset);
            uint version=HeaderUInt32(header,8);
            ulong metadataSize=HeaderUInt32(header,0),fileSize=HeaderUInt32(header,4),dataOffset=HeaderUInt32(header,12);
            ulong headerSize=20;
            if(version==22)
            {
                if(header.Length<48)throw new InvalidDataException("导航资源扩展头被截断："+asset);
                headerSize=48;metadataSize=HeaderUInt32(header,20);
                fileSize=((ulong)HeaderUInt32(header,24)<<32)|HeaderUInt32(header,28);
                dataOffset=((ulong)HeaderUInt32(header,32)<<32)|HeaderUInt32(header,36);
            }
            if((version!=21 && version!=22) || header[16]>1 || metadataSize==0 || fileSize!=(ulong)actualSize
                || dataOffset<headerSize+metadataSize || dataOffset>=fileSize)
                throw new InvalidDataException("导航资源完整性失败："+asset+"；文件头长度="+fileSize+"，实际长度="+actualSize+"。请按已核实的原始 Git blob 定向恢复，不能忽略导入错误。");
            if(AssetDatabase.AssetPathToGUID(asset)!=expectedGuid)throw new InvalidDataException("导航资源 GUID 已改变，拒绝替换原场景引用："+asset);
            // 只刷新这一个曾缓存错误结果的资源，不删除 Library、不重建导航，也不保存场景。
            AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            if(AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(asset)==null)throw new InvalidDataException("导航资源无法反序列化："+asset);
            Debug.Log("[MAP] CHECK: 导航资源原始头/长度/GUID 通过并已定向重导入；version="+version+" metadata="+metadataSize+" bytes="+actualSize+" dataOffset="+dataOffset);
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key+"active",false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))SessionState.SetString(Key+"error","验收超时");
            if(SessionState.GetString(Key+"error","")!="")
            {
                if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
                else if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.Exit(1);
                return;
            }
            // EditorApplication.update 不等于游戏帧；让 yield return null 真正越过帧级输入/接触锁。
            if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<_next || Time.frameCount==_lastCheckFrame)return;
            var c=UnityEngine.Object.FindObjectOfType<FishingPrototype>();if(c==null || c.WorkshopWorld==null)return;
            try
            {
                if(_checks==null)_checks=Checks(c,SessionState.GetString(Key+"suite",""));
                _next=EditorApplication.timeSinceStartup+.02;
                _lastCheckFrame=Time.frameCount;
                if(!_checks.MoveNext()){SessionState.SetBool(Key+"finished",true);EditorApplication.ExitPlaymode();}
            }
            catch(Exception ex){SessionState.SetString(Key+"error",ex.ToString());(_checks as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();}
        }
        private static void Check(bool ok,string text)
        {
            if(!ok)throw new InvalidOperationException(text);
            Debug.Log("["+SessionState.GetString(Key+"suite","")+"] CHECK: "+text);
        }
        private static IEnumerator Checks(FishingPrototype c,string suite)
        {
            if(suite=="V056")
            {
                var run=FishingV056Validation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V055")
            {
                var run=FishingV055Validation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V054")
            {
                var run=FishingV054Validation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="CLASSROOM")
            {
                var run=FishingClassroomValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V053-BOAT")
            {
                var run=FishingV053BoatValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="NPC-FACING")
            {
                var run=FishingNpcFacingValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V051-WRECK")
            {
                var run=FishingWreckRouteValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-STORY")
            {
                var run=FishingV044StoryValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-NPC")
            {
                var run=FishingV044NpcValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-SCENE")
            {
                var run=FishingV044SceneCapture.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-OVERLAP")
            {
                var run=FishingV044OverlapAudit.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-WALK")
            {
                var run=FishingV044WalkAudit.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-VOYAGE")
            {
                var run=FishingV044VoyageFloatAudit.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-PERF")
            {
                var run=FishingV044PerfValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-SKY")
            {
                var run=FishingV044SkyValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-LIFE")
            {
                var run=FishingV044LifeValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-TIDE")
            {
                var run=FishingV044TideValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-SEA")
            {
                var run=FishingV044SeaValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V044-REALMS")
            {
                var run=FishingV044RealmValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V043")
            {
                var run=FishingV043Validation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V042")
            {
                var run=FishingV042Validation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"仅操作临时隔离存档");
            int firstFrame=Time.frameCount;while(Time.frameCount==firstFrame)yield return null;
            if(suite=="COAST-ART")
            {
                var run=FishingCoastArtValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="VOYAGE-QUEST")
            {
                var run=FishingVoyageValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="HOME-ECOLOGY")
            {
                var run=FishingHomeEcologyValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="FEEDBACK-P3")
            {
                var run=FishingFeedbackP3Validation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="FEEDBACK-TOOLS")
            {
                var run=FishingFeedbackToolsValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="MARINE-CAPTURE")
            {
                var run=FishingMarineCaptureValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="FEEDBACK-MOTION")
            {
                var run=FishingFeedbackP3Validation.MotionChecks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="COMBAT")
            {
                var run=FishingCombatValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="WATER")
            {
                var run=FishingWaterValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="P0-ART")
            {
                var run=FishingP0ArtValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="PORTABLE-CHART")
            {
                var run=FishingPortableChartValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="WORLD-AUDIO")
            {
                var run=FishingWorldAudioValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="AUDIO-MIX")
            {
                var run=FishingAudioMixValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="REGIONAL-MUSIC")
            {
                var run=FishingRegionalMusicValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="ACTION-AUDIO")
            {
                var run=FishingActionAudioValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V039-REALM")
            {
                var checks=FishingV039Validation.Realm(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V039-HOME")
            {
                var checks=FishingV039Validation.Home(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V039")
            {
                var checks=FishingV039Validation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V038")
            {
                var checks=FishingV038Validation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V038-SWIMMING")
            {
                var checks=FishingV038Validation.Swimming(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V037-HANDS")
            {
                var checks=FishingV037HandsValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V037-PORTAL")
            {
                var checks=FishingV037PortalValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V037-WEATHER")
            {
                var checks=FishingV037WeatherValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V037-UI")
            {
                var checks=FishingV037UIValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V036")
            {
                var checks=FishingV036Validation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V036-UI")
            {
                var checks=FishingV036UIValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V036-STORAGE")
            {
                var checks=FishingV036StorageValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V036-PORTAL")
            {
                var checks=FishingV036PortalValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V036-ORCHARD")
            {
                var checks=FishingV036OrchardValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V036-PERFORMANCE")
            {
                var checks=FishingV036PerformanceValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V035-LEVELS")
            {
                var checks=FishingV035Levels.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V035-FARM")
            {
                var checks=FishingV035FarmValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V036-FARM")
            {
                var checks=FishingV036FarmValidation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V035")
            {
                var checks=FishingV035Validation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V034")
            {
                var checks=FishingV034Validation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="MOUSE-FISHING")
            {
                var run=FishingMouseFishingValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="ANGLING")
            {
                var run=FishingAnglingValidation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V033")
            {
                var checks=FishingV033Validation.Checks(c);try{while(checks.MoveNext())yield return checks.Current;}finally{(checks as IDisposable)?.Dispose();}yield break;
            }
            if(suite=="V032")
            {
                var checks=FishingV032Validation.Checks(c);while(checks.MoveNext())yield return checks.Current;yield break;
            }
            if(suite=="V031")
            {
                var run=FishingV031Validation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as System.IDisposable)?.Dispose();}
            }
            else if(suite=="V030")
            {
                var run=FishingV030Validation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}
            }
            else if(suite=="V029")
            {
                var run=FishingV029Validation.Checks(c);try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}

            }
            else if(suite=="V028")
            {
                foreach(var run in new[]{FishingV028UIValidation.Checks(c),FishingV028ToolsSleepValidation.Checks(c),FishingV028CoastValidation.Checks(c)})
                {
                    try{while(run.MoveNext())yield return run.Current;}
                    finally{(run as IDisposable)?.Dispose();}
                }
            }
            else if(suite=="V028-UI")
            {
                var run=FishingV028UIValidation.Checks(c);while(run.MoveNext())yield return run.Current;
            }
            else if(suite=="V028-COAST")
            {
                var run=FishingV028CoastValidation.Checks(c);while(run.MoveNext())yield return run.Current;
            }
            else if(suite=="V028-TOOLS-SLEEP")
            {
                var run=FishingV028ToolsSleepValidation.Checks(c);while(run.MoveNext())yield return run.Current;
            }
            else if(suite=="CASHIER-DIALOGUE")
            {
                var run=FishingCashierDialogueValidation.Checks(c);while(run.MoveNext())yield return run.Current;
            }
            else if(suite=="LANTERN")
            {
                var run=FishingLanternValidation.Checks(c);while(run.MoveNext())yield return run.Current;
            }
            else if(suite=="PICKAXE")
            {
                var run=FishingPickaxeValidation.Checks(c);while(run.MoveNext())yield return run.Current;
            }
            else if(suite=="V027")
            {
                var run=FishingV027Validation.Checks(c);
                try{while(run.MoveNext())yield return run.Current;}
                finally{(run as IDisposable)?.Dispose();}
            }
            else if(suite=="V026")
            {
                var run=FishingV026Validation.Checks(c);
                try{while(run.MoveNext())yield return run.Current;}
                finally{(run as IDisposable)?.Dispose();}
            }
            else if(suite=="V025")
            {
                var run=FishingV025Validation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="V024")
            {
                var run=FishingV024Validation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="V023")
            {
                var run=FishingV023Validation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="V022")
            {
                var run=FishingV022Validation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="V021")
            {
                var run=FishingV021Validation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="V020")
            {
                var run=FishingV020Validation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="V019")
            {
                var run=FishingV019Validation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="LIVING-COAST")
            {
                var run=FishingLivingCoastValidation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="V018")
            {
                var run=FishingV018Validation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="V017")
            {
                var run=FishingV017Validation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="V016")
            {
                var run=FishingV016Validation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="MAP")
            {
                var run=Map(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="CHARACTER")
            {
                var run=Character(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="INTERACTION")
            {
                var run=Interactions(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="FIRST-PERSON")
            {
                var run=FishingFirstPersonValidation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="HUNTING")
            {
                var run=FishingHuntingValidation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="FORGE")
            {
                var run=FishingForgeValidation.Checks(c);while(run.MoveNext())yield return null;
            }
            else if(suite=="PLAYER")
            {
                var run=FishingPlayerRepairValidation.Checks(c);while(run.MoveNext())yield return null;
            }
            else throw new ArgumentException("未知 v0.11 套件："+suite);
        }
        private static IEnumerator Map(FishingPrototype c)
        {
            Check(c.ShopStaticRoot!=null,"商店安装必须先于地图烘焙");
            var scene=SceneManager.GetActiveScene();int roots=scene.rootCount;bool wasPaused=c.Paused;
            if(!wasPaused)c.TogglePause();yield return null;
            try
            {
                BoatAttack.Editor.FishingMapBatchValidation.Run();
                Check(true,"静态合批子集/变换、像素位置、GPU 网格与 Terrain holes 回归完成");
                var map=BoatAttack.Editor.FishingMapBaker.BakeInitializedScene(new[]{c.ShopStaticRoot},0,2048);
                Check(map!=null && map.IsUsable && map.includesInitializedScenery,"初始化捕鱼场景的地图资源可用");
                Check(map.terrainCount>=2 && map.staticRendererCount>0 && map.terrainVertexCount>100,"实际 Terrain 与静态可视网格参与烘焙");
                Check(map.sources.Any(s=>s.Contains(c.ShopStaticRoot.name)) && map.sources.Any(s=>s.Contains("V03 habitat")),"商店与运行时礁区均进入来源清单");
                Check(scene.rootCount==roots && scene.path==FishingPrototypeEditor.ScenePath,"烘焙未改写源场景根对象");
                foreach(var p in new[]{c.harbor,c.ShopPosition,c.Shore.Sites[0].Landing,c.fishingSpots[3]})
                {
                    Check(FishingMapMath.TryWorldToUV(map.Bounds,p.x,p.z,out var u,out var v) && FishingMapMath.KnownUV(u,v),"关键地标位于实际采样覆盖内 "+p);
                    FishingMapMath.TryUVToWorld(map.Bounds,u,v,out var x,out var z);
                    Check(Math.Abs(x-p.x)<.001 && Math.Abs(z-p.z)<.001,"地标 UV 往返无偏移");
                }
                var image=new Texture2D(2,2);
                try
                {
                    Check(image.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/FishingMap.png"))),"新 PNG 可解码");
                    var pixels=image.GetPixels32();var colors=new HashSet<int>();
                    for(int i=0;i<pixels.Length;i+=17){var p=pixels[i];colors.Add((p.r/8)<<10 | (p.g/8)<<5 | p.b/8);}
                    Check(colors.Count>8,"真实高程图不是空白或二值填充；人工仍须检查海岸/标记对齐");
                }
                finally{UnityEngine.Object.Destroy(image);}
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1366,768),new Vector2Int(2560,1080),new Vector2Int(1280,1024)})
                {
                    float scale=FishingHUD.LayoutScale(size.x,size.y),height=size.y/scale,offset=(size.x-1600*scale)*.5f;
                    var chart=new FishingHUD.FocusLayout(height,false,CatchPhase.Idle).Chart;
                    Check(chart.height>=130+FishingHUD.TerrainMapRadius+12+36,"图例拥有完整 36 逻辑像素空间");
                    var point=new Vector2(offset+(chart.x+8)*scale,size.y-(chart.yMax-8)*scale);
                    Check(FishingHUD.BlocksToolPointer(point,size.x,size.y),"图例区域不穿透鼠标输入 "+size);
                }
                c.GetComponent<FishingHUD>().ReleaseTerrainMap();yield return null;
            }
            finally{if(!wasPaused && c.Paused)c.TogglePause();}
        }
        private static void Boat(FishingPrototype c,Vector3 p)
        {
            var rb=c.Player.engine.RB;rb.position=p;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(p,Quaternion.identity);Physics.SyncTransforms();
        }
        private static void Feet(FishingPrototype c,Vector3 p,Vector3 towards)
        {
            c.Shore.Body.enabled=false;var d=Vector3.ProjectOnPlane(towards-p,Vector3.up);
            c.Shore.Actor.SetPositionAndRotation(p,d.sqrMagnitude>.001f?Quaternion.LookRotation(d):Quaternion.identity);
            c.Shore.Body.enabled=true;c.View.ResetLook();Physics.SyncTransforms();c.Shore.Body.Move(Vector3.down*.12f);
        }
        private static void Equip(FishingPrototype c,FieldItem item)
        {
            var bag=c.Career.Backpack;
            if(bag.Count(item)==0)Check(bag.TryAdd(item,1),"装备夹具加入 "+item);
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(bag.At(i).item==item){if(i!=0)bag.Move(i,0);break;}
            c.SelectFieldSlot(0);
        }
        private static IEnumerator Land(FishingPrototype c)
        {
            c.ReturnToHarbor();Boat(c,c.Shore.Sites[0].Berth+Vector3.up*.6f);
            Check(c.Shore.TryDisembark(),"沿原四岸点的实际入口登陆");
            for(int i=0;i<10;i++)yield return null;
        }
        private static void HandOnGoal(FishingPrototype c,bool left,string label)
        {
            var goal=left?c.Hands.Left:c.Hands.Right;var character=c.Shore.Character;
            Check(goal.Visible && Vector3.Distance(character.PalmPosition(left),goal.transform.position)<.035f,label+"：真实蒙皮掌心到达独立目标");
            Check(c.Hands.CanReach(left,goal.transform.position,-.005f),label+"：目标未依赖 IK 夹短补救");
        }
        private static IEnumerator Character(FishingPrototype c)
        {
            FishingCharacterValidation.CheckSource(c);
            var expiry=FishingCharacterValidation.CheckTargetExpiry();while(expiry.MoveNext())yield return null;
            c.MoveToSpotForAutomation(0);c.Interact();c.PresentCharacterFrame();
            FishingCharacterValidation.CheckPoses(c);FishingCharacterValidation.CheckVisibility(c,true);
            HandOnGoal(c,true,"卷线器");HandOnGoal(c,false,"钓竿");
            foreach(var hand in new[]{c.Hands.Left,c.Hands.Right})
            {
                var viewport=c.viewCamera.WorldToViewportPoint(hand.Palm.position);
                Check(viewport.z>.06f && viewport.x>.05f && viewport.x<.95f && viewport.y>.1f && viewport.y<.6f,
                    "第一人称真实持竿双掌留在下方可见操作区，避免遮挡中心："+viewport);
            }
            c.ToggleView();c.PresentCharacterFrame();FishingCharacterValidation.CheckVisibility(c,false);
            Check(Vector3.Distance(c.Hands.Rod.position,c.Shore.Character.HandReferencePosition)<1,"第三人称钓竿仍位于肩系而非相机前");
            var land=Land(c);while(land.MoveNext())yield return null;
            foreach(var item in new[]{FieldItem.FieldGuide,FieldItem.Binoculars,FieldItem.Lantern,FieldItem.StonePickaxe,FieldItem.IronPickaxe})
            {
                Equip(c,item);yield return null;
                if(item==FieldItem.Lantern)Check(c.InspectFieldItem(c.Career.Backpack.Selected),"显式拿起检视腰挂灯");
                for(int view=0;view<2;view++)
                {
                    if(c.FirstPerson!=(view==0))c.ToggleView();c.PresentCharacterFrame();
                    Check(c.FieldWorld.HeldTool.gameObject.activeInHierarchy && c.FieldWorld.PresentedItem==item,"同一物品模型在两种视角可见 "+item);
                    HandOnGoal(c,false,item+"右手");HandOnGoal(c,true,item+"左手/休息位");
                    Check(Vector3.Distance(c.FieldWorld.HeldTool.position,c.Shore.Character.HandReferencePosition)<1.2f,"工具没有跟随远处第三人称相机");
                }
                if(item==FieldItem.Lantern)c.EndItemInspection();
                if(item==FieldItem.Binoculars)
                {
                    c.UseFieldItem();
                    for(int step=0;step<100 && c.View.BinocularBlend<.999f;step++)yield return null;
                    c.PresentCharacterFrame();
                    Check(c.BinocularsRaised && c.FieldWorld.HeldTool.gameObject.activeInHierarchy && c.FieldWorld.HeldToolRenderer.enabled
                        && Mathf.Abs(c.viewCamera.fieldOfView-58)<.01f,"第三人称举镜保留实物，不套用光学缩放");
                    c.ToggleView();c.PresentCharacterFrame();
                    Check(c.BinocularsRaised && c.View.OpticalView && !c.FieldWorld.HeldToolRenderer.enabled
                        && Mathf.Abs(c.viewCamera.fieldOfView-FishingViewRig.BinocularFieldOfView)<.01f,"第一人称进入无遮挡光学观察，切视角不放下物品");
                    c.UseFieldItem();
                }
            }
            c.ReturnToHarbor();c.MoveToSpotForAutomation(0);c.Interact();
            for(int i=0;i<10;i++)yield return null;
            Check(c.Cast(),"抄网夹具从生产抛竿流程开始");int guard=0;
            while(c.Session.Phase==CatchPhase.Waiting && guard++<1000)c.Session.Tick(.02f,false);
            Check(c.Hook(),"夹具提竿");guard=0;
            while(c.Session.Phase==CatchPhase.Fighting && guard++<6000)c.Session.Tick(.02f,c.Session.Tension<.6f);
            Check(c.Session.Phase==CatchPhase.Landing && c.Scoop(),"进入真实抄网辅助阶段");c.PresentCharacterFrame(.02f);
            Check(!c.HasPendingCatch && c.Hands.Left.Visible,"未入网时也必须供应左手网目标");
            HandOnGoal(c,true,"抄网手柄");
            Check(Vector3.Distance(c.Hands.Left.transform.position,c.LandingNet.TransformPoint(new Vector3(.05f,.035f,0)))<.001f,"左手握点跟随最终网姿态");
            c.ReturnToHarbor();
        }
        private static bool StabilizeInteractionPose(FishingPrototype c,bool geometryOnly=false,bool crouched=true)
        {
            c.Shore.SetCrouch(crouched);int stable=0;
            // Automated 的 Tick(false) 不执行重力；显式走正常 Move，不能靠等待帧或写 Grounded。
            for(int step=0;step<30 && stable<3;step++)
            {
                var before=c.Shore.Actor.position;
                if(geometryOnly && c.WorldActionBusy)
                    // 故意移离锁定目标的夹具：Shore.Move 会被动作锁拦截，只以真实胶囊碰撞落地；不解锁/取消动作。
                    c.Shore.Body.Move(Vector3.down*(2f/60f));
                else c.Shore.Move(Vector2.zero,false,false,crouched,1f/60f);
                Physics.SyncTransforms();
                stable=c.Shore.Grounded && c.Shore.Crouched==crouched && (c.Shore.Actor.position-before).sqrMagnitude<.000001f?stable+1:0;
            }
            c.PresentCharacterFrame();Physics.SyncTransforms();
            return stable>=3 && c.Shore.Grounded && c.Shore.Crouched==crouched;
        }
        private static FishingPrototype.InteractionPreflight RefreshInteractionPreflight(FishingPrototype c)
        {
            // 仅刷新已有感知缓存，绝不通过请求/取消动作来筛选样本。
            c.WorkshopWorld.Sense();c.FieldWorld.Sense();c.Shore.UpdateProximity();
            return c.InspectWorldInteractionStart();
        }
        private static void RequestInteraction(FishingPrototype c,string expectedId,string label,bool harvest=false,bool crouched=true)
        {
            bool stable=StabilizeInteractionPose(c,crouched:crouched);var check=RefreshInteractionPreflight(c);
            bool began=harvest?c.RequestHarvest():c.RequestWorldInteraction();
            string actualId=c.Actions.TargetId;
            string detail="; returned="+began+" actual="+actualId+" expected="+expectedId+" message="+c.Message+" stable="+stable
                +" actorNow="+c.Shore.Actor.position.ToString("F4")+" preflight={"+check+"}";
            // 返回值与目标锁定分别断言；失败仍保留请求前几何，而不是被起手后的 busy 状态覆盖。
            Check(began,label+"：请求返回 true"+(began?"":detail));
            Check(actualId==expectedId,label+"：锁定 "+expectedId+(actualId==expectedId?"":detail));
            bool ready=stable && check.Grounded && check.Crouched==crouched && check.CanBegin;
            Check(ready,label+"：实际落地/蹲姿及共享预检通过"+(ready?"":detail));
        }
        private static FishingWorkshopWorld.ResourceNode PrepareResource(FishingPrototype c,int except=-1,bool geometryOnly=false)
        {
            long actionId=c.Actions.ActionId;float progress=c.Actions.Progress;int commits=c.ActionCommitAttempts;
            var lockedContact=c.ActionTargetPosition;var rejected=new List<string>();
            if(geometryOnly && !c.WorldActionBusy)throw new InvalidOperationException("移离目标夹具必须保留原活动动作");
            foreach(var node in c.WorkshopWorld.Nodes)
            {
                if(node.Root==null || !node.Root.gameObject.activeInHierarchy || node.Item!=FieldItem.Stone || node.Id==except || !c.Career.Workshop.ResourceReady(node.Id))continue;
                string expected="resource:"+node.Id,last="没有通过 Ground/Clearance 的站位";
                for(int pose=0;pose<24;pose++)
                {
                    var seed=node.Feet+Quaternion.Euler(0,(pose%8)*45,0)*Vector3.back*(.35f+(pose/8)*.2f)+Vector3.up*.08f;
                    if(!c.Shore.Ground(seed,out var ground) || !c.Shore.Clearance(ground))continue;
                    Feet(c,ground+Vector3.up*.08f,node.Feet);
                    bool stable=StabilizeInteractionPose(c,geometryOnly);var check=RefreshInteractionPreflight(c);
                    float oldDistance=Vector3.Distance(check.BaseShoulder,lockedContact);
                    // 另一个最近候选不一定已离开旧接触点；连生产最大 22cm 俯身也不能再触及旧目标。
                    bool leftOriginal=!geometryOnly || oldDistance>check.ArmReach+.22f;
                    last="stable="+stable+" geometryOnly="+geometryOnly+" oldDistance="+oldDistance.ToString("F4")+" preflight={"+check+"}";
                    if(!stable || check.TargetId!=expected || !(geometryOnly?check.GeometryReady:check.CanBegin) || !leftOriginal)continue;
                    Check(check.Grounded && check.Crouched,"石料夹具通过真实碰撞确认 Grounded/Crouched："+expected);
                    Check(c.Actions.ActionId==actionId && c.Actions.Progress==progress && c.ActionCommitAttempts==commits,"站位预检未启动、推进或提交动作");
                    return node;
                }
                rejected.Add("expected="+expected+" "+last);
            }
            throw new InvalidOperationException("找不到满足全类别候选/真实可达性的石料夹具；message="+c.Message+"\n"+string.Join("\n",rejected));
        }
        private static IEnumerator SettleInteractionStance(FishingPrototype c,bool crouched)
        {
            c.Shore.SetCrouch(crouched);
            // 碰撞体立刻变矮，但骨架按真实帧平滑过渡；同帧重复 Pose 不能代替等待。
            for(int frame=0;frame<120;frame++)
            {
                c.Shore.Move(Vector2.zero,false,false,crouched,1f/60f);
                c.PresentCharacterFrame();
                if(c.Shore.Grounded && c.Shore.Character.IsStanceSettled(true,crouched))
                {
                    Check(true,"真实跨帧姿态与落地已稳定，crouched="+crouched);
                    yield break;
                }
                yield return null;
            }
            throw new InvalidOperationException("真实姿态或落地未在120帧内稳定，crouched="+crouched);
        }
        private static IEnumerator SettleFishingSeat(FishingPrototype c)
        {
            float deadline=Time.realtimeSinceStartup+3f;int frames=0;
            do
            {
                c.PresentCharacterFrame();
                if(c.Shore.Character.IsStanceSettled(false,false))
                {
                    Check(true,"鱼获夹具等待真实坐姿落位，帧数="+frames);
                    yield break;
                }
                frames++;yield return null;
            }while(Time.realtimeSinceStartup<deadline);
            throw new InvalidOperationException("鱼获夹具坐姿未在真实帧中落位");
        }
        private static IEnumerator Interactions(FishingPrototype c)
        {
            var original=c.Career.Snapshot();var land=Land(c);while(land.MoveNext())yield return null;
            var crouch=SettleInteractionStance(c,true);while(crouch.MoveNext())yield return null;
            var node=PrepareResource(c);
            int before=c.Career.Backpack.Count(FieldItem.Stone);
            RequestInteraction(c,"resource:"+node.Id,"E 使用真实动作请求并锁定资源 ID");
            long id=c.Actions.ActionId;
            Check(!c.RequestWorldInteraction() && c.Actions.ActionId==id,"重复 E 不替换动作");
            c.TickWorldAction(.1f,true,false);Check(c.Career.Backpack.Count(FieldItem.Stone)==before,"起手阶段不提前发料");
            c.ToggleView();Check(c.Actions.ActionId==id,"切换视角不重放或取消动作");
            c.TickWorldAction(5,true,false);c.PresentCharacterFrame();
            Check(c.LastActionSucceeded && c.Career.Backpack.Count(FieldItem.Stone)==before+3,"低帧率跨过接触/结束仍只提交一次");HandOnGoal(c,false,"徒手接触");
            int attempts=c.ActionCommitAttempts;c.TickWorldAction(5,true,false);
            Check(c.ActionCommitAttempts==attempts && c.Career.Backpack.Count(FieldItem.Stone)==before+3 && !c.Actions.TryConsumeContact(id),"已消费许可不会重复收益");
            yield return null;c.TickWorldAction(.01f,true,false);
            for(int i=0;i<90;i++)c.Career.Workshop.Tick(1);
            node=PrepareResource(c);
            before=c.Career.Backpack.Count(FieldItem.Stone);
            RequestInteraction(c,"resource:"+node.Id,"目标切换回归起手");id=c.Actions.ActionId;
            var another=PrepareResource(c,node.Id,geometryOnly:true);
            Check(c.Actions.ActionId==id && c.Actions.TargetId=="resource:"+node.Id,"移离后仍锁定原动作编号和资源 ID");
            c.TickWorldAction(1,true,false);
            Check(!c.LastActionSucceeded && c.Career.Backpack.Count(FieldItem.Stone)==before && c.Career.Workshop.ResourceReady(another.Id),"走近另一矿点不会把旧许可改投最近资源");
            yield return null;
            node=PrepareResource(c);
            RequestInteraction(c,"resource:"+node.Id,"暂停回归起手");id=c.Actions.ActionId;c.TogglePause();c.TickWorldAction(1,true,false);
            Check(c.Actions.ActionId==0 && !c.Actions.TryConsumeContact(id) && c.Career.Backpack.Count(FieldItem.Stone)==before,"暂停取消未接触动作");c.TogglePause();yield return null;
            RequestInteraction(c,"resource:"+node.Id,"失焦回归起手");c.SendMessage("OnApplicationFocus",false);c.TickWorldAction(1,true,false);
            Check(!c.WorldActionBusy && c.Career.Backpack.Count(FieldItem.Stone)==before,"失焦不遗留接触回调");yield return null;
            RequestInteraction(c,"resource:"+node.Id,"换槽回归起手");c.SelectFieldSlot(1);c.TickWorldAction(1,true,false);
            Check(!c.WorldActionBusy && c.Career.Backpack.Count(FieldItem.Stone)==before,"切换装备取消旧快照");c.SelectFieldSlot(0);yield return null;
            RequestInteraction(c,"resource:"+node.Id,"XR 边界回归起手");c.TickWorldAction(1,true,true);Check(!c.WorldActionBusy,"模拟 XR 激活取消桌面动作");yield return null;
            RequestInteraction(c,"resource:"+node.Id,"消失目标回归起手");id=c.Actions.ActionId;
            // 只移除隔离 Play Mode 中生成的节点实例，不改写任何资源文件或场景。
            UnityEngine.Object.DestroyImmediate(node.Root.gameObject);c.TickWorldAction(1,true,false);
            Check(!c.WorldActionBusy && !c.Actions.TryConsumeContact(id) && c.Career.Backpack.Count(FieldItem.Stone)==before,"目标消失作废旧许可，不转投其他资源");yield return null;
            node=PrepareResource(c);
            RequestInteraction(c,"resource:"+node.Id,"读档回归起手");id=c.Actions.ActionId;var saved=c.Career.Snapshot();c.RestoreShopTestProfile(saved);
            Check(!c.WorldActionBusy && !c.Actions.TryConsumeContact(id),"读档使旧动作编号失效");yield return null;
            var full=c.Career.Snapshot();for(int i=0;i<full.backpack.Count;i++)full.backpack[i]=FishingRecipes.Stack(FieldItem.Shell,64);
            full.backpack[0]=FishingRecipes.Stack(FieldItem.FieldGuide,1);full.selectedSlot=0;c.RestoreShopTestProfile(full);
            node=PrepareResource(c);
            RequestInteraction(c,"resource:"+node.Id,"满包回归仍走接触请求");c.TickWorldAction(1,true,false);
            Check(!c.LastActionSucceeded && c.Career.Workshop.ResourceReady(node.Id) && c.Career.Backpack.Count(FieldItem.Stone)==0,"满包失败不消耗资源");yield return null;
            c.RestoreShopTestProfile(original);Equip(c,FieldItem.StonePickaxe);yield return null;
            node=PrepareResource(c);
            RequestInteraction(c,"resource:"+node.Id,"F 镐采集使用同一动作时钟",harvest:true);c.TickWorldAction(1,true,false);c.PresentCharacterFrame();
            Check(c.LastActionSucceeded && Vector3.Distance(c.FieldWorld.HeldTool.TransformPoint(FishingFieldWorld.PickTip),c.ActionTargetPosition)<.025f,"镐尖与收益接触点一致");HandOnGoal(c,false,"镐柄接触");
            yield return null;c.TickWorldAction(.01f,true,false);c.RestoreShopTestProfile(original);
            var pickup=c.FieldWorld.Pickups.First(p=>p.Root!=null && !c.Career.Backpack.Collected(p.Id));
            var pickupSeed=pickup.Feet+Vector3.back*.3f;
            Check(c.Shore.Ground(pickupSeed,out var pickupGround),"拾取夹具使用实际岸面");Feet(c,pickupGround+Vector3.up*.08f,pickup.Root.position);
            int pickupBefore=c.Career.Backpack.Count(pickup.Item);
            RequestInteraction(c,"pickup:"+pickup.Id,"第三人称 E 选择更近的拾取而非远处资源");
            Check(!c.Career.Backpack.Collected(pickup.Id),"拾取起手不提前置领取位");c.TickWorldAction(1,true,false);c.PresentCharacterFrame();
            Check(c.LastActionSucceeded && c.Career.Backpack.Collected(pickup.Id) && c.Career.Backpack.Count(pickup.Item)==pickupBefore+pickup.Count,"拾取接触只提交已锁定物品");
            HandOnGoal(c,false,"拾取接触");yield return null;c.TickWorldAction(.01f,true,false);
            Equip(c,FieldItem.Workbench);yield return null;bool placing=false;
            var standing=SettleInteractionStance(c,false);while(standing.MoveNext())yield return null;
            int stationsBefore=c.Career.Workshop.Stations.Count();
            for(int attempt=0;attempt<80 && !placing;attempt++)
            {
                if(!c.FieldWorld.TryLocation(c.Shore.Sites[0].Landing,8+attempt/8,attempt*137.5f,out var foot))continue;
                var direction=Quaternion.Euler(0,attempt*41,0)*Vector3.forward;
                Feet(c,foot+Vector3.up*.05f,foot+direction);if(!StabilizeInteractionPose(c,crouched:false))continue;
                placing=c.RequestPlaceEquipped();
            }
            Check(placing && c.Career.Workshop.Stations.Count()==stationsBefore && c.Career.Backpack.Count(FieldItem.Workbench)==1,
                "落位预览阶段不扣除设施；placing="+placing+" stations="+c.Career.Workshop.Stations.Count()+" expectedStations="+stationsBefore
                +" count="+c.Career.Backpack.Count(FieldItem.Workbench)+" message="+c.Message+" actor="+c.Shore.Position);
            c.CancelWorldAction();Check(c.Career.Backpack.Count(FieldItem.Workbench)==1,"取消放置无损退回原物品");yield return null;
            Check(c.RequestPlaceEquipped(),"同一合法落位可以重新请求");c.TickWorldAction(1,true,false);c.PresentCharacterFrame();
            Check(c.LastActionSucceeded && c.Career.Workshop.Stations.Count()==stationsBefore+1 && c.Career.Backpack.Count(FieldItem.Workbench)==0,"接触时复核落位并只生成一个设施");
            yield return null;c.TickWorldAction(.01f,true,false);c.Career.Expedition.Accept();c.Career.Expedition.ReadRecord();c.Career.Expedition.FindComponent();c.Expedition.RefreshFromCareer();
            var beaconSeed=c.Shore.Sites[2].Tasks[1]+Vector3.forward*.65f;
            Check(c.Shore.Ground(beaconSeed,out var beaconGround) && c.Shore.Clearance(beaconGround),"维修夹具位于旧港实际可站立岸面");
            Feet(c,beaconGround+Vector3.up*.08f,c.Expedition.BeaconPosition);
            RequestInteraction(c,"expedition:4","维修入口使用实际接触请求",crouched:false);
            Check(c.Actions.Kind==FishingActionKind.Repair,"维修入口锁定 Repair 动作类型");
            c.TickWorldAction(.1f,true,false);Check(!c.Expedition.Aligning,"接触前不打开信标校准");c.TickWorldAction(1,true,false);
            Check(c.LastActionSucceeded && c.Expedition.Aligning && !c.Career.Expedition.Has(FishingExpedition.BeaconRepaired),"接触只打开原校准事务，不提前修复或消耗线圈");
            c.Expedition.Cancel();c.CancelWorldAction();c.ReturnToHarbor();c.RestoreShopTestProfile(original);yield return null;
            // 此段验证鱼获事务和手部几何，隔离救援后的浮力瞬态；实航/抄网物理由独立套件覆盖。
            var handlingBody=c.Player.engine.RB;var handlingConstraints=handlingBody.constraints;
            handlingBody.velocity=Vector3.zero;handlingBody.angularVelocity=Vector3.zero;handlingBody.constraints=RigidbodyConstraints.FreezeAll;
            // 固定轻度横倾，防止测试只在偶然水平的船姿通过；人物仍使用生产 yaw 参考系。
            var handlingRotation=Quaternion.Euler(0,handlingBody.rotation.eulerAngles.y,12f);
            handlingBody.rotation=handlingRotation;c.Player.transform.rotation=handlingRotation;Physics.SyncTransforms();
            var fish=new CatchRecord{id="v011-contact-"+Guid.NewGuid().ToString("N"),kind=FishKind.Coral,grams=1000,value=FishingSpecies.Value(FishKind.Coral,false,1000)};
            Check(c.Career.BeginPending(fish,0,c.Player.transform.position.x,c.Player.transform.position.z),"待处理鱼事务夹具");c.CatchRig.ResetFixture();
            var seated=SettleFishingSeat(c);while(seated.MoveNext())yield return null;
            Check(!c.Career.PendingCatch.measured && c.RequestInspectCatch() && c.CatchRig.LeftHandInspecting,
                "未测量鱼获使用左手检视入口");
            Check(c.Career.PendingCatch.measured && c.Career.Count==0 && !c.WorldActionBusy,
                "左手检视开始时记录测量，鱼获仍待处理且不启动旧双手动作");
            Check(!c.RequestCatchResolution(true) && !c.RequestCatchResolution(false),"手持鱼必须先入网再结算");
            for(int frame=0;frame<100 && !c.CatchRig.LeftInspectionSettled;frame++)yield return null;
            c.PresentCharacterFrame();
            Check(c.CatchRig.LeftInspectionSettled && Vector3.Distance(c.Shore.Character.PalmPosition(true),
                c.ActiveFish.TransformPoint(c.CatchRig.LeftInspectionGrip))<.035f,"检视完成时左掌接触同一条鱼");
            Check(c.RequestInspectCatch() && c.CatchRig.PlacingInNet && !c.RequestInspectCatch(),
                "再次 E 开始放入抄网，重复 E 不重启动作");
            for(int frame=0;frame<100 && c.CatchRig.PlacingInNet;frame++)yield return null;
            Check(c.CatchRig.Location==FishingCatchRig.Place.Net && c.HasPendingCatch
                && c.Career.PendingCatch.fish.id==fish.id && c.Career.Count==0,"入网保留原凭证，不提前入箱");
            bool storing=c.RequestCatchResolution(true);
            Check(storing,"入箱使用动作请求；message="+c.Message+" box="+c.FishBox.position+" shoulder="+c.Shore.Character.ShoulderPosition(false)+" reach="+c.Shore.Character.ArmReach(false)
                +" surface="+c.CatchRig.ReceivingPoint(c.Shore.Character.ShoulderPosition(false))+" seated="+c.Shore.Character.IsStanceSettled(false,false)+" boat="+c.Player.transform.position+" euler="+c.Player.transform.eulerAngles);
            var receivingRim=c.FishBox.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f=>f.name=="Top rim" && f.transform.localPosition.x>0);
            Check(receivingRim!=null,"木箱保留右侧实体上沿");
            var rimPoint=receivingRim.transform.InverseTransformPoint(c.ActionTargetPosition);var rimBounds=receivingRim.sharedMesh.bounds;
            Check(Mathf.Abs(rimPoint.y-rimBounds.max.y)<.002f && rimPoint.x>=rimBounds.min.x-.002f && rimPoint.x<=rimBounds.max.x+.002f
                && rimPoint.z>=rimBounds.min.z-.002f && rimPoint.z<=rimBounds.max.z+.002f,"接收点位于实际箱沿上表面，不是悬空代理");
            Check(Mathf.Abs(rimPoint.z)<rimBounds.extents.z-.01f,"入箱锁定箱沿面内接收点，而非远端角点");
            id=c.Actions.ActionId;c.SendMessage("OnApplicationFocus",false);c.TickWorldAction(1,true,false);
            Check(c.HasPendingCatch && c.Career.Count==0 && !c.Actions.TryConsumeContact(id),"取消入箱保留待处理鱼");yield return null;
            Check(c.RequestCatchResolution(true),"取消后可用新编号重新入箱");c.TickWorldAction(1,true,false);
            Check(!c.HasPendingCatch && c.Career.Count==1 && !c.RequestCatchResolution(true),"鱼获事务只结算一次");
            var reloaded=FishingSaveStore.Load(c.SavePath,out var warning);
            Check(warning=="" && reloaded.Snapshot().version == FishingSaveData.CurrentVersion && reloaded.Count==1 && reloaded.Workshop.Initialized,"新动作保存鱼获与工坊数据");
            c.CancelWorldAction();yield return null;
            var releasedFish=new CatchRecord{id="v011-release-"+Guid.NewGuid().ToString("N"),kind=FishKind.Coral,grams=1000,value=FishingSpecies.Value(FishKind.Coral,false,1000)};
            Check(c.Career.BeginPending(releasedFish,0,c.Player.transform.position.x,c.Player.transform.position.z),"普通鱼放生夹具");c.CatchRig.ResetFixture();
            seated=SettleFishingSeat(c);while(seated.MoveNext())yield return null;
            int releaseCoins=c.Career.Coins,releaseCount=c.Career.Count;
            c.CatchRig.RequestRelease(true);Check(c.CatchRig.ReleaseConfirmation && !c.WorldActionBusy,"第一次 X 仅要求确认");
            c.CatchRig.RequestRelease(true);Check(c.WorldActionBusy && c.Actions.Kind==FishingActionKind.Release,"第二次 X 发起可达的真实放生动作；"+c.Message);
            long releaseId=c.Actions.ActionId;c.TickWorldAction(.1f,true,false);Check(c.HasPendingCatch,"松手前仍保留鱼获所有权");
            c.TickWorldAction(1,true,false);
            Check(c.LastActionSucceeded && !c.HasPendingCatch && c.Career.Coins==releaseCoins && c.Career.Count==releaseCount
                && !c.Actions.TryConsumeContact(releaseId),"普通鱼松手只结算一次，不产生售鱼金币");
            c.CancelWorldAction();yield return null;
            int legendGrams=FishingSpecies.Grams(FishKind.Sailfish,.5f);
            var releasedLegend=new CatchRecord{id="v011-legend-release-"+Guid.NewGuid().ToString("N"),kind=FishKind.Sailfish,grams=legendGrams,value=FishingSpecies.Value(FishKind.Sailfish,false,legendGrams)};
            Check(c.Career.BeginPending(releasedLegend,0,c.Player.transform.position.x,c.Player.transform.position.z),"传说鱼放生夹具");c.CatchRig.ResetFixture();
            seated=SettleFishingSeat(c);while(seated.MoveNext())yield return null;
            Check(!c.ResolveCatch(false) && c.HasPendingCatch,"传说鱼未测量仍不能结算");Check(c.MeasureCatch(),"先测量传说鱼");
            releaseCoins=c.Career.Coins;int legends=c.Career.LegendCatches;
            c.CatchRig.RequestRelease(true);c.CatchRig.RequestRelease(true);
            Check(c.WorldActionBusy && c.Actions.Kind==FishingActionKind.Release,"已测量传说鱼可用双 X 正常放生；"+c.Message);
            c.TickWorldAction(1,true,false);
            Check(c.LastActionSucceeded && !c.HasPendingCatch && c.Career.LegendCatches==legends+1
                && c.LastCatch.Bounty>0 && c.Career.Coins==releaseCoins+c.LastCatch.Bounty,"传说鱼放生与赏金只结算一次");
            c.CancelWorldAction();handlingBody.constraints=handlingConstraints;c.ReturnToHarbor();
        }
    }
}




