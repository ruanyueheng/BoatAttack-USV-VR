Shader "Fishing/MapBake"
{
    Properties
    {
        _LowColor ("低岸高程色", Color) = (.73,.69,.54,1)
        _LandColor ("中段高程色", Color) = (.35,.46,.36,1)
        _HighColor ("高岩高程色", Color) = (.66,.68,.66,1)
        _SeaLevel ("实际平均水面", Float) = 0
        _Include ("包含子网格", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _LowColor, _LandColor, _HighColor;
            float _SeaLevel, _Include;
            CBUFFER_END
            struct AppData { float4 vertex : POSITION; };
            struct Varyings { float4 position : SV_POSITION; float height : TEXCOORD0; };
            Varyings vert(AppData input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.vertex.xyz);
                output.position = TransformWorldToHClip(world);
                output.height = world.y;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                clip(_Include - .5);
                float height = input.height - _SeaLevel;
                clip(height);
                // 仅用真实世界高程分层配色；未生成法线时不虚构坡面阴影、水深或植被分类。
                half3 low = lerp(_LowColor.rgb, _LandColor.rgb, smoothstep(.5, 5, height));
                half3 color = lerp(low, _HighColor.rgb, smoothstep(8, 20, height));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
