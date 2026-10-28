using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>Finds visible models that interpenetrate in the places the player visits most (home, harbour, farm, landings,
    /// camps, residents, portals). Pairs are AABB-filtered, then vertices of each mesh are tested against the other mesh with
    /// a temporary mesh collider plus a reversed copy: a point is inside when rays in most axis directions leave through
    /// a back face before entering a front face; depth is the shortest such exit distance.</summary>
    public static class FishingV044OverlapAudit
    {
        public static void Run()=>FishingV011Validation.Begin("V044-OVERLAP");
        const float Allowed=.02f;
        // Plants, ground shells and effects are meant to sit in the ground or in each other.
        static readonly string[] Excluded={"grass","flower","weed","reed","fern","plant","bush","shrub","vine","moss","kelp","seaweed","foliage","clover","crop","sprout","leaf","leaves",
            "water","wave","foam","ripple","splash","ground","terrain","sand","shore","beach","soil","path","decal","shadow","glow","light","beam","halo","fog","mist","smoke","fx","sky","ocean","sea"};
        // Deliberate contact, each with the reason it is meant to overlap.
        static readonly (string a,string b,string why)[] Whitelist={
            ("post","","柱/桩本就插在地面或码头板里"),("pile","","码头桩插入水下与甲板"),("door","wall","门嵌在墙洞里"),("window","wall","窗嵌在墙里"),
            ("frame","wall","门窗框嵌墙"),("coin","coin","金币刻意堆叠"),("book","book","书本刻意叠放"),("plank","plank","木板拼接"),("rope","","绳索缠绕在桩/柱上"),("player","boat","玩家坐在船舱内"),("player","skiff","玩家坐在船舱内"),
        };
        sealed class Item{public Renderer R;public Mesh Mesh;public Vector3[] Points;public MeshCollider In,Out;public string Path,Zone;}
        sealed class Hit{public Item A,B;public float Depth;public Vector3 Point;public string Allowed;}

        static string PathOf(Transform t){var s=t.name;for(var p=t.parent;p!=null;p=p.parent)s=p.name+"/"+s;return s;}
        static bool Skip(Renderer r)
        {
            if(!r.enabled || !r.gameObject.activeInHierarchy || r.gameObject.layer==4 || r.gameObject.layer==5 || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer)return true;
            var e=r.bounds.extents;if(e.magnitude<.05f || Mathf.Max(e.x,e.z)>12)return true;
            var name=PathOf(r.transform).ToLowerInvariant();var leaf=r.name.ToLowerInvariant();
            if(Excluded.Any(leaf.Contains))return true;
            return name.Contains("ui") && name.Contains("canvas");
        }
        static Mesh MeshOf(Renderer r)
        {
            if(r is SkinnedMeshRenderer s){if(s.sharedMesh==null)return null;var m=new Mesh();s.BakeMesh(m,true);return m;}
            return r.TryGetComponent<MeshFilter>(out var f)?f.sharedMesh:null;
        }
        // The topmost ancestor below a shared container: parts of one model share it and may legitimately touch.
        static Transform ModelRoot(Transform t,Dictionary<Transform,int> rendererCount)
        {
            var best=t;
            for(var p=t.parent;p!=null && p.parent!=null;p=p.parent){if(p.GetComponent<FishingPrototype>()!=null || rendererCount.TryGetValue(p,out var n) && n>40)break;best=p;}
            return best;
        }
        static void Build(Item item)
        {
            var go=new GameObject("overlap-probe "+item.R.name){hideFlags=HideFlags.HideAndDontSave};go.layer=31;
            var t=item.R.transform;go.transform.SetPositionAndRotation(t.position,t.rotation);go.transform.localScale=item.R is SkinnedMeshRenderer?Vector3.one:t.lossyScale;
            item.In=go.AddComponent<MeshCollider>();item.In.sharedMesh=item.Mesh;
            var flip=UnityEngine.Object.Instantiate(item.Mesh);flip.hideFlags=HideFlags.HideAndDontSave;
            for(int s=0;s<flip.subMeshCount;s++){var ix=flip.GetTriangles(s);for(int i=0;i<ix.Length;i+=3)(ix[i],ix[i+1])=(ix[i+1],ix[i]);flip.SetTriangles(ix,s);}
            item.Out=go.AddComponent<MeshCollider>();item.Out.sharedMesh=flip;
            var v=item.Mesh.vertices;var step=Mathf.Max(1,v.Length/600);var pts=new List<Vector3>();
            var m=item.R is SkinnedMeshRenderer?Matrix4x4.TRS(t.position,t.rotation,Vector3.one):t.localToWorldMatrix;
            for(int i=0;i<v.Length;i+=step)pts.Add(m.MultiplyPoint3x4(v[i]));
            item.Points=pts.ToArray();
        }
        static readonly Vector3[] Dirs={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        static float Depth(Item b,Vector3 p)
        {
            float reach=b.R.bounds.size.magnitude+1,best=float.MaxValue;int votes=0;
            foreach(var d in Dirs)
            {
                var ray=new Ray(p,d);bool exit=b.Out.Raycast(ray,out var o,reach),enter=b.In.Raycast(ray,out var e,reach);
                if(exit && (!enter || o.distance<e.distance)){votes++;best=Mathf.Min(best,o.distance);}
            }
            return votes>=5?best:0;
        }
        static string Allow(Item a,Item b)
        {
            string x=a.Path.ToLowerInvariant(),y=b.Path.ToLowerInvariant();
            foreach(var (p,q,why) in Whitelist)
                if(x.Contains(p) && (q=="" || y.Contains(q)) || y.Contains(p) && (q=="" || x.Contains(q)))return why;
            return null;
        }

        public static IEnumerator Checks(FishingPrototype c)
        {
            string folder=Environment.GetEnvironmentVariable("V044_OVERLAP_OUT");if(string.IsNullOrEmpty(folder))folder="Logs/V044/overlap/before";
            folder=Path.GetFullPath(folder);Directory.CreateDirectory(folder);
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);if(c.Paused)c.TogglePause();
            c.ReturnToHarbor();for(int f=0;f<60;f++)yield return null;
            // The reef is only solid at low water; reuse the tide suite's clock jump (private there).
            if(c.TidalFlat!=null)
            {
                var toTide=typeof(FishingV044TideValidation).GetMethod("ToTide",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
                for(int i=0;i<4 && (c.Career.Calendar.Hour<9 || c.Career.Calendar.Hour>15);i++)toTide.Invoke(null,new object[]{c,8.0});
                c.TidalFlat.Refresh(false);for(int f=0;f<6;f++)yield return null;Physics.SyncTransforms();
            }
            Debug.Log("[V044-OVERLAP] stage: zones");
            var zones=new List<(string name,Vector3 at,float r)>();
            if(c.Home!=null){zones.Add(("家岛渔屋",c.Home.Origin,13));zones.Add(("家岛码头",c.Home.BoardwalkStart,12));}
            if(c.Farm!=null && c.Farm.Root!=null)zones.Add(("菜园与水井",c.Farm.Root.TransformPoint(new Vector3(6,0,-6)),12));
            zones.Add(("港口码头",c.harbor,20));zones.Add(("商店收银台",c.ShopPosition,10));
            for(int i=0;i<4;i++){zones.Add(("岛"+i+"登岛点",FishingIslandWorld.Landing(i),12));zones.Add(("岛"+i+"营地",FishingIslandWorld.Center(i),20));
                if(c.Realms!=null)zones.Add(("岛"+i+"秘境入口",c.Realms.PortalApproach(i),8));}
            if(c.TidalFlat!=null && c.TidalFlat.Root!=null){zones.Add(("灯塔退潮礁石路",c.TidalFlat.Root.position,20));zones.Add(("灯塔礁台",c.TidalFlat.Islet,14));}
            if(c.Keeper!=null)
            {
                zones.Add(("灯塔外梯与回廊",c.Keeper.Axis,10));
                for(int a=0;a<360;a+=60)zones.Add(("灯塔外梯与回廊",c.Keeper.DeckPoint(a,2.6f),2.5f));
                if(c.Keeper.Ladder!=null){zones.Add(("灯塔外梯与回廊",c.Keeper.Ladder.Bottom,3));zones.Add(("灯塔外梯与回廊",c.Keeper.Ladder.Top,3));}
                for(int i=0;i<8;i++){Transform p=null;try{p=c.Keeper.Parcel(i);}catch(System.IndexOutOfRangeException){break;}if(p!=null)zones.Add(("码头谢礼包",p.position,3));}
            }
            for(int i=0;i<6;i++){var p=c.Residents?.Person(i);if(p!=null)zones.Add(("居民"+i,p.position,4));}

            var all=UnityEngine.Object.FindObjectsOfType<Renderer>();
            var counts=new Dictionary<Transform,int>();
            foreach(var r in all)for(var p=r.transform.parent;p!=null;p=p.parent){counts.TryGetValue(p,out var n);counts[p]=n+1;}
            var items=new List<Item>();
            foreach(var r in all)
            {
                if(Skip(r))continue;
                var zone=zones.FirstOrDefault(z=>r.bounds.SqrDistance(z.at)<z.r*z.r);if(zone.name==null)continue;
                var mesh=MeshOf(r);if(mesh==null || mesh.vertexCount<3)continue;
                items.Add(new Item{R=r,Mesh=mesh,Path=PathOf(r.transform),Zone=zone.name});
            }
            Debug.Log("[V044-OVERLAP] stage: build "+items.Count);
            var roots=items.ToDictionary(i=>i,i=>ModelRoot(i.R.transform,counts));
            foreach(var i in items)Build(i);
            Physics.SyncTransforms();yield return null;
            var hits=new List<Hit>();int pairs=0;
            for(int i=0;i<items.Count;i++)for(int j=i+1;j<items.Count;j++)
            {
                Item a=items[i],b=items[j];if(roots[a]==roots[b])continue;
                var ab=a.R.bounds;var bb=b.R.bounds;if(!ab.Intersects(bb))continue;
                Vector3 lo=Vector3.Max(ab.min,bb.min),hi=Vector3.Min(ab.max,bb.max),size=hi-lo;
                if(size.x<Allowed || size.y<Allowed || size.z<Allowed)continue;
                pairs++;var box=new Bounds((lo+hi)/2,size);
                Hit best=null;
                foreach(var (x,y) in new[]{(a,b),(b,a)})foreach(var p in x.Points)
                {
                    if(!box.Contains(p))continue;var d=Depth(y,p);
                    if(d>Allowed && (best==null || d>best.Depth))best=new Hit{A=a,B=b,Depth=d,Point=p};
                }
                if(best!=null){best.Allowed=Allow(a,b);hits.Add(best);}
                if(pairs%200==0)yield return null;
            }
            foreach(var i in items)if(i.In!=null)UnityEngine.Object.DestroyImmediate(i.In.gameObject);

            Debug.Log("[V044-OVERLAP] stage: report "+hits.Count);
            var real=hits.Where(h=>h.Allowed==null).OrderByDescending(h=>h.Depth).ToList();
            var md=new StringBuilder();
            md.AppendLine("# V044 穿模检测报告").AppendLine().AppendLine("生成时间："+DateTime.Now.ToString("yyyy-MM-dd HH:mm")+"；候选模型 "+items.Count+"，包围盒相交对 "+pairs+"，真实穿插 "+real.Count+"，白名单 "+(hits.Count-real.Count)+"。").AppendLine();
            md.AppendLine("## 分区统计").AppendLine().AppendLine("| 区域 | 模型数 | 穿插数 |").AppendLine("|---|---|---|");
            foreach(var z in zones.Select(z=>z.name).Distinct())md.AppendLine("| "+z+" | "+items.Count(i=>i.Zone==z)+" | "+real.Count(h=>h.A.Zone==z || h.B.Zone==z)+" |");
            md.AppendLine().AppendLine("## 穿插明细（按深度）").AppendLine().AppendLine("| # | 区域 | 对象 A | 对象 B | 深度 m | 位置 | 截图 |").AppendLine("|---|---|---|---|---|---|---|");
            var cam=c.viewCamera.transform;var keep=(cam.position,cam.rotation);
            for(int k=0;k<real.Count;k++)
            {
                var h=real[k];string shot="";
                if(k<80)
                {
                    shot="hit"+k.ToString("00");var size=Mathf.Clamp(Mathf.Min(h.A.R.bounds.size.magnitude,h.B.R.bounds.size.magnitude),.6f,4);
                    var eye=h.Point+new Vector3(.6f,.7f,-1)*size*1.3f;
                    if(Physics.Linecast(h.Point+Vector3.up*.3f,eye,out var block,~0,QueryTriggerInteraction.Ignore))eye=Vector3.Lerp(h.Point,block.point,.85f);
                    cam.SetPositionAndRotation(eye,Quaternion.LookRotation(h.Point-eye));yield return null;
                    FishingV016Validation.Capture(c,folder,shot);shot="!["+shot+"]("+shot+".png)";
                }
                md.AppendLine("| "+k+" | "+h.A.Zone+" | `"+h.A.Path+"` | `"+h.B.Path+"` | "+h.Depth.ToString("0.000")+" | "+h.Point.ToString("F2")+" | "+shot+" |");
            }
            cam.SetPositionAndRotation(keep.position,keep.rotation);
            md.AppendLine().AppendLine("## 白名单（允许接触）").AppendLine();
            foreach(var h in hits.Where(h=>h.Allowed!=null))md.AppendLine("- `"+h.A.Path+"` ↔ `"+h.B.Path+"` "+h.Depth.ToString("0.000")+"m：" +h.Allowed);
            // Invisible walls: solid colliders noticeably wider than the model they belong to, plus the reported walk stalls.
            md.AppendLine().AppendLine("## 隐形墙（碰撞体比可见模型大 >0.3m）").AppendLine().AppendLine("| 区域 | 碰撞体 | 超出 m |").AppendLine("|---|---|---|");
            string[] stalls={"ancienttree","galleon","shorerock","observatoryexterior"};
            foreach(var col in UnityEngine.Object.FindObjectsOfType<Collider>())
            {
                if(col.isTrigger || !col.enabled || col is TerrainCollider || col is WheelCollider)continue;
                var path=PathOf(col.transform);var zone=zones.FirstOrDefault(z=>col.bounds.SqrDistance(z.at)<z.r*z.r).name;
                if(zone==null && !stalls.Any(path.ToLowerInvariant().Replace(" ","").Contains))continue;
                var rs=col.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                if(rs.Length==0 && col.transform.parent!=null)rs=col.transform.parent.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                float over;
                if(rs.Length==0)over=col.bounds.size.magnitude;
                else{var vb=rs[0].bounds;foreach(var r in rs)vb.Encapsulate(r.bounds);var cb=col.bounds;
                    over=Mathf.Max(vb.min.x-cb.min.x,cb.max.x-vb.max.x,vb.min.z-cb.min.z,cb.max.z-vb.max.z,cb.max.y-vb.max.y);}
                if(over>.3f)md.AppendLine("| "+(zone??"走路卡点")+" | `"+path+"`"+(rs.Length==0?"（无可见模型）":"")+" | "+over.ToString("0.00")+" |");
            }
            File.WriteAllText(Path.Combine(folder,"report.md"),md.ToString(),new UTF8Encoding(false));
            Debug.Log("[V044-OVERLAP] CHECK: "+items.Count+" models, "+pairs+" pairs, "+real.Count+" interpenetrations → "+Path.Combine(folder,"report.md"));
        }
    }
}
