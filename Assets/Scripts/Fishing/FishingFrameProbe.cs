using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>[Claude v0.41] 仅命令行开启（-fishing-frame-probe 路径）：记录可见窗口下每帧耗时、GC 次数与 >40ms 卡顿尖峰。</summary>
    public sealed class FishingFrameProbe : MonoBehaviour
    {
        public const string EnableFlag="-fishing-frame-probe";
        private string _path;
        private readonly List<float> _frames=new List<float>(200000);
        private readonly StringBuilder _spikes=new StringBuilder();
        private int _gc;
        private FishingPrototype _c;
        public static void Attach(FishingPrototype c)
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,EnableFlag);
            if(i<0 || i+1>=args.Length || !Path.IsPathRooted(args[i+1]))return;
            var probe=c.gameObject.AddComponent<FishingFrameProbe>();probe._path=args[i+1];probe._c=c;probe._gc=GC.CollectionCount(0);
        }
        private void Update()
        {
            if(Time.realtimeSinceStartup<10)return;
            float ms=Time.unscaledDeltaTime*1000;_frames.Add(ms);
            int gc=GC.CollectionCount(0);
            if(ms>40)_spikes.AppendLine($"{Time.realtimeSinceStartup:F1}s {ms:F1}ms gc+{gc-_gc} pos={_c.ExplorerPosition} ashore={_c.Ashore} case={(_c.Acceptance!=null?_c.Acceptance.ToString():"")}");
            _gc=gc;
            if(_c.AcceptanceOptions==null && Time.realtimeSinceStartup>70){OnApplicationQuit();_frames.Clear();Application.Quit(0);}
        }
        private void OnApplicationQuit()
        {
            if(_frames.Count==0)return;var sorted=new List<float>(_frames);sorted.Sort();
            float sum=0;foreach(var f in _frames)sum+=f;
            string head=$"frames={_frames.Count} avg={sum/_frames.Count:F2}ms p50={sorted[sorted.Count/2]:F2} p95={sorted[(int)(sorted.Count*.95f)]:F2} p99={sorted[(int)(sorted.Count*.99f)]:F2} max={sorted[sorted.Count-1]:F1} gc0={GC.CollectionCount(0)}\n";
            File.WriteAllText(_path,head+_spikes);
        }
    }
}
