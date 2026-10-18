Shader "BoatAttack/FishingNightSky"
{
    Properties { _Tint("Tint",Color)=(1,1,1,1) _Visibility("Visibility",Range(0,1))=1 _Moon("Moon",Float)=0 _Solid("Solid lens",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;float _Visibility;float _Moon;float _Solid;
            CBUFFER_END
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.color=i.color;return o;}
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.uv-.5;float radius=length(p);float disc=1-smoothstep(.41,.47,radius);
                float crescent=smoothstep(.345,.362,length(p-float2(.17,.085)));
                float grain=.90+.10*sin(p.x*59+sin(p.y*43))*sin(p.y*67);
                float star=pow(saturate(1-radius*2),2);
                float alpha=lerp(lerp(star,disc*crescent,_Moon),1,_Solid)*_Visibility*i.color.a;
                return half4(_Tint.rgb*i.color.rgb*lerp(1,grain,_Moon),alpha*_Tint.a);
            }
            ENDHLSL
        }
    }
}
