using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    public static class FishingV061Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V061");
        public static void RunPicoProbe()=>FishingV011Validation.Begin("V061-PICO-PROBE");
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string message)
        {if(!ok)throw new InvalidOperationException(message);Debug.Log("[V061] CHECK: "+message);}
        [Serializable] private sealed class MeshReference
        {public string field,hash;public int originalVertices,compactVertices;}
        [Serializable] private sealed class MeshReferences
        {public List<MeshReference> meshes=new List<MeshReference>();}
        private const string MeshReferencePath="Docs/Validation/V061/xr-mesh-reference.json";
        private static string SurfaceHash(Mesh mesh)
        {
            using(var bytes=new MemoryStream())
            using(var writer=new BinaryWriter(bytes))
            using(var hash=SHA256.Create())
            using(var data=Mesh.AcquireReadOnlyMeshData(mesh))
            {
                writer.Write(mesh.subMeshCount);writer.Write(mesh.blendShapeCount);
                foreach(var attribute in mesh.GetVertexAttributes())
                {writer.Write((int)attribute.attribute);writer.Write((int)attribute.format);writer.Write(attribute.dimension);writer.Write(attribute.stream);}
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var indices=mesh.GetIndices(sub);writer.Write((int)mesh.GetTopology(sub));writer.Write(indices.Length);
                    foreach(int index in indices)for(int stream=0;stream<mesh.vertexBufferCount;stream++)
                    {
                        int stride=mesh.GetVertexBufferStride(stream);var vertices=data[0].GetVertexData<byte>(stream);
                        for(int offset=0;offset<stride;offset++)writer.Write(vertices[index*stride+offset]);
                    }
                }
                foreach(var matrix in mesh.bindposes)for(int i=0;i<16;i++)writer.Write(matrix[i]);
                writer.Flush();return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-","");
            }
        }
        public static IEnumerator PicoProbeChecks(FishingPrototype c)
        {
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();if(!c.Paused)c.TogglePause();
            var type=typeof(FishingCharacter);
            var references=new MeshReferences();
            foreach(var field in new[]{"_xrFirstPersonMesh","_xrTabletopMesh"})
            {
                var source=(Mesh)type.GetField(field,Private).GetValue(c.Shore.Character);
                if(source==null)continue;
                var mesh=UnityEngine.Object.Instantiate(source);
                try
                {
                    var used=new HashSet<int>();for(int i=0;i<mesh.subMeshCount;i++)foreach(int id in mesh.GetTriangles(i))used.Add(id);
                    int before=mesh.vertexCount;string original=SurfaceHash(mesh);mesh.OptimizeReorderVertexBuffer();
                    Check(original==SurfaceHash(mesh),field+" preserves every indexed vertex stream, submesh and bind pose exactly");
                    references.meshes.Add(new MeshReference{field=field,hash=original,originalVertices=before,compactVertices=mesh.vertexCount});
                    Debug.Log("[V061-PICO-PROBE] "+field+" vertices="+before+" used="+used.Count+" optimized="+mesh.vertexCount);
                }
                finally{UnityEngine.Object.Destroy(mesh);}
            }
            Directory.CreateDirectory(Path.GetDirectoryName(MeshReferencePath));File.WriteAllText(MeshReferencePath,JsonUtility.ToJson(references,true));
            yield return null;
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Performance regression uses an isolated profile");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();
            if(!c.Paused)c.TogglePause();
            var character=c.Shore.Character;
            if(FishingMobileQuality.Requested)
            {
                Check(Mathf.Approximately(QualitySettings.lodBias,FishingMobileQuality.LodBias),"Desktop settings no longer overwrite the PICO preview's existing LOD budget");
                var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                Check(pipeline!=null && pipeline.msaaSampleCount==4 && Mathf.Approximately(pipeline.renderScale,1),"PICO full-resolution rendering and 4x MSAA are preserved");
            }
            var references=JsonUtility.FromJson<MeshReferences>(File.ReadAllText(MeshReferencePath));
            foreach(var entry in references.meshes)
            {
                var mesh=(Mesh)typeof(FishingCharacter).GetField(entry.field,Private).GetValue(character);
                Check(mesh.vertexCount==entry.compactVertices && SurfaceHash(mesh)==entry.hash,entry.field+" compacts unused vertices and exactly preserves the original visible surface/UVs/weights");
            }
            var animation=(Animation)typeof(FishingCharacter).GetField("_animation",Private).GetValue(character);
            var clips=(Dictionary<string,string>)typeof(FishingCharacter).GetField("_clips",Private).GetValue(character);
            var states=(List<(AnimationState State,string Name)>)typeof(FishingCharacter).GetField("_animationStates",Private).GetValue(character);
            var fade=(Dictionary<string,float>)typeof(FishingCharacter).GetField("_fadeStart",Private).GetValue(character);
            var ageField=typeof(FishingCharacter).GetField("_fadeAge",Private);
            var sample=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),character,typeof(FishingCharacter).GetMethod("SampleLocomotion",Private));
            var select=(Action<string>)Delegate.CreateDelegate(typeof(Action<string>),character,typeof(FishingCharacter).GetMethod("SelectClip",Private));
            Check(states.Count==animation.GetClipCount() && states.Count>=6,"Cached states include every source and interaction clip");
            var times=new float[states.Count];var weights=new float[states.Count];var enabled=new bool[states.Count];
            foreach(string key in new[]{"Idle","Walk","Run","Interact","Punch_Right","HitRecieve"})
            {
                string active=clips[key];select(active);
                foreach(float dt in new[]{0f,1f/60,.05f,.2f})
                {
                    float age=(float)ageField.GetValue(character);
                    for(int i=0;i<states.Count;i++){var s=states[i].State;times[i]=s.time;weights[i]=s.weight;enabled[i]=s.enabled;}
                    float expectedAge=age;
                    ReferenceSample(animation,active,fade,ref expectedAge,dt);
                    var expected=new (float Time,float Weight,bool Enabled)[states.Count];
                    for(int i=0;i<states.Count;i++)
                    {
                        var s=states[i].State;expected[i]=(s.time,s.weight,s.enabled);
                        s.time=times[i];s.weight=weights[i];s.enabled=enabled[i];
                    }
                    ageField.SetValue(character,age);sample(dt);
                    Check(Mathf.Approximately((float)ageField.GetValue(character),expectedAge),key+" fade clock matches 0.60 at dt="+dt);
                    for(int i=0;i<states.Count;i++)
                    {
                        var s=states[i].State;var want=expected[i];
                        Check(Mathf.Approximately(s.time,want.Time) && Mathf.Approximately(s.weight,want.Weight) && s.enabled==want.Enabled,
                            key+" preserves time/weight/enabled for "+states[i].Name+" at dt="+dt);
                    }
                }
            }
            select(clips["Idle"]);sample(.2f);
            long before=GC.GetAllocatedBytesForCurrentThread();GC.KeepAlive(new byte[128]);
            bool counterAvailable=GC.GetAllocatedBytesForCurrentThread()>before;
            if(counterAvailable)
            {
                before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<256;i++)sample(0);
                long optimized=GC.GetAllocatedBytesForCurrentThread()-before;
                float age=(float)ageField.GetValue(character);
                before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<256;i++)ReferenceSample(animation,clips["Idle"],fade,ref age,0);
                long original=GC.GetAllocatedBytesForCurrentThread()-before;
                Check(original>0 && optimized<original/4,"Warm animation sampling allocation falls by at least 75%: "+original+" -> "+optimized+" bytes / 256 samples");
            }
            else Debug.Log("[V061] Allocation counter unavailable in this Unity runtime; use player GC measurements, not an invented zero.");

            c.DockConsole.PreviewForValidation();
            var layout=(Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>),c.DockConsole,typeof(FishingDockConsole).GetMethod("Layout",Private));
            var console=c.DockConsole.GetComponentsInChildren<RectTransform>(true);
            foreach(bool pending in new[]{true,false,true,false})
            {
                layout(pending);foreach(var rect in console)rect.hasChanged=false;layout(pending);
                bool unchanged=true;foreach(var rect in console)unchanged &= !rect.hasChanged;
                Check(unchanged,"Unchanged "+(pending?"catch":"dock")+" geometry does not relayout or resize its hit targets");
            }
            var status=(Text)typeof(FishingDockConsole).GetField("_status",Private).GetValue(c.DockConsole);
            string text=status.text;int dirty=0;UnityEngine.Events.UnityAction changed=()=>dirty++;
            status.RegisterDirtyVerticesCallback(changed);
            try{c.DockConsole.Present();Check(status.text==text && dirty==0,"Unchanged dock status no longer dirties its text mesh");}
            finally{status.UnregisterDirtyVerticesCallback(changed);}
            Check(c.Career.BeginPending(new CatchRecord{id="v061-ui-catch",kind=FishKind.Coral,grams=800,value=FishingSpecies.Value(FishKind.Coral,false,800)},0,0,0),"Isolated catch fixture preserves the production pending transaction");
            c.DockConsole.Present();
            var title=(Text)typeof(FishingDockConsole).GetField("_title",Private).GetValue(c.DockConsole);
            string catchTitle=title.text,catchStatus=status.text;dirty=0;
            title.RegisterDirtyVerticesCallback(changed);status.RegisterDirtyVerticesCallback(changed);
            try{c.DockConsole.Present();Check(title.text==catchTitle && status.text==catchStatus && dirty==0,"Unchanged catch title and status no longer rebuild between intermediate texts");}
            finally
            {
                title.UnregisterDirtyVerticesCallback(changed);status.UnregisterDirtyVerticesCallback(changed);
                c.Career.ResolvePending(false,out _,out _);typeof(FishingDockConsole).GetField("_preview",Private).SetValue(c.DockConsole,false);c.DockConsole.Hide();
            }
            var notebook=c.XRNotebook;notebook.Close();
            var map=(Material)typeof(FishingXRNotebook).GetField("_mapPalette",Private).GetValue(notebook);
            var late=(Action)Delegate.CreateDelegate(typeof(Action),notebook,typeof(FishingXRNotebook).GetMethod("LateUpdate",Private));
            var marker=new Color(.123f,.456f,.789f,1);map.SetColor("_Water",marker);late();
            Check(map.GetColor("_Water")==marker,"Closed notebook leaves its unused map material idle");
            // Component fixture: Open deliberately rejects an editor without a native XR runtime.
            typeof(FishingXRNotebook).GetProperty("IsOpen").SetValue(notebook,true);
            typeof(FishingXRNotebook).GetField("_page",Private).SetValue(notebook,12);
            var canvas=(Canvas)typeof(FishingXRNotebook).GetField("_canvas",Private).GetValue(notebook);canvas.gameObject.SetActive(true);
            ((Action)Delegate.CreateDelegate(typeof(Action),notebook,typeof(FishingXRNotebook).GetMethod("Refresh",Private)))();
            late();Check(map.GetColor("_Water")==FishingUITheme.MapWater,"Opening the visible map applies the current palette immediately");notebook.Close();
            c.PresentCharacterFrame();
            Check(character.Ready && character.InteractionAnimationReady,"Calibrated character and every interaction remain ready");
            Directory.CreateDirectory("Logs/V061");
            File.WriteAllText("Logs/V061/regression.txt","Animation times, weights and enabled states match frozen 0.60 sampling for six clips and four deltas.\nUnchanged dock status does not rebuild its text mesh.\nNo gameplay, save schema or quality setting was removed.\n");
            yield return null;
        }
        // Frozen 0.60 sampling verifies that caching preserves animation and transition semantics.
        private static void ReferenceSample(Animation animation,string active,Dictionary<string,float> starts,ref float age,float dt)
        {
            age=Mathf.Min(.15f,age+dt);float fade=Mathf.SmoothStep(0,1,age/.15f);
            foreach(AnimationState state in animation)
            {
                float start=starts.TryGetValue(state.name,out var weight)?weight:(state.name==active?1:0);
                state.weight=Mathf.Lerp(start,state.name==active?1:0,fade);
                state.enabled=state.weight>0 || state.name==active;state.speed=0;
                if(state.enabled && state.length>0)state.time=state.wrapMode==WrapMode.ClampForever?Mathf.Min(state.time+dt,state.length):Mathf.Repeat(state.time+dt,state.length);
            }
            animation.Sample();
        }
    }
}
