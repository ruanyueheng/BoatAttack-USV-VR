using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>V044 动态天空、室内无雨、天气玩法：日月轨迹/月相/星空旋转/流星，渔屋内雨滴遮挡，雨天咬钩与晃船、风速；截图存 Logs/V044/sky。</summary>
    public static class FishingV044SkyValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V044-SKY");
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-SKY] CHECK: "+label);}
        private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
        private static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
        private static void Select(FishingWeather weather,BayWeather preset)
        {
            for(int i=0;i<48;i++)weather.Climate.Tick(.25f,false);
            if(weather.Climate.Current!=preset)Check(weather.Climate.Select(preset),"Can select "+preset);
            for(int i=0;i<48;i++)weather.Climate.Tick(.25f,false);
            weather.ApplyEnvironment();
        }
        private static void Hour(FishingPrototype c,float hour){c.Career.Calendar.AdvanceToHour(hour);c.Weather.ApplyEnvironment();c.Nightscape.Apply(0);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Only the isolated automation profile is used");
            const string cumulus="Assets/Resources/FishingSkyCumulus.png";
            UnityEditor.AssetDatabase.ImportAsset(cumulus,UnityEditor.ImportAssetOptions.ForceUpdate);
            var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(cumulus);
            Debug.Log("[V044-SKY] cumulus import sRGB="+importer.sRGBTexture+" max="+importer.maxTextureSize+" wrap="+importer.wrapMode+" hash="+UnityEditor.AssetDatabase.GetAssetDependencyHash(cumulus)+" fileMD5="+BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(File.ReadAllBytes(cumulus))).Replace("-",""));
            Check(!importer.sRGBTexture,"Cumulus atlas imports as linear data");
            var weather=c.Weather;var sky=RenderSettings.skybox;var camera=c.viewCamera.transform;
            sky.SetTexture("_CumulusTex",Resources.Load<Texture2D>("FishingSkyCumulus"));
            string output=Path.GetFullPath("Logs/V044/sky");Directory.CreateDirectory(output);
            c.ReturnToHarbor();if(c.StartMenuOpen)c.FinishTitleVoyage();if(c.Paused)c.TogglePause();
            Check(sky.shader.name=="BoatAttack/FishingCoastalSky" && sky.GetTexture("_NoiseTex")!=null,"Sky uses one precomputed tileable noise texture");

            // Rules: weather changes play.
            Check(BayClimate.BiteScale(BayWeather.Rain,FishKind.Coral)<BayClimate.BiteScale(BayWeather.Clear,FishKind.Coral),"Rain makes fish bite sooner");
            foreach(BayWeather w in Enum.GetValues(typeof(BayWeather)))Check(!string.IsNullOrEmpty(BayClimate.FishingEffect(w)),"Forecast explains fishing effect for "+w);

            // A moon near full so the night pair shows it.
            for(int i=0;i<30 && Mathf.Abs(weather.MoonPhase-.5f)>.1f;i++){Hour(c,9);Hour(c,21);}
            Check(Mathf.Abs(weather.MoonPhase-.5f)<=.1f,"Moon phase cycles through full within a season");
            float phase=weather.MoonPhase;Hour(c,9);Hour(c,21);
            Check(Mathf.Abs(Mathf.DeltaAngle(phase*360,weather.MoonPhase*360)-360f/FishingCalendar.DaysPerSeason)<1,"Moon phase advances one step per day");

            var view=c.Home.Origin+new Vector3(22,10,-48);
            void Look(Vector3 dir){camera.SetPositionAndRotation(view,Quaternion.LookRotation(dir));}
            Select(weather,BayWeather.Clear);
            Hour(c,12);var noon=weather.SunDirection;Check(noon.y>.8f,"Noon sun is high");
            Look(new Vector3(-.2f,.18f,1));FishingV016Validation.Capture(c,output,"sky-day");
            Select(weather,BayWeather.Sunset);Hour(c,17.9f);
            Check(weather.SunDirection.y<.08f && weather.SunDirection.y>-.05f,"Sunset sun sits on the horizon ("+weather.SunDirection.y+")");
            camera.SetPositionAndRotation(c.Home.Origin+Vector3.up*38,Quaternion.LookRotation(Vector3.ProjectOnPlane(weather.SunDirection,Vector3.up).normalized+Vector3.up*.05f));FishingV016Validation.Capture(c,output,"sky-sunset");
            Hour(c,6.2f);camera.SetPositionAndRotation(c.Home.Origin+Vector3.up*38,Quaternion.LookRotation(Vector3.ProjectOnPlane(weather.SunDirection,Vector3.up).normalized+Vector3.up*.05f));FishingV016Validation.Capture(c,output,"sky-sunrise");

            var cards=weather.CloudCards;
            Check(cards!=null && cards.sharedMaterials.Length==1 && cards.GetComponent<MeshFilter>().sharedMesh.vertexCount==FishingWeather.CardMax*4,"Cumulus cards are one mesh, one material (single draw call)");
            Check(weather.CloudCardCount<=FishingWeather.CardMax,"Card budget bounded");
            bool reduced=weather.ReducedEffects;if(!reduced)weather.ToggleReducedEffects();weather.ApplyEnvironment();
            Check(weather.CloudCardCount==6 && Shader.GetGlobalFloat("_FishingSkyLowEnd")>.5f,"Low-end sky: 6 cards and per-pixel card fade off");
            weather.ToggleReducedEffects();weather.ApplyEnvironment();
            Check(weather.CloudCardCount==FishingWeather.CardMax && Shader.GetGlobalFloat("_FishingSkyLowEnd")<.5f,"Desktop sky: 12 cards with card fade");
            if(reduced)weather.ToggleReducedEffects();
            Check(c.viewCamera.farClipPlane>=FishingWeather.CardDistance+200,"Camera far plane ("+c.viewCamera.farClipPlane+") keeps the cloud cards 200m inside it");
            Check(c.viewCamera.cullingMask==(c.viewCamera.cullingMask|(1<<cards.gameObject.layer)) && cards.bounds.Intersects(new Bounds(c.viewCamera.transform.position,Vector3.one*2600)),"Cumulus cards sit in the camera's sky");
            Debug.Log("[V044-SKY] cumulus cards: renderers=1 quads="+weather.CloudCardCount+" far="+c.viewCamera.farClipPlane);
            // Same-camera cycle for the Little Prince lamplighter comparison.
            Hour(c,17.9f);var west=Quaternion.LookRotation(Vector3.ProjectOnPlane(weather.SunDirection,Vector3.up).normalized+Vector3.up*.22f);
            foreach(var (hour,label,preset) in new[]{(11.5f,"cmp-1-day",BayWeather.Clear),(17.9f,"cmp-2-sunset",BayWeather.Sunset),(18.85f,"cmp-3-dusk",BayWeather.Sunset),(22.5f,"cmp-4-night",BayWeather.Clear)})
            {Select(weather,preset);Hour(c,hour);camera.SetPositionAndRotation(c.Home.Origin+new Vector3(0,9,0),west);FishingV016Validation.Capture(c,output,label);}
            Select(weather,BayWeather.Clear);Hour(c,22);
            Check(weather.MoonDirection.y>.15f,"Near-full moon is up at night ("+weather.MoonDirection+")");
            Check(c.Nightscape.Visibility>.95f && sky.GetFloat("_StarVisibility")>.95f,"Clear night shows stars");
            Check(Vector3.Distance(c.Nightscape.Moon.position-c.viewCamera.transform.position,weather.MoonDirection*c.Nightscape.Moon.localPosition.magnitude)<5,"Moon marker follows the sky moon");
            var moonA=weather.MoonDirection;var starsA=sky.GetMatrix("_StarRotation");
            var nightDir=Vector3.ProjectOnPlane(moonA,Vector3.up).normalized+Vector3.up*.55f;
            Look(nightDir);FishingV016Validation.Capture(c,output,"sky-night-2200");
            Hour(c,22.5f);
            Check(Vector3.Angle(moonA,weather.MoonDirection)>3,"Moon moves over 30 game minutes ("+Vector3.Angle(moonA,weather.MoonDirection)+" deg)");
            Check(Quaternion.Angle(starsA.rotation,sky.GetMatrix("_StarRotation").rotation)>6,"Stars rotate about the pole over 30 game minutes");
            Look(nightDir);FishingV016Validation.Capture(c,output,"sky-night-2230");
            Set(weather,"_meteorAge",.35f);Set(weather,"_meteorHead",(nightDir.normalized+camera.right*.25f+Vector3.up*.1f).normalized);
            Set(weather,"_meteorDir",Vector3.ProjectOnPlane(-camera.right-Vector3.up*.5f,nightDir.normalized).normalized);
            weather.ApplyEnvironment();Check(sky.GetVector("_Meteor").w>0,"Meteor reaches the sky shader");
            FishingV016Validation.Capture(c,output,"sky-night-meteor");Set(weather,"_meteorAge",-1f);

            weather.EventFog=1;Set(weather,"_eventFog",1f);weather.ApplyEnvironment();
            Check(RenderSettings.fogEndDistance<=50,"Sea-event fog thickens visibility to about 45m");
            weather.EventFog=0;Set(weather,"_eventFog",0f);weather.ApplyEnvironment();
            Check(RenderSettings.fogEndDistance>100,"Sea-event fog releases");
            // Rain: windier sea and indoor shelter.
            Hour(c,11);Select(weather,BayWeather.Rain);
            Check(weather.WindStrength>.5f && weather.RainRollLimit<=6,"Rain brings wind and a bounded comfort roll");
            Look(new Vector3(-.2f,.18f,1));weather.RefreshForCapture();FishingV016Validation.Capture(c,output,"sky-rain-outdoor");
            var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=c.Home.Berth+Vector3.up*.7f;rb.rotation=Quaternion.identity;
            c.Player.transform.SetPositionAndRotation(rb.position,Quaternion.identity);Physics.SyncTransforms();yield return null;
            Check(c.Shore.TryDisembark() && c.Shore.Relocate(c.Home.Origin+new Vector3(0,.2f,-3.5f),Quaternion.identity),"Character stands inside the cottage");
            for(int i=0;i<10;i++)yield return null;
            Check(c.Home.Inside,"Cottage reports inside");
            weather.RefreshForCapture();
            var rain=weather.transform.Find("Bounded coastal rain");var verts=Field<Vector3[]>(weather,"_rainVertices");
            int inside=0,outside=0;
            for(int i=0;i<96;i++)
            {
                if((verts[i*4]-verts[i*4+2]).sqrMagnitude<1e-8f)continue;
                var p=rain.TransformPoint(verts[i*4]);
                bool inHouse=Mathf.Abs(p.x-c.Home.Origin.x)<5.5f && Mathf.Abs(p.z-c.Home.Origin.z)<4.5f && p.y<c.Home.Origin.y+3.2f;
                if(inHouse)inside++;else outside++;
            }
            Debug.Log("[V044-SKY] rain drops inside="+inside+" outside="+outside);
            Check(inside==0,"No rain drops fall inside the cottage");
            Check(outside>0,"Rain stays visible outside the cottage");
            c.Weather.SendMessage("Update");
            Check(weather.Sheltered,"Camera inside the cottage counts as sheltered (roof overhead)");
            FishingV016Validation.Capture(c,output,"sky-rain-indoor");
            Check(c.TackleShop==null || !c.TackleShop.Available || weather.Indoor(c.TackleShop.transform.position+Vector3.up*1.5f),"Tackle shop interior is an indoor volume");
            Check(weather.Indoor(c.Home.LighthouseLens-Vector3.up*6),"Lighthouse tower interior is an indoor volume");
            Check(!weather.Indoor(c.Home.Origin+new Vector3(0,1,16)),"Open ground outside stays outdoors");
            camera.SetPositionAndRotation(c.Home.Origin+new Vector3(0,1.6f,-3.5f),Quaternion.LookRotation(new Vector3(0,0,-1)));
            FishingV016Validation.Capture(c,output,"sky-rain-indoor-door");
            Select(weather,BayWeather.Clear);
            yield return null;
        }
    }
}
