Shader "Hidden/WaterSystem/DepthProbe"
{
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "../Shaders/WaterCommon.hlsl"
            float4 _ProbePosition;
            float4 Vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            float4 Frag(float4 positionCS : SV_POSITION) : SV_Target
            {
                float depth = WaterTextureDepth(_ProbePosition.xyz);
                return float4(depth, depth, depth, 1);
            }
            ENDHLSL
        }
    }
}
