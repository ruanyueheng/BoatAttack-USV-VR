using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingWaterRoute
    {
        public const int PlanningNodeBudget=32;
        public const double PlanningMilliseconds=1.5;
        private float[] _cost;
        private int[] _previous;
        private bool[] _closed;
        private readonly List<(float score,int id)> _frontier=new List<(float,int)>();
        private readonly Dictionary<int,Vector3> _approaches=new Dictionary<int,Vector3>();
        private readonly List<Vector3> _reverse=new List<Vector3>(),_candidateRoute=new List<Vector3>();
        private IEnumerator _planner;
        private long _stepDeadline;
        private int _planningTarget=-1,_routeTarget=-1,_stepVisited;
        private Vector3 _planningGoal,_planningStart;
        private bool _planningSucceeded;
        public bool Planning=>_planner!=null;
        public int LastPlanStepExpandedNodes {get;private set;}
        public double LastPlanStepMilliseconds {get;private set;}
        public int PlanningBufferLength=>_cost?.Length??0;
        private void BuildPlanningBuffers(){_cost=new float[_water.Length];_previous=new int[_water.Length];_closed=new bool[_water.Length];}
        public void RequestPlan()
        {
            CancelPlanning();_target=_planningTarget=_c.NavigationTarget;_planningGoal=_c.NavigationPosition;_planningGoal.y=0;
            _planningStart=_c.Player.transform.position;_planningStart.y=0;_planningSucceeded=false;Failure="";
            _planner=SearchRoute();_nextPlan=Time.time+2;
        }
        public void CancelPlanning(){(_planner as IDisposable)?.Dispose();_planner=null;}
        public bool AdvancePlanning()
        {
            if(_planner==null)return false;
            if(_planningTarget!=_c.NavigationTarget || !_c.isActiveAndEnabled){CancelPlanning();return false;}
            long start=Stopwatch.GetTimestamp();_stepDeadline=start+(long)(Stopwatch.Frequency*PlanningMilliseconds/1000);_stepVisited=0;
            try
            {
                if(_planner.MoveNext())return false;
                CancelPlanning();return _planningSucceeded;
            }
            finally{LastPlanStepExpandedNodes=_stepVisited;LastPlanStepMilliseconds=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;}
        }
        public bool Plan()
        {
            RequestPlan();while(Planning)AdvancePlanning();return _planningSucceeded;
        }
        private bool PlanningBudgetReached()=>_stepVisited>=PlanningNodeBudget || Stopwatch.GetTimestamp()>=_stepDeadline;
        private IEnumerator SearchRoute()
        {
            var start=_planningStart;var goal=_planningGoal;_candidateRoute.Clear();
            if(ClearSegment(start,goal)){_candidateRoute.Add(start);_candidateRoute.Add(goal);CommitRoute(goal);yield break;}
            _approaches.Clear();int direct=ClearPoint(goal)?Connect(goal):-1;
            if(direct>=0)_approaches[direct]=goal;
            for(int radius=2;radius<=8;radius+=2)for(int i=0;i<16;i++)
            {
                if(PlanningBudgetReached())yield return null;
                float a=i*Mathf.PI/8;var p=goal+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;
                if(!ClearPoint(p))continue;int candidate=Connect(p);if(candidate>=0 && !_approaches.ContainsKey(candidate))_approaches[candidate]=p;
            }
            if(_approaches.Count==0){Failure="航点附近没有可通行的水面";yield break;}
            int first=Connect(start);if(first<0){Failure="船体过于靠近岸边，请退回开阔水面";yield break;}
            for(int i=0;i<_cost.Length;i++)
            {
                if((i&2047)==0 && PlanningBudgetReached())yield return null;
                _cost[i]=float.MaxValue;_previous[i]=-1;_closed[i]=false;
            }
            _frontier.Clear();_cost[first]=0;PushFrontier((Vector3.Distance(Point(first),goal),first));int last=-1;
            while(_frontier.Count>0)
            {
                if(PlanningBudgetReached())yield return null;
                int id=PopFrontier().id;if(_closed[id])continue;_closed[id]=true;_stepVisited++;
                if(_approaches.TryGetValue(id,out var approach)){last=id;goal=approach;break;}
                int x=id%_width,z=id/_width;
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,nz=z+dz;if((dx==0 && dz==0) || nx<0 || nz<0 || nx>=_width || nz>=_depth)continue;
                    int next=nz*_width+nx;float step=dx!=0 && dz!=0?Cell*1.414214f:Cell;
                    if(_closed[next] || _cost[id]+step>=_cost[next] || !Water(next))continue;
                    if(dx!=0 && dz!=0 && (!Water(z*_width+nx) || !Water(nz*_width+x)))continue;
                    if(!ClearSegment(Point(id),Point(next)))continue;
                    _cost[next]=_cost[id]+step;_previous[next]=id;PushFrontier((_cost[next]+Vector3.Distance(Point(next),goal),next));
                }
            }
            if(last<0){Failure="当前水道不连通，请退回开阔水面";yield break;}
            _reverse.Clear();_reverse.Add(goal);
            for(int id=last;id>=0;id=_previous[id])_reverse.Add(Point(id));_reverse.Add(start);_reverse.Reverse();_candidateRoute.Add(start);
            for(int i=0;i<_reverse.Count-1;)
            {
                int next=_reverse.Count-1;
                while(next>i+1 && !ClearSegment(_reverse[i],_reverse[next])){next--;if(PlanningBudgetReached())yield return null;}
                _candidateRoute.Add(_reverse[next]);i=next;if(PlanningBudgetReached())yield return null;
            }
            var current=_c.Player.transform.position;current.y=0;
            if((current-start).sqrMagnitude>.25f)
            {
                int entry=_candidateRoute.Count-1;
                while(entry>0 && !ClearSegment(current,_candidateRoute[entry])){entry--;if(PlanningBudgetReached())yield return null;}
                if(!ClearSegment(current,_candidateRoute[entry])){Failure="船只已驶离本次搜索起点，正在重新寻找安全水道";yield break;}
                _candidateRoute.RemoveRange(0,entry);_candidateRoute.Insert(0,current);
            }
            CommitRoute(goal);
        }
        private void CommitRoute(Vector3 goal)
        {
            if(_planningTarget!=_c.NavigationTarget)return;
            var current=_c.Player.transform.position;current.y=0;
            if(_candidateRoute.Count<2 || ((current-_candidateRoute[0]).sqrMagnitude>.000001f && !ClearSegment(current,_candidateRoute[1])))
            {Failure="船只已驶离本次搜索起点，正在重新寻找安全水道";_target=-1;_nextPlan=Time.time;return;}
            _candidateRoute[0]=current;
            _route.Clear();_route.AddRange(_candidateRoute);_goal=goal;_routeTarget=_planningTarget;_planningSucceeded=true;
        }
        private static bool Before((float score,int id) a,(float score,int id) b)=>a.score<b.score || (a.score==b.score && a.id<b.id);
        private void PushFrontier((float score,int id) value)
        {
            int at=_frontier.Count;_frontier.Add(value);
            while(at>0){int parent=(at-1)/2;if(!Before(value,_frontier[parent]))break;_frontier[at]=_frontier[parent];at=parent;}_frontier[at]=value;
        }
        private (float score,int id) PopFrontier()
        {
            var first=_frontier[0];var value=_frontier[_frontier.Count-1];_frontier.RemoveAt(_frontier.Count-1);if(_frontier.Count==0)return first;
            int at=0;
            while(at*2+1<_frontier.Count)
            {
                int child=at*2+1;if(child+1<_frontier.Count && Before(_frontier[child+1],_frontier[child]))child++;
                if(!Before(_frontier[child],value))break;_frontier[at]=_frontier[child];at=child;
            }
            _frontier[at]=value;return first;
        }
    }
}
