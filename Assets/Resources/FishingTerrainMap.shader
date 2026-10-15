Shader "Fishing/TerrainMap"
{
    Properties
    {
        _MainTex ("Baked terrain", 2D) = "white" {}
        _ViewUV ("World UV window (not the circular mask)", Vector) = (0,0,1,1)
        _HasMap ("Valid capture", Float) = 0
        _Circular ("Circular desktop map", Float) = 1
        _ZTest ("Depth test", Float) = 8
        _Water ("Water", Color) = (.76,.85,.82,1)
        _Low ("Low shore", Color) = (.91,.82,.63,1)
        _Land ("Land", Color) = (.66,.74,.58,1)
        _High ("High rock", Color) = (.77,.75,.68,1)
        _Unknown ("Unknown paper", Color) = (.88,.84,.74,1)
        _Hatch ("Unknown hatching", Color) = (.75,.72,.64,1)
        [HideInInspector] _SourceWater ("Baked water", Color) = (.10,.24,.28,1)
        [HideInInspector] _SourceLow ("Baked low shore", Color) = (.73,.69,.54,1)
        [HideInInspector] _SourceLand ("Baked land", Color) = (.35,.46,.36,1)
        [HideInInspector] _SourceHigh ("Baked high rock", Color) = (.66,.68,.66,1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            ZTest [_ZTest] ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _ViewUV;
            float _HasMap;
            float _Circular;
            half4 _Water, _Low, _Land, _High, _Unknown, _Hatch;
            half4 _SourceWater, _SourceLow, _SourceLand, _SourceHigh;
            struct AppData { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 position : SV_POSITION; float2 localUV : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(AppData input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.localUV = input.uv;
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                // Local UV supplies the circle. World UV supplies ONLY the texture sample.
                float d = length(input.localUV * 2 - 1);
                clip(1 - d * _Circular);
                float edge = lerp(1, saturate((1 - d) / max(fwidth(d), .0001)), _Circular);
                float2 worldUV = _ViewUV.xy + input.localUV * _ViewUV.zw;
                float known = _HasMap * step(0, worldUV.x) * step(worldUV.x, 1)
                    * step(0, worldUV.y) * step(worldUV.y, 1);
                // Sampling may clamp at the hardware edge; the outside is ALWAYS replaced by unknown.
                half3 sampleColor = tex2D(_MainTex, worldUV).rgb;
                // The bake retains elevation data in its original palette; both UIs restyle that same data.
                half3 lowDelta = _SourceLand.rgb - _SourceLow.rgb;
                half3 highDelta = _SourceHigh.rgb - _SourceLand.rgb;
                half lowT = saturate(dot(sampleColor - _SourceLow.rgb, lowDelta) / max(dot(lowDelta, lowDelta), .0001));
                half highT = saturate(dot(sampleColor - _SourceLand.rgb, highDelta) / max(dot(highDelta, highDelta), .0001));
                half3 lowError = sampleColor - lerp(_SourceLow.rgb, _SourceLand.rgb, lowT);
                half3 highError = sampleColor - lerp(_SourceLand.rgb, _SourceHigh.rgb, highT);
                half3 waterError = sampleColor - _SourceWater.rgb;
                half lowDistance = dot(lowError, lowError), highDistance = dot(highError, highError);
                half3 terrain = lowDistance < highDistance ? lerp(_Low.rgb, _Land.rgb, lowT) : lerp(_Land.rgb, _High.rgb, highT);
                sampleColor = dot(waterError, waterError) < min(lowDistance, highDistance) ? _Water.rgb : terrain;
                float hatch = step(.72, frac((input.position.x + input.position.y) / 12));
                half3 unknown = lerp(_Unknown.rgb, _Hatch.rgb, hatch);
                return half4(lerp(unknown, sampleColor, known) * input.color.rgb, edge * input.color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
