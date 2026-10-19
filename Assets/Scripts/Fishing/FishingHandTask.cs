using System;

namespace BoatAttack.Fishing
{
    public enum FishingHandTaskKind { Sieve, Winder }

    /// <summary>Metres relative to the player's hands; desktop supplies the same bounded motion.</summary>
    public sealed class FishingHandTask
    {
        private float _x,_y,_angle,_turns,_cooldown;
        private bool _sample;
        private int _direction;
        public FishingHandTaskKind Kind { get; }
        public int Strokes { get; private set; }
        public bool Claimed { get; private set; }
        public float Progress=>Kind==FishingHandTaskKind.Sieve?Math.Min(1,Strokes/12f):Math.Min(1,_turns/1080f);
        public bool Complete=>Progress>=1;
        public FishingHandTask(FishingHandTaskKind kind)
        {
            if(kind!=FishingHandTaskKind.Sieve && kind!=FishingHandTaskKind.Winder)throw new ArgumentOutOfRangeException(nameof(kind));
            Kind=kind;
        }
        public void Release(){_sample=false;_direction=0;_cooldown=0;}
        public bool Sample(float x,float y,float seconds)
        {
            if(Complete || !Finite(x) || !Finite(y) || !Finite(seconds) || seconds<=0 || seconds>.15f)
            {Release();return false;}
            _cooldown=Math.Max(0,_cooldown-seconds);
            float angle=(float)(Math.Atan2(y,x)*180/Math.PI),radius=(float)Math.Sqrt(x*x+y*y);
            if(!_sample){_x=x;_y=y;_angle=angle;_sample=true;return false;}
            // Tracking teleports, jitter and long frame gaps must never count as deliberate gestures.
            if(Math.Abs(x-_x)>.30f || Math.Abs(y-_y)>.30f){Release();return false;}
            if(Kind==FishingHandTaskKind.Sieve)
            {
                float delta=x-_x;int direction=delta>=0?1:-1;
                if(direction==_direction){_x=x;return false;}
                if(Math.Abs(delta)<.075f || _cooldown>0)return false;
                _direction=direction;_x=x;_y=y;_cooldown=.16f;Strokes++;return true;
            }
            float change=angle-_angle;
            if(change>180)change-=360;if(change< -180)change+=360;
            _angle=angle;_x=x;_y=y;
            if(radius<.04f || radius>.22f || Math.Abs(change)>65){_sample=false;return false;}
            if(_direction==0 && Math.Abs(change)>1)_direction=change>0?1:-1;
            float before=_turns;_turns=Math.Max(0,Math.Min(1080,_turns+change*_direction));
            return (int)(before/90)!=(int)(_turns/90);
        }
        public bool HasMaterials(FishingBackpack bag)=>bag!=null && (Kind==FishingHandTaskKind.Sieve?bag.Count(FieldItem.Sand)>=2:bag.Count(FieldItem.Fiber)>=3);
        public bool Claim(FishingBackpack bag)
        {
            if(Claimed || !Complete || !HasMaterials(bag))return false;
            var cost=FishingRecipes.Stack(Kind==FishingHandTaskKind.Sieve?FieldItem.Sand:FieldItem.Fiber,Kind==FishingHandTaskKind.Sieve?2:3);
            if(!bag.Exchange(new[]{cost},Kind==FishingHandTaskKind.Sieve?FieldItem.Shell:FieldItem.Rope,1))return false;
            Claimed=true;return true;
        }
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
}
