using System;

namespace BoatAttack.Fishing
{
    public static class FishingRifleBallistics
    {
        public const float RecoverySeconds=.28f;
        public static float Spread(int burst,bool aiming,bool moving)
            =>(aiming?.16f:.45f)+Math.Min(20,Math.Max(0,burst))*.055f+(moving?1.5f:0);
        public static void Recoil(int burst,out float pitch,out float yaw)
        {
            int shot=Math.Min(30,Math.Max(0,burst));
            pitch=Math.Min(5,shot*.28f);
            yaw=shot<4?0:(float)Math.Sin((shot-4)*.72f)*Math.Min(1.5f,(shot-3)*.13f);
        }
    }
}
