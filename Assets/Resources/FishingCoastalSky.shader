Shader "BoatAttack/FishingCoastalSky"
{
    Properties { _SkyTint("Sky tint",Color)=(.5,.5,.5,1) _Daylight("Daylight",Float)=1 _CoastTime("Cloud time",Float)=0 _SunDirection("Sun direction",Vector)=(0,1,0,0) }
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
            float4 _SkyTint,_SunDirection;float _Daylight,_CoastTime;
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
                float cloud=smoothstep(.64,.83,n)*smoothstep(.025,.16,h)*(1-smoothstep(.65,1,h));
                float3 cloudColor=lerp(float3(.08,.12,.21),float3(.93,.91,.83),day);
                sky=lerp(sky,cloudColor,cloud*.76);
                float halo=pow(saturate(dot(d,normalize(_SunDirection.xyz))),96);
                sky+=float3(1,.72,.37)*halo*.32*day;
                sky*=lerp(float3(.8,.9,1.1),.9+min(_SkyTint.rgb*2,1)*.1,day);
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
