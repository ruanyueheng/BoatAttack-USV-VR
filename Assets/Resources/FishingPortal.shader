Shader "BoatAttack/FishingPortal"
{
    Properties
    {
        _Tint("Sea-glass light",Color)=(.42,.84,.91,1)
        _Shoulder("Arch shoulder",Float)=.69
        _Height("Portal height",Float)=3.08
        _Mode("Stardust mode",Float)=0
        _PulseTime("Interaction pulse",Float)=-10
        _Motion("Ambient motion",Range(0,1))=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _Shoulder,_Height,_Mode,_PulseTime,_Motion;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; float2 seed:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; half twinkle:TEXCOORD2; float seed:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A a)
            {
                V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);
                if(_Mode>.5)
                {
                    a.p.y+=sin(_Time.y*.23+a.seed.x*6.28)*.09*_Motion;
                    a.p.x+=sin(_Time.y*.19+a.seed.y*6.28)*.045*_Motion;
                    a.p.z+=cos(_Time.y*.17+a.seed.x*6.28)*.035*_Motion;
                }
                v.p=TransformObjectToHClip(a.p.xyz);v.uv=a.uv;v.fog=ComputeFogFactor(v.p.z);v.seed=a.seed.x;
                v.twinkle=lerp(.48,.36+.44*smoothstep(-.8,.8,sin(_Time.y*.55+a.seed.y*6.28)),_Motion);return v;
            }
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                float4 n=frac(sin(float4(dot(i,float2(127.1,311.7)),dot(i+float2(1,0),float2(127.1,311.7)),dot(i+float2(0,1),float2(127.1,311.7)),dot(i+1,float2(127.1,311.7))))*43758.5453);
                return lerp(lerp(n.x,n.y,f.x),lerp(n.z,n.w,f.x),f.y);
            }
            half4 Frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                if(_Mode>.5)
                {
                    float2 q=abs(v.uv*2-1);
                    half core=exp(-dot(q,q)*8),rays=pow(saturate(1-q.x-q.y),4);
                    half halo=exp(-dot(q,q)*3)*.12;
                    half density=lerp(step(frac(v.seed),.26),1,_Motion);
                    return half4(MixFog(lerp(_Tint.rgb,half3(1,.89,.64),.75),v.fog),(core*.55+rays*.45+halo)*v.twinkle*density);
                }
                float2 q=float2((v.uv.x-.5)*2,max(0,(v.uv.y-_Shoulder)/(1-_Shoulder)));
                float edge=min(1-abs(q.x),1-dot(q,q));clip(edge);
                float t=_Time.y*_Motion;half boundary=1-smoothstep(.01,.11,edge);
                float2 p=(v.uv-float2(.5,.48))*float2(2,2.7);
                float turn=.35*length(p)-t*.045,s=sin(turn),c=cos(turn);
                float2 flow=mul(float2x2(c,-s,s,c),p);
                float cloud=Noise(flow*2.1+float2(t*.018,-t*.028));
                cloud=.66*cloud+.34*Noise(flow*4.2+float2(cloud*.8,t*.035));
                half mist=smoothstep(.22,.8,cloud);
                float radial=length(p+float2(.12,-.12));
                half glow=exp(-radial*radial*1.6);
                float age=_Time.y-_PulseTime;
                half pulse=(age>=0 && age<1.8)?exp(-pow((radial-age*.65)*6,2))*(1-age/1.8)*_Motion:0;
                half3 color=lerp(_Tint.rgb*.60,_Tint.rgb*.85+half3(.23,.24,.21),mist);
                color=lerp(color,half3(.91,.83,.61),boundary*.34)+glow*.07+pulse*.12;
                half alpha=(.94+mist*.055+boundary*.005)*smoothstep(0,.035,edge)*smoothstep(0,.055,v.uv.y);
                return half4(MixFog(color,v.fog),saturate(alpha));
            }
            ENDHLSL
        }
    }
}
