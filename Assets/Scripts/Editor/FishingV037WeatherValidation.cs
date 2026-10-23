using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV037WeatherValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V037-WEATHER");
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V037-WEATHER] CHECK: "+label);}
        private static T Field<T>(FishingWeather weather,string name)=>(T)typeof(FishingWeather).GetField(name,Private).GetValue(weather);
        private static void Tick(FishingWeather weather,float seconds,bool frozen=false)=>typeof(FishingWeather).GetMethod("TickStorm",Private).Invoke(weather,new object[]{seconds,frozen});
        private static void Select(FishingWeather weather,BayWeather preset)
        {
            for(int i=0;i<48;i++)weather.Climate.Tick(.25f,false);
            Check(weather.Climate.Select(preset),"Can select "+preset);
            for(int i=0;i<48;i++)weather.Climate.Tick(.25f,false);
            weather.ApplyEnvironment();
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            var weather=c.Weather;var profile=c.Career.Snapshot();var previous=weather.Climate.Current;
            bool automatic=weather.Climate.Automatic,reduced=weather.ReducedEffects,focused=Field<bool>(weather,"_focused");float effects=c.EffectsVolume,ambient=c.AmbientVolume;
            var camera=c.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;
            string output=Path.GetFullPath(Path.Combine("Logs","V037Weather",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(output);
            try
            {
                c.ReturnToHarbor();if(c.StartMenuOpen)c.FinishTitleVoyage();if(c.Paused)c.TogglePause();
                if(weather.ReducedEffects)weather.ToggleReducedEffects();
                typeof(FishingWeather).GetField("_focused",Private).SetValue(weather,true);
                c.SetDayNight(false);Select(weather,BayWeather.Clear);
                float clearSun=RenderSettings.sun.intensity,clearAmbient=RenderSettings.ambientSkyColor.grayscale,clearReflection=RenderSettings.reflectionIntensity;
                camera.SetPositionAndRotation(c.Home.Origin+new Vector3(22,10,-48),Quaternion.LookRotation(new Vector3(-.2f,.12f,1)));
                FishingV016Validation.Capture(c,output,"weather-clear");
                Select(weather,BayWeather.Rain);var sky=RenderSettings.skybox;
                Check(sky.shader.name=="BoatAttack/FishingCoastalSky" && Mathf.Approximately(sky.GetFloat("_Storm"),1) && Mathf.Approximately(sky.GetFloat("_CloudCover"),1),"Rain reaches the actual sky material as full overcast");
                Check(RenderSettings.sun.intensity<clearSun*.4f && RenderSettings.sun.intensity>0,"Rain darkens daylight while retaining navigable light");
                Check(RenderSettings.ambientSkyColor.grayscale<clearAmbient*.65f && RenderSettings.reflectionIntensity<clearReflection*.4f,"Rain darkens ambient fill and sea reflections");
                Check(RenderSettings.fogEndDistance>=180 && RenderSettings.fogEndDistance<300,"Rain haze retains a usable navigation distance");
                FishingV016Validation.Capture(c,output,"weather-rain");
                c.SetDayNight(true);weather.ApplyEnvironment();c.Nightscape.Apply(0);
                Check(c.Nightscape.Visibility<.001f,"Rain clouds fully hide the nighttime moon and stars");
                FishingV016Validation.Capture(c,output,"weather-rain-night");c.SetDayNight(false);weather.ApplyEnvironment();
                int strikes=Field<int>(weather,"_strike");for(int i=0;i<32;i++)Tick(weather,.25f);Tick(weather,.04f);
                Check(Field<int>(weather,"_strike")==strikes+1 && sky.GetFloat("_Lightning")>0 && sky.GetFloat("_Lightning")<=.65f,"One distant strike follows the bounded initial delay");
                Check(Field<float>(weather,"_stormWait")>=13 && Field<float>(weather,"_thunderDelay")>0,"Strikes stay sparse and thunder is delayed after light");
                var direction=Field<Vector3>(weather,"_lightningDirection");
                var offshore=c.harbor-Vector3.ProjectOnPlane(direction,Vector3.up).normalized*400;offshore.y=32;
                camera.SetPositionAndRotation(offshore,Quaternion.LookRotation(direction));
                Check(!Physics.Raycast(camera.position,direction,400,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),"Lightning evidence has an unobstructed view across the sea");
                FishingV016Validation.Capture(c,output,"weather-lightning");
                var flash=new Texture2D(2,2,TextureFormat.RGB24,false);
                try
                {
                    Check(flash.LoadImage(File.ReadAllBytes(Path.Combine(output,"weather-lightning.png"))),"Lightning evidence decodes as a real rendered image");
                    int strokeRows=0;
                    for(int y=flash.height*35/100;y<flash.height*65/100;y+=4)
                    {
                        float background=Mathf.Max(flash.GetPixel(flash.width*32/100,y).grayscale,flash.GetPixel(flash.width*68/100,y).grayscale);
                        float peak=0;
                        for(int x=flash.width*43/100;x<flash.width*57/100;x++)peak=Mathf.Max(peak,flash.GetPixel(x,y).grayscale);
                        if(peak>.72f && peak>background+.18f)strokeRows++;
                    }
                    Check(strokeRows>8,"Rendered lightning contains a bright central stroke against the cloud background; rows="+strokeRows);
                }
                finally{UnityEngine.Object.Destroy(flash);}
                var thunder=Field<AudioSource>(weather,"_thunder");
                Check(thunder!=null && !thunder.loop && !thunder.playOnAwake && thunder.clip!=null && thunder.clip.length>=3.9f,"Thunder uses a finite separately controlled original rumble");
                c.SetSetting("Ambient",.5f);Check(Mathf.Abs(thunder.volume-.06f)<.001f,"Thunder obeys the quieter environment volume setting");
                c.SetSetting("Effects",0);Check(Mathf.Abs(thunder.volume-.06f)<.001f,"Muting action effects does not change the environment thunder route");
                for(int i=0;i<6;i++)Tick(weather,.25f);
                Check(Field<float>(weather,"_thunderDelay")<0,"Delayed thunder is dispatched after the distant flash");
                Tick(weather,.25f,true);
                Check(sky.GetFloat("_Lightning")==0 && Field<float>(weather,"_thunderDelay")<0 && !thunder.isPlaying,"Pause cancels both flash and pending thunder");
                for(int i=0;i<33;i++)Tick(weather,.25f);weather.ToggleReducedEffects();
                Check(weather.RainQuadBudget==32 && sky.GetFloat("_Lightning")==0 && Field<float>(weather,"_thunderDelay")<0 && !thunder.isPlaying,"Reduced effects immediately cancels lightning and delayed thunder");
                for(int i=0;i<80;i++)Tick(weather,.25f);
                Check(Field<float>(weather,"_thunderDelay")<0 && sky.GetFloat("_Lightning")==0,"Reduced effects cannot schedule a new strike");
                weather.ToggleReducedEffects();for(int i=0;i<33;i++)Tick(weather,.25f);
                typeof(FishingWeather).GetMethod("OnApplicationFocus",Private).Invoke(weather,new object[]{false});
                Check(sky.GetFloat("_Lightning")==0 && Field<float>(weather,"_thunderDelay")<0 && !thunder.isPlaying,"Focus loss immediately cancels storm transients");
                typeof(FishingWeather).GetMethod("OnApplicationFocus",Private).Invoke(weather,new object[]{true});
                for(int i=0;i<33;i++)Tick(weather,.25f);
                Check(weather.Climate.Select(BayWeather.Clear),"Rain can transition back to clear");weather.ApplyEnvironment();
                Check(Field<float>(weather,"_thunderDelay")<0 && sky.GetFloat("_Lightning")==0 && !thunder.isPlaying,"Leaving rain cancels pending thunder before the visual transition ends");
                for(int i=0;i<48;i++)weather.Climate.Tick(.25f,false);weather.ApplyEnvironment();
                Check(weather.StormAmount==0 && sky.GetFloat("_Storm")==0 && sky.GetFloat("_CloudCover")<.2f,"Clear weather removes the overcast continuously");
                Check(Mathf.Abs(RenderSettings.sun.intensity-clearSun)<.001f && Mathf.Abs(RenderSettings.reflectionIntensity-clearReflection)<.001f,"Clear weather restores daylight and sea reflections");
                var originalSky=Field<Material>(weather,"_originalSky");var originalAmbient=Field<Color>(weather,"_ambient");float originalSun=Field<float>(weather,"_sunIntensity");
                weather.enabled=false;
                Check(RenderSettings.skybox==originalSky && RenderSettings.ambientSkyColor==originalAmbient && Mathf.Abs(RenderSettings.sun.intensity-originalSun)<.001f,"Disabling weather restores the borrowed scene environment");
                Check(!thunder.isPlaying && Field<float>(weather,"_thunderDelay")<0,"Disabling weather cancels thunder and all pending events");
                weather.enabled=true;weather.ApplyEnvironment();
                Check(RenderSettings.skybox!=originalSky && RenderSettings.skybox!=sky,"Re-enable creates a fresh isolated sky material");
                yield return null;
            }
            finally
            {
                c.SetSetting("Effects",effects);c.SetSetting("Ambient",ambient);
                if(!weather.enabled)weather.enabled=true;
                if(weather.ReducedEffects!=reduced)weather.ToggleReducedEffects();
                typeof(FishingWeather).GetField("_focused",Private).SetValue(weather,focused);
                c.ReturnToHarbor();c.RestoreShopTestProfile(profile);Select(weather,previous);if(automatic)weather.Climate.ResumeCycle();
                camera.SetPositionAndRotation(position,rotation);weather.ApplyEnvironment();c.Nightscape.Apply(0);
            }
        }
    }
}
