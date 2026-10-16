using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        private Light _lanternSpill;
        private void PresentLanternLight()
        {
            if(_lanternSpill==null)
            {
                _lanternSpill=new GameObject("Lantern soft ground spill",typeof(Light)).GetComponent<Light>();
                _lanternSpill.transform.SetParent(transform,false);_lanternSpill.type=LightType.Spot;
                _lanternSpill.color=new Color(1,.76f,.47f);_lanternSpill.range=13;
                _lanternSpill.spotAngle=115;_lanternSpill.innerSpotAngle=60;_lanternSpill.shadows=LightShadows.None;
                _lanternSpill.renderMode=LightRenderMode.ForcePixel;
            }
            _lanternSpill.enabled=_lamp.enabled;
            if(!_lamp.enabled)return;
            float flame=1+.022f*Mathf.Sin(Time.time*4.3f)+.012f*Mathf.Sin(Time.time*7.1f);
            float night=1-_c.Career.Calendar.Daylight;
            _lamp.intensity=Mathf.Lerp(.55f,.95f,night)*flame;
            _lamp.shadows=UnityEngine.XR.XRSettings.isDeviceActive?LightShadows.None:LightShadows.Soft;
            _lanternSpill.intensity=Mathf.Lerp(.9f,1.5f,night)*flame;
            _lanternSpill.transform.SetPositionAndRotation(_lamp.transform.position,
                Quaternion.LookRotation(_c.Shore.Actor.forward+Vector3.down*.55f));
        }
    }
}
