using System;
namespace BoatAttack.Fishing
{
    /// <summary>Jump edge policy, shared by the actual CharacterController motor and pure regression tests.</summary>
    public sealed class FishingLocomotion
    {
        public const float StandingHeight=1.75f, CrouchingHeight=1.08f, Gravity=18f, JumpHeight=1.1f;
        private bool _jumpReady;
        public float VerticalSpeed { get; private set; }
        public float Stamina { get; private set; } = 1;
        public void RestoreStamina() => Stamina = Math.Min(1, Stamina + .5f);
        public void Exert(bool effort, bool resting, float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
            dt = Math.Min(dt, .05f);
            Stamina = Math.Max(0, Math.Min(1, Stamina + (effort ? -1f / 12f : resting ? .18f : .07f) * dt));
        }
        public void ResetInput(){_jumpReady=false;}
        public void Reset(){VerticalSpeed=0;ResetInput();}
        public void HitCeiling(){if(VerticalSpeed>0)VerticalSpeed=0;}
        public float Step(bool grounded,bool jumpHeld,bool crouched,float dt)
        {
            if(float.IsNaN(dt) || float.IsInfinity(dt) || dt<=0)return VerticalSpeed;
            dt=Math.Min(dt,.05f);
            if(!jumpHeld)_jumpReady=true;
            bool jump=jumpHeld && _jumpReady; if(jumpHeld)_jumpReady=false;
            if(grounded && VerticalSpeed<=0)VerticalSpeed=-2;
            if(jump && grounded && !crouched)VerticalSpeed=(float)Math.Sqrt(2*Gravity*JumpHeight);
            else VerticalSpeed=Math.Max(-18,VerticalSpeed-Gravity*dt);
            return VerticalSpeed;
        }
    }
}
