// 三色漸層天空盒（ART_STYLE「光照與後處理」：兩色或三色漸層，最符合低面數風格）。
// 天頂色 → 地平線色 → 地平線以下的顏色；地平線色和場景的霧色相同，遠景會退進天空裡。
// 太陽方向（URP 的主光）附近加一圈柔光，沒有硬邊的太陽盤。
Shader "BossFight/Sky Gradient"
{
    Properties
    {
        _TopColor ("天頂色", Color) = (0.36, 0.6, 0.86, 1)
        _HorizonColor ("地平線色（= 霧色）", Color) = (0.65, 0.76, 0.69, 1)
        _BottomColor ("地平線以下", Color) = (0.45, 0.52, 0.45, 1)
        _TopExponent ("天頂漸層曲線（越大地平線帶越寬）", Range(0.1, 8)) = 1.6
        _BottomExponent ("地平線以下漸層曲線", Range(0.1, 8)) = 1
        _HorizonOffset ("地平線高度偏移", Range(-0.3, 0.3)) = 0
        _SunGlowColor ("太陽柔光", Color) = (1, 0.92, 0.75, 1)
        _SunGlowIntensity ("太陽柔光強度", Range(0, 2)) = 0.35
        _SunGlowPower ("太陽柔光集中度", Range(1, 256)) = 24
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor, _HorizonColor, _BottomColor, _SunGlowColor;
                float _TopExponent, _BottomExponent, _HorizonOffset, _SunGlowIntensity, _SunGlowPower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                float y = dir.y - _HorizonOffset;
                half3 color = y >= 0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(y), 1.0 / _TopExponent))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-y), 1.0 / _BottomExponent));
                float sun = pow(saturate(dot(dir, _MainLightPosition.xyz)), _SunGlowPower);
                color += _SunGlowColor.rgb * sun * _SunGlowIntensity;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
