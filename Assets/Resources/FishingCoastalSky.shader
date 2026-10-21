Shader "BoatAttack/FishingCoastalSky"
{
    Properties { _SkyTint("Sky tint",Color)=(.5,.5,.5,1) _Daylight("Daylight",Float)=1 _CoastTime("Cloud time",Float)=0 _SunDirection("Sun direction",Vector)=(0,1,0,0) _Storm("Storm cover",Range(0,1))=0 _CloudCover("Cloud cover",Range(0,1))=.18 _Lightning("Distant lightning",Range(0,1))=0 _LightningDirection("Lightning direction",Vector)=(0,.28,1,0) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _SkyTint,_SunDirection,_LightningDirection;float _Daylight,_CoastTime,_Storm,_CloudCover,_Lightning;
            struct A { float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.p=TransformObjectToHClip(a.p.xyz);v.direction=a.p.xyz;return v;}
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
            half4 Frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float3 d=normalize(v.direction);float h=saturate(d.y);float day=saturate(_Daylight);
                float warm=4*day*(1-day);
                float3 top=lerp(float3(.012,.023,.058),float3(.065,.25,.48),day);
                float3 horizon=lerp(float3(.035,.065,.12),lerp(float3(.46,.69,.79),float3(.90,.52,.30),warm*.7),day);
                float3 sky=lerp(horizon,top,pow(h,.45));
                float2 p=d.xz/(max(d.y,0)+.34)*3.4+float2(_CoastTime*.003,0);
                float n=Noise(p)*.58+Noise(p*2.07)*.28+Noise(p*4.11)*.14;
                float coverage=saturate(_CloudCover),storm=saturate(_Storm);
                float cloud=smoothstep(lerp(.64,.30,coverage),lerp(.83,.60,coverage),n)*smoothstep(.015,.12,h);
                float3 cloudColor=lerp(float3(.08,.12,.21),float3(.93,.91,.83),day);
                sky=lerp(sky,cloudColor,cloud*.76);
                float under=0;
                if(storm>.001)
                {
                    // Broad continuous overcast replaces the blue hemisphere during rain.
                    float2 heavyP=d.xz/(max(d.y,0)+.23)*1.8+float2(_CoastTime*.004,-_CoastTime*.0017);
                    float broad=Noise(heavyP)*.65+Noise(heavyP*2.13)*.25+Noise(heavyP*5.1)*.10;
                    under=smoothstep(.32,.76,broad);
                    float3 stormTop=lerp(float3(.018,.025,.045),float3(.105,.135,.18),day);
                    float3 stormHorizon=lerp(float3(.035,.045,.065),float3(.30,.35,.39),day);
                    float3 overcast=lerp(stormHorizon,stormTop,pow(h,.38));
                    overcast*=lerp(.65,1.18,under);
                    float folds=smoothstep(.35,.73,Noise(heavyP*3.1+float2(3.7,8.1)));
                    overcast+=float3(.035,.039,.045)*folds*(.25+.75*day);
                    sky=lerp(sky,overcast,storm);
                }
                float halo=pow(saturate(dot(d,normalize(_SunDirection.xyz))),96);
                sky+=float3(1,.72,.37)*halo*.32*day*(1-storm)*(1-coverage*.8);
                sky*=lerp(float3(.8,.9,1.1),.9+min(_SkyTint.rgb*2,1)*.1,day);
                if(_Lightning>.001 && storm>.01)
                {
                    float3 centre=normalize(_LightningDirection.xyz);
                    float3 right=normalize(cross(float3(0,1,0),centre));
                    float3 up=cross(centre,right);
                    float2 q=float2(dot(d,right),dot(d,up));
                    float y=saturate((q.y+.13)/.27);
                    float zig=(sin(y*23)+sin(y*57)*.35)*.014;
                    float trunk=1-smoothstep(.001,.003,abs(q.x-zig));
                    trunk*=step(-.13,q.y)*step(q.y,.14);
                    float fork=1-smoothstep(.0007,.0022,abs(q.x-zig-(.04-q.y)*.42));
                    fork*=step(-.055,q.y)*step(q.y,.04);
                    float bolt=saturate(trunk+fork*.6)*step(.8,dot(d,centre));
                    sky+=float3(.22,.28,.36)*_Lightning*storm*(.4+under*.6);
                    sky+=float3(.85,.91,1)*bolt*_Lightning*3*storm;
                }
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
