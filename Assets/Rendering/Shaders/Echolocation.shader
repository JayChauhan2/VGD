Shader "Hidden/Echolocation"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Ripple Color", Color) = (1, 1, 1, 1)
        _Center ("Center Position", Vector) = (0, 0, 0, 0)
        _Radius ("Current Radius", Float) = 0
        _EdgeWidth ("Edge Width", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always // Always draw on top (or handle depth manually?)
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha // Alpha blending

        Pass
        {
            Name "EcholocationPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Core URP libraries
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _Color;
            float4 _Center;
            float _Radius;
            float _EdgeWidth;

            Varyings vert (Attributes input)
            {
                Varyings output;
                
                // Standard Fullscreen Vertex Shader logic
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                // 1. Sample Scene Depth
                float depth = SampleSceneDepth(input.uv);

                // 2. Reconstruct World Position
                // We need to unproject the screen UV + Depth to World Space.
                // URP Core.hlsl provides ComputeWorldSpacePosition, but it usually needs depth in raw or linear.
                // A common way in URP for full screen:
                float3 worldPos = ComputeWorldSpacePosition(input.uv, depth, UNITY_MATRIX_I_VP);
                
                // 3. Calculate distance from Center
                float dist = distance(worldPos, _Center.xyz);
                
                // 4. Determine if we are on the ripple ring
                // Ripple is at _Radius. Width is _EdgeWidth.
                // We want a value of 1.0 when dist == _Radius, and falloff to 0.0 at _Radius +/- _EdgeWidth
                
                float halfWidth = _EdgeWidth * 0.5;
                float lowerBound = _Radius - halfWidth;
                float upperBound = _Radius + halfWidth;

                float alpha = 0;

                if (dist > lowerBound && dist < upperBound)
                {
                    // Simple linear fade or hard edge? Let's do a smooth bell curve or linear fade.
                    // 1 at center, 0 at edges.
                    float distFromCenterOfRing = abs(dist - _Radius);
                    alpha = 1.0 - (distFromCenterOfRing / halfWidth);
                }
                
                // 5. Check if it's the skybox
                // Scene depth is usually 0 or 1 at the far plane (depending on Z-buffer direction).
                // Usually LinearEyeDepth gives a large value.
                // To be safe, if using Raw depth, check against 0 (Reverse Z) or 1 (Standard Z).
                #if UNITY_REVERSED_Z
                    if(depth <= 0.0001) alpha = 0;
                #else
                    if(depth >= 0.9999) alpha = 0;
                #endif

                return float4(_Color.rgb, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
