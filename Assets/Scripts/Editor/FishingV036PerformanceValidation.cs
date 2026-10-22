using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV036PerformanceValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V036-PERFORMANCE");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V036-PERFORMANCE] CHECK: "+label);}
        private static bool Same(IReadOnlyList<Vector3> a,IReadOnlyList<Vector3> b)
        {if(a.Count!=b.Count)return false;for(int i=0;i<a.Count;i++)if((a[i]-b[i]).sqrMagnitude>.000001f)return false;return true;}
        public static IEnumerator Checks(FishingPrototype c)
        {
            int navigation=c.NavigationTarget;bool paused=c.Paused;var route=c.WaterRoute;
            try
            {
                if(c.Paused)c.TogglePause();c.ReturnToHarbor();c.SelectNavigation(c.HomeNavigationIndex);yield return null;c.TogglePause();
                var reference=V035Reference(c);Check(reference!=null && reference.Count>=2,"V035 reference finds the actual home voyage around the coast");
                Check(route.Plan() && Same(reference,route.Points),"Reusable heap planner preserves the V035 A* route numerically");
                var sync=new List<Vector3>(route.Points);int bufferLength=route.PlanningBufferLength;
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var cost=typeof(FishingWaterRoute).GetField("_cost",flags).GetValue(route);
                var previous=typeof(FishingWaterRoute).GetField("_previous",flags).GetValue(route);
                var closed=typeof(FishingWaterRoute).GetField("_closed",flags).GetValue(route);
                route.RequestPlan();Check(route.Planning && Same(sync,route.Points),"Starting a runtime replan keeps the current safe route intact");
                int steps=0,maxNodes=0;double maxMilliseconds=0;
                while(route.Planning)
                {
                    route.AdvancePlanning();maxNodes=Math.Max(maxNodes,route.LastPlanStepExpandedNodes);maxMilliseconds=Math.Max(maxMilliseconds,route.LastPlanStepMilliseconds);
                    if(route.LastPlanStepExpandedNodes>FishingWaterRoute.PlanningNodeBudget)throw new InvalidOperationException("Planning exceeded its per-frame node budget");
                    if(route.Planning && !Same(sync,route.Points))throw new InvalidOperationException("Partial search published an incomplete route");
                    if(++steps>=10000)throw new InvalidOperationException("Runtime route search did not finish");yield return null;
                }
                Check(Same(sync,route.Points),"Incremental and synchronous planning produce the same route and endpoint");
                Check(maxNodes<=FishingWaterRoute.PlanningNodeBudget,"Every planning slice respects the 32-node work bound and leaves partial results private");
                Check(ReferenceEquals(cost,typeof(FishingWaterRoute).GetField("_cost",flags).GetValue(route)) && ReferenceEquals(previous,typeof(FishingWaterRoute).GetField("_previous",flags).GetValue(route))
                    && ReferenceEquals(closed,typeof(FishingWaterRoute).GetField("_closed",flags).GetValue(route)) && route.PlanningBufferLength==bufferLength,"Replans retain the world-sized search arrays");
                for(int i=1;i<route.Points.Count;i++)Check(route.ClearSegment(route.Points[i-1],route.Points[i]),"Unchanged hull clearance on committed route segment "+i);
                route.Plan();long before=GC.GetAllocatedBytesForCurrentThread();bool plans=true;
                for(int i=0;i<4;i++)plans&=route.Plan();long allocation=GC.GetAllocatedBytesForCurrentThread()-before;
                Check(plans && allocation<16384,"Warm replans allocate only bounded request state, not world-sized arrays or tree nodes");
                route.RequestPlan();c.TogglePause();c.SelectNavigation(0);c.TogglePause();route.AdvancePlanning();
                Check(!route.Planning && Same(sync,route.Points),"Changing the waypoint cancels the previous search without publishing stale results");
                route.RequestPlan();route.enabled=false;Check(!route.Planning,"Disabling route guidance cancels pending search work");route.enabled=true;
                route.RequestPlan();c.ReturnToHarbor();Check(!route.Planning,"Returning to harbor cancels the in-flight route request");
                string text="Route equivalence: V035 reference = reusable synchronous = incremental\nGrid cells: "+bufferLength+"\nRuntime slices: "+steps+"\nMaximum expanded nodes per slice: "+maxNodes+"\nMaximum measured slice milliseconds: "+maxMilliseconds.ToString("F3")+"\nManaged bytes for 4 warm replans: "+allocation+"\nBudget: 1.5 ms soft deadline plus at most one physics/node step; 32 expanded nodes hard maximum.\nEditor timings are diagnostic and are not a player/headset frame-rate guarantee.\n";
                string folder=Path.GetFullPath("Logs/V036");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"route-performance.txt"),text);Debug.Log("[V036-PERFORMANCE] "+text);
            }
            finally{route.CancelPlanning();route.enabled=true;if(c.Paused)c.TogglePause();c.ReturnToHarbor();c.SelectNavigation(navigation);if(paused)c.TogglePause();}
        }
        // Frozen V035 reference keeps the original SortedSet frontier and per-search arrays to detect changes in path/tie semantics.
        private static List<Vector3> V035Reference(FishingPrototype c)
        {
            var route=c.WaterRoute;const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            int width=(int)typeof(FishingWaterRoute).GetField("_width",flags).GetValue(route),depth=(int)typeof(FishingWaterRoute).GetField("_depth",flags).GetValue(route);
            var origin=(Vector3)typeof(FishingWaterRoute).GetField("_origin",flags).GetValue(route);var cache=new byte[width*depth];
            Vector3 Point(int id)=>origin+new Vector3(id%width*4,0,id/width*4);
            bool Water(int id){if(cache[id]==0)cache[id]=(byte)(route.ClearPoint(Point(id))?1:2);return cache[id]==1;}
            int Connect(Vector3 p)
            {
                int x=Mathf.RoundToInt((p.x-origin.x)/4),z=Mathf.RoundToInt((p.z-origin.z)/4),best=-1;float distance=float.MaxValue;
                for(int dz=-2;dz<=2;dz++)for(int dx=-2;dx<=2;dx++)
                {
                    int nx=x+dx,nz=z+dz;if(nx<0 || nz<0 || nx>=width || nz>=depth)continue;int id=nz*width+nx;float d=(Point(id)-p).sqrMagnitude;
                    if(d>=distance || !Water(id) || !route.ClearSegment(p,Point(id)))continue;best=id;distance=d;
                }
                return best;
            }
            var goal=c.NavigationPosition;goal.y=0;var start=c.Player.transform.position;start.y=0;
            if(route.ClearSegment(start,goal))return new List<Vector3>{start,goal};
            var approaches=new Dictionary<int,Vector3>();int direct=route.ClearPoint(goal)?Connect(goal):-1;if(direct>=0)approaches[direct]=goal;
            for(int radius=2;radius<=8;radius+=2)for(int i=0;i<16;i++)
            {float a=i*Mathf.PI/8;var p=goal+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;if(!route.ClearPoint(p))continue;int candidate=Connect(p);if(candidate>=0 && !approaches.ContainsKey(candidate))approaches[candidate]=p;}
            int first=Connect(start);if(first<0 || approaches.Count==0)return null;
            int n=cache.Length;var cost=new float[n];var previous=new int[n];var closed=new bool[n];for(int i=0;i<n;i++){cost[i]=float.MaxValue;previous[i]=-1;}
            var open=new SortedSet<(float score,int id)>();cost[first]=0;open.Add((Vector3.Distance(Point(first),goal),first));int last=-1;
            while(open.Count>0)
            {
                var head=open.Min;open.Remove(head);int id=head.id;if(closed[id])continue;closed[id]=true;
                if(approaches.TryGetValue(id,out var approach)){last=id;goal=approach;break;}int x=id%width,z=id/width;
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,nz=z+dz;if((dx==0 && dz==0) || nx<0 || nz<0 || nx>=width || nz>=depth)continue;int next=nz*width+nx;float step=dx!=0 && dz!=0?4*1.414214f:4;
                    if(closed[next] || cost[id]+step>=cost[next] || !Water(next))continue;if(dx!=0 && dz!=0 && (!Water(z*width+nx) || !Water(nz*width+x)))continue;
                    if(!route.ClearSegment(Point(id),Point(next)))continue;cost[next]=cost[id]+step;previous[next]=id;open.Add((cost[next]+Vector3.Distance(Point(next),goal),next));
                }
            }
            if(last<0)return null;var reverse=new List<Vector3>{goal};for(int id=last;id>=0;id=previous[id])reverse.Add(Point(id));reverse.Add(start);reverse.Reverse();
            var result=new List<Vector3>{start};for(int i=0;i<reverse.Count-1;){int next=reverse.Count-1;while(next>i+1 && !route.ClearSegment(reverse[i],reverse[next]))next--;result.Add(reverse[next]);i=next;}return result;
        }
    }
}
