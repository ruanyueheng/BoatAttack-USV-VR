using System;
namespace BoatAttack.Fishing
{
    /// <summary>A world-started drag owns its release even when the pointer later crosses HUD chrome.</summary>
    public sealed class FishingMouseAngling
    {
        public bool Captured { get; private set; }
        public float Side { get; private set; }
        public bool Step(bool allowed,bool pressed,bool held,bool released,bool overUI,float deltaX)
        {
            if(!allowed){Reset();return false;}
            bool release=Captured && (released || !held);
            if(release){Reset();return true;}
            if(pressed && !overUI){Captured=true;Side=0;return false;}
            if(Captured && held && !float.IsNaN(deltaX) && !float.IsInfinity(deltaX))
                Side=Math.Max(-1,Math.Min(1,Side+deltaX/150f));
            return false;
        }
        public void Reset(){Captured=false;Side=0;}
    }
}
