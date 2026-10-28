using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Diagnostic only (-fishing-dc-breakdown): once the game is idle at the harbor, hides each scene root's renderers in turn and logs how many real draw/SetPass calls it owned.</summary>
    public sealed class FishingDrawBreakdown : MonoBehaviour
    {
        public const string Flag="-fishing-dc-breakdown";
        public static void AttachIfRequested(FishingPrototype c)
        {
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),Flag)>=0)c.gameObject.AddComponent<FishingDrawBreakdown>();
        }
        private IEnumerator Start()
        {
            var c=GetComponent<FishingPrototype>();
            yield return new WaitForSecondsRealtime(8);
            if(c.StartMenuOpen)c.FinishTitleVoyage();
            yield return new WaitForSecondsRealtime(12);
            var text=new StringBuilder();
            using(var draw=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count"))
            using(var setPass=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count"))
            {
                IEnumerator Settle(){for(int i=0;i<6;i++)yield return null;}
                yield return Settle();long total=draw.LastValue,totalSet=setPass.LastValue;
                text.Append("harbor real draw calls=").Append(total).Append(" setpass=").Append(totalSet).Append('\n');
                var roots=new List<Transform>();
                foreach(var root in gameObject.scene.GetRootGameObjects()){if(root==gameObject){foreach(Transform child in root.transform)roots.Add(child);}else roots.Add(root.transform);}
                var rows=new List<(long d,long s,string n)>();
                foreach(var t in roots)
                {
                    // Fresh baseline per root: title-voyage props fade out over time, so a single start value drifts.
                    yield return Settle();total=draw.LastValue;totalSet=setPass.LastValue;
                    var hidden=new List<Renderer>();foreach(var r in t.GetComponentsInChildren<Renderer>())if(r.enabled){r.enabled=false;hidden.Add(r);}
                    if(hidden.Count==0)continue;
                    yield return Settle();long d=total-draw.LastValue,s=totalSet-setPass.LastValue;
                    foreach(var r in hidden)if(r!=null)r.enabled=true;yield return Settle();
                    if(d>=5)rows.Add((d,s,t.name+" ("+hidden.Count+" renderers)"));
                }
                rows.Sort((a,b)=>b.d.CompareTo(a.d));long sum=0;
                foreach(var row in rows){sum+=row.d;text.Append("  -").Append(row.d).Append(" dc  -").Append(row.s).Append(" setpass  ").Append(row.n).Append('\n');}
                text.Append("final total=").Append(total).Append("; unattributed (sky/water DrawMesh/UI/other cameras) ≈ ").Append(total-sum).Append('\n');
            }
            var file=Path.Combine(Application.persistentDataPath,"dc-breakdown.txt");File.WriteAllText(file,text.ToString());
            Debug.Log("[DC-BREAKDOWN] "+file+"\n"+text);Application.Quit();
        }
    }
}
