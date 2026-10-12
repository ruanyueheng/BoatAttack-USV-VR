using System;
namespace BoatAttack.Fishing
{
    [Serializable]
    public sealed class PendingCatchData
    {
        public CatchRecord fish;
        public int ground;
        public bool measured;
        public float x,z;
        public PendingCatchData Copy()=>new PendingCatchData{fish=fish.Copy(),ground=ground,x=x,z=z,measured=measured};
    }
    /// <summary>One durable undecided catch. Rendering/hand poses never own rewards or inventory.</summary>
    public sealed partial class FishingCareer
    {
        private PendingCatchData _pending;
        private bool _resolving;
        public PendingCatchData PendingCatch=>_pending?.Copy();
        public bool HasPendingCatch=>_pending!=null;
        public int ReleasedCatches { get; private set; }
        public bool OrderMarked { get; private set; }
        public bool MarkOrder(){if(OrderMarked)return false;OrderMarked=true;return true;}
        public bool BeginPending(CatchRecord fish,int ground,float x,float z)
        {
            var pending=new PendingCatchData{fish=fish,ground=ground,x=x,z=z};
            if(_pending!=null || !ValidPending(pending) || _recorded.Contains(fish.id))return false;
            _pending=pending.Copy();return true;
        }
        // JsonUtility represents null inline serializable objects as blank objects in some versions.
        private static bool EmptyPending(PendingCatchData p)=>p==null || (!p.measured && p.ground==0 && p.x==0 && p.z==0 && (p.fish==null || (string.IsNullOrEmpty(p.fish.id) && p.fish.grams==0 && p.fish.value==0 && p.fish.kind==FishKind.Coral && !p.fish.rare)));
        private static bool ValidPending(PendingCatchData p)=>p!=null && Valid(p.fish) && p.ground>=0 && p.ground<FishingGrounds.Count
            && !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.z) && !float.IsInfinity(p.z) && Math.Abs(p.x)<100000 && Math.Abs(p.z)<100000;
        public bool MeasurePending(){if(_pending==null || _pending.measured)return false;_pending.measured=true;return true;}
        public bool ResolvePending(bool store,out CatchRecord fish,out int bounty)
        {
            fish=null;bounty=0;if(_pending==null)return false;
            var p=_pending;bool legend=FishingSpecies.Of(p.fish.kind).Legendary;
            if(legend && (!_pending.measured || store))return false;
            if(store && !legend && Count>=Capacity)return false;
            _resolving=true;
            try
            {
                if(legend){bounty=RecordLegend(p.fish);if(bounty<=0)return false;}
                else if(store){if(!Store(p.fish))return false;}
                else
                {
                    if(!Remember(p.fish.id))return false;
                    Record(p.fish);ReleasedCatches++;
                }
                // Preserve the existing quest contract: only a NEW successful storage counts.
                if(store && !legend)Expedition.RecordCatch(true,FishingGrounds.MatchesTarget(p.ground,p.fish.kind,p.x,p.z));
                fish=p.fish.Copy();_pending=null;return true;
            }
            finally{_resolving=false;}
        }
    }
    /// <summary>Measured overlap followed by a small lift, in the boat frame. Fast swipes cannot capture.</summary>
    public sealed class FishingNetGate
    {
        private float _covered,_height;
        private bool _armed;
        public bool Captured { get; private set; }
        public void Reset(){_covered=0;_armed=Captured=false;}
        public bool Step(bool ready,float radial,float vertical,float upright,float speed,float height,float dt)
        {
            if(!ready || !Finite(radial) || !Finite(vertical) || !Finite(upright) || !Finite(speed) || !Finite(height) || !Finite(dt) || dt<=0)
            {Reset();return false;}
            if(Captured)return false;
            bool safe=radial>=0 && radial<.24f && Math.Abs(vertical)<.32f && upright>.6f && speed>=0 && speed<1.8f;
            if(!safe){Reset();return false;}
            if(!_armed)
            {
                if(Math.Abs(vertical)>.14f){_covered=0;return false;}
                _covered+=Math.Min(dt,.05f);
                if(_covered>=.16f){_armed=true;_height=height;}
            }
            else if(height-_height>=.18f){Captured=true;return true;}
            return false;
        }
        private static bool Finite(float x)=>!float.IsNaN(x) && !float.IsInfinity(x);
    }
    /// <summary>Ordinary catches may go straight from net to box. Deliberate proximity + gentle tilt, never a fling.</summary>
    public sealed class FishingNetTransferGate
    {
        private float _dwell;private bool _sent;
        public void Reset(){_dwell=0;_sent=false;}
        public bool Step(bool ordinaryNetFish,float horizontal,float above,float upright,float speed,float dt)
        {
            bool finite=Finite(horizontal)&&Finite(above)&&Finite(upright)&&Finite(speed)&&Finite(dt);
            if(!ordinaryNetFish || !finite || dt<=0 || horizontal<0 || horizontal>.4f || above<-.08f || above>.55f || upright>.8f || upright<-.3f || speed<0 || speed>1.2f)
            {Reset();return false;}
            if(_sent)return false;_dwell+=Math.Min(dt,.05f);
            if(_dwell<.18f)return false;_sent=true;return true;
        }
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
    public sealed class FishingPressEdge
    {
        private bool _released;
        public bool Step(bool available,bool held)
        {
            if(!available){_released=false;return false;}
            if(!held){_released=true;return false;}
            bool press=_released;_released=false;return press;
        }
        public void Reset()=>_released=false;
    }
}
