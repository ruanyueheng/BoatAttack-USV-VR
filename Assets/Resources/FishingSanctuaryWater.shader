Shader "BoatAttack/FishingSanctuaryWater"
{
    Properties
    {
        _DeepColor("Deep water",Color)=(.018,.17,.20,1)
        _ShallowColor("Shallow water",Color)=(.09,.39,.34,1)
        _FlowVertical("Falling stream",Float)=0
        _WaveAmplitude("Wave height in metres",Float)=.012
        _WaveFrequency("Wave frequency",Float)=1.7
        _UseVertexColor("Basin edge weights",Float)=0
        _Ripple("Contact ripple",Vector)=(0,0,-100,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor,_ShallowColor;
            float4 _Ripple;
            half _FlowVertical,_WaveAmplitude,_WaveFrequency,_UseVertexColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half fog:TEXCOORD1; half3 normalWS:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A input)
            {
                V output=(V)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                float2 p=output.positionWS.xz;float t=_Time.y;
                float weight=lerp(1,input.color.r,_UseVertexColor);
                float age=t-_Ripple.z;float distance=length(p-_Ripple.xy);
                float ripple=sin(distance*10-age*9)*exp(-distance*.6)*saturate(1-age/3)*step(0,age)*_Ripple.w;
                output.positionWS.y+=(sin(p.x*_WaveFrequency+t*.9)*cos(p.y*_WaveFrequency*.71-t*.7)+ripple)*_WaveAmplitude*weight;
                output.positionCS=TransformWorldToHClip(output.positionWS);output.fog=ComputeFogFactor(output.positionCS.z);return output;
            }
            half4 Frag(V input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.positionWS.xz;float t=_Time.y;
                half3 wave=normalize(half3(-cos(p.x*_WaveFrequency+t*.9)*cos(p.y*_WaveFrequency*.71-t*.7)*_WaveAmplitude*_WaveFrequency,1,
                    sin(p.x*_WaveFrequency+t*.9)*sin(p.y*_WaveFrequency*.71-t*.7)*_WaveAmplitude*_WaveFrequency*.71));
                half3 n=normalize(lerp(input.normalWS,wave,saturate(input.normalWS.y)));
                if(_FlowVertical>.5h)n=normalize(cross(ddx(input.positionWS),ddy(input.positionWS)));
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);n*=dot(n,view)<0?-1:1;
                half fresnel=.02+.98*pow(1-saturate(dot(n,view)),5);
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float raw=SampleSceneDepth(uv);float depth=max(0,LinearEyeDepth(raw,_ZBufferParams)+TransformWorldToView(input.positionWS).z);
                half shallow=exp(-depth*1.4);half3 base=lerp(_DeepColor.rgb,_ShallowColor.rgb,shallow);
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));half3 ambient=SampleSH(n);
                half3 tint=base*(ambient*.8+sun.color*(.28+.55*saturate(dot(n,sun.direction)))*sun.shadowAttenuation);
                // One existing opaque-color sample and the environment probe avoid a second scene-rendering camera.
                half3 through=SampleSceneColor(saturate(uv+n.xz*.006*saturate(depth*2)));
                half3 reflection=GlossyEnvironmentReflection(reflect(-view,n),input.positionWS,.12h,1.0h);
                half3 color=lerp(lerp(through,tint,1-exp(-depth*1.1)),reflection,saturate(fresnel*.8+.08));
                color+=sun.color*pow(saturate(dot(n,normalize(sun.direction+view))),110)*.38*sun.shadowAttenuation;
                half foam=(1-smoothstep(.02,.17,depth))*(.5+.5*sin(p.x*7+p.y*4+t));color=lerp(color,half3(.6,.73,.69),foam*.35);
                if(_FlowVertical>.5h)
                {
                    half streak=pow(saturate(sin(p.y*85+sin(input.positionWS.y*6-t*7)*.4)*.5+.5),14);
                    color=lerp(half3(.09,.32,.29),half3(.73,.91,.86),streak*.72);
                    return half4(MixFog(color,input.fog),.38+streak*.42);
                }
                return half4(MixFog(color,input.fog),.92);
            }
            ENDHLSL
        }
    }
}
