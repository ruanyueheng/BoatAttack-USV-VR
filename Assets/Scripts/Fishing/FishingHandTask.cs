using System;

namespace BoatAttack.Fishing
{
    public enum FishingHandTaskKind { Sieve, Winder, Saw, Forge }

    /// <summary>Metres relative to the player's hands; desktop supplies the same bounded motion.</summary>
    public sealed class FishingHandTask
    {
        private float _x,_y,_angle,_turns,_cooldown;
        private bool _sample;
        private readonly bool _gunParts,_fineSpinning;
        private int _direction;
        public FishingHandTaskKind Kind { get; }
        public int Strokes { get; private set; }
        public bool Claimed { get; private set; }
        public int StrokeTarget=>Kind==FishingHandTaskKind.Sieve?12:Kind==FishingHandTaskKind.Saw?10:8;
        public FieldItem Output=>Kind==FishingHandTaskKind.Sieve?FieldItem.Shell:Kind==FishingHandTaskKind.Winder?FieldItem.Rope:Kind==FishingHandTaskKind.Saw?FieldItem.Plank:_gunParts?FieldItem.GunParts:FieldItem.IronPickHead;
        public int OutputCount=>Kind==FishingHandTaskKind.Saw?4:Kind==FishingHandTaskKind.Winder && _fineSpinning?2:1;
        public float Progress=>Kind==FishingHandTaskKind.Winder?Math.Min(1,_turns/1080f):Math.Min(1,Strokes/(float)StrokeTarget);
        public bool Complete=>Progress>=1;
        public FishingHandTask(FishingHandTaskKind kind,bool gunParts=false,FishingLore lore=null)
        {
            if(kind<FishingHandTaskKind.Sieve || kind>FishingHandTaskKind.Forge)throw new ArgumentOutOfRangeException(nameof(kind));
            if(gunParts && kind!=FishingHandTaskKind.Forge)throw new ArgumentException("Only forge has a metal-part choice",nameof(gunParts));
            Kind=kind;_gunParts=gunParts;_fineSpinning=lore!=null && lore.BlueprintUnlocked(0);
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
            if(Kind!=FishingHandTaskKind.Winder)
            {
                bool vertical=Kind==FishingHandTaskKind.Forge;float delta=vertical?y-_y:x-_x;int direction=delta>=0?1:-1;
                if(direction==_direction){if(vertical)_y=y;else _x=x;return false;}
                if(Math.Abs(delta)<.075f || _cooldown>0)return false;
                _direction=direction;_x=x;_y=y;_cooldown=.16f;if(!vertical || direction<0){Strokes++;return true;}return false;
            }
            float change=angle-_angle;
            if(change>180)change-=360;if(change< -180)change+=360;
            _angle=angle;_x=x;_y=y;
            if(radius<.04f || radius>.22f || Math.Abs(change)>65){_sample=false;return false;}
            if(_direction==0 && Math.Abs(change)>1)_direction=change>0?1:-1;
            float before=_turns;_turns=Math.Max(0,Math.Min(1080,_turns+change*_direction));
            return (int)(before/90)!=(int)(_turns/90);
        }
        private FieldStack[] Costs=>Kind==FishingHandTaskKind.Sieve?new[]{FishingRecipes.Stack(FieldItem.Sand,2)}
            :Kind==FishingHandTaskKind.Winder?new[]{FishingRecipes.Stack(FieldItem.Fiber,3)}
            :Kind==FishingHandTaskKind.Saw?new[]{FishingRecipes.Stack(FieldItem.Driftwood,1)}:new[]{FishingRecipes.Stack(FieldItem.IronIngot,_gunParts?6:3)};
        public bool HasMaterials(FishingBackpack bag)
        {if(bag==null)return false;foreach(var cost in Costs)if(bag.Count(cost.item)<cost.count)return false;return true;}
        public bool Claim(FishingBackpack bag)
        {
            if(Claimed || !Complete || !HasMaterials(bag) || !bag.Exchange(Costs,Output,OutputCount))return false;
            Claimed=true;return true;
        }
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
}
