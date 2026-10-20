using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed class FishingClimbRoute : MonoBehaviour
    {
        public Vector3 bottom, top, topOutset;
        public Vector3 Bottom => transform.TransformPoint(bottom);
        public Vector3 Top => transform.TransformPoint(top);
        public Vector3 ClimbEnd => Top+transform.TransformVector(topOutset);
    }
}
