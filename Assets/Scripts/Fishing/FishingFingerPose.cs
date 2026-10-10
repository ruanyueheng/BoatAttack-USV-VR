using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>每个手指的独立弯曲量；大直径镜筒不能复用细镐柄的握拳姿态。</summary>
    public readonly struct FishingFingerPose
    {
        public readonly Vector4 Fingers;
        public readonly float Thumb;
        public FishingFingerPose(float index,float middle,float ring,float pinky,float thumb)
        {Fingers=new Vector4(index,middle,ring,pinky);Thumb=thumb;}
        public static FishingFingerPose Uniform(float curl)=>new FishingFingerPose(curl,curl,curl,curl,curl);
        public float Curl(int digit)=>digit==4?Thumb:Fingers[digit];
        public static readonly FishingFingerPose Handle=new FishingFingerPose(.72f,.8f,.8f,.8f,.6f);
        public static readonly FishingFingerPose Barrel=new FishingFingerPose(.32f,.38f,.44f,.5f,.58f);
        public static readonly FishingFingerPose Book=new FishingFingerPose(.12f,.22f,.28f,.34f,.38f);
        public static readonly FishingFingerPose Lantern=new FishingFingerPose(.74f,.86f,.94f,1f,.72f);
    }
}
