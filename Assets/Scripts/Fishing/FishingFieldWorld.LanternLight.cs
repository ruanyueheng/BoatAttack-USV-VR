using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        private Light _lanternSpill;
        public Light LanternLight=>_lamp;
        public Light LanternSpill=>_lanternSpill;
        private void PresentLanternLight()
        {
            if(_lanternSpill==null)
            {
                _lanternSpill=new GameObject("Lantern soft ground spill",typeof(Light)).GetComponent<Light>();
                _lanternSpill.transform.SetParent(transform,false);_lanternSpill.type=LightType.Spot;
                _lanternSpill.color=new Color(1,.76f,.47f);_lanternSpill.range=10;
                _lanternSpill.spotAngle=125;_lanternSpill.innerSpotAngle=70;_lanternSpill.shadows=LightShadows.None;
                _lanternSpill.renderMode=LightRenderMode.ForcePixel;
            }
            _lanternSpill.enabled=_lamp.enabled;
            if(!_lamp.enabled)return;
            float flame=1+.022f*Mathf.Sin(Time.time*4.3f)+.012f*Mathf.Sin(Time.time*7.1f);
            float night=1-_c.Career.Calendar.Daylight;
            // The old presentation overwrote the 2.6 build intensity with 0.55–0.95,
            // making a waist-height light barely visible on the mobile night coast.
            _lamp.intensity=Mathf.Lerp(2.2f,3.4f,night)*flame;
            // Do not let the wearer's body/lantern frame shadow away the nearby pool.
            // Two unshadowed lights fit the existing PICO per-object light budget.
            _lamp.shadows=LightShadows.None;
            _lanternSpill.intensity=Mathf.Lerp(1.8f,2.8f,night)*flame;
            _lanternSpill.transform.SetPositionAndRotation(_lamp.transform.position,
                Quaternion.LookRotation(_c.Shore.Actor.forward+Vector3.down*.55f));
        }
    }
}
