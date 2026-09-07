Shader "Tanki/Bombardment Shockwave"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1.0, 0.76, 0.24, 0.42)
        _DistortionStrength("Distortion Strength", Range(0.0, 0.04)) = 0.018
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BombardmentShockwave"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _DistortionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(saturate(1.0h - abs(dot(normalWS, viewDirectionWS))), 1.45h);
                half ripple = 0.72h + 0.28h * sin(dot(normalWS.xz, half2(8.7h, 6.1h)) + _Time.y * 19.0h);

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 normalVS = mul((half3x3)GetWorldToViewMatrix(), normalWS);
                float2 offset = normalVS.xy * (_DistortionStrength * fresnel * ripple);
                half3 refractedScene = SampleSceneColor(saturate(screenUv + offset));

                half alpha = _BaseColor.a * lerp(0.22h, 1.0h, fresnel);
                half3 color = lerp(refractedScene, _BaseColor.rgb * 1.65h, alpha * 0.32h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
