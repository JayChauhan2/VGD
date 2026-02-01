Shader "Hidden/Echolocation"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Ripple Color", Color) = (0, 1, 1, 1)
        _Center ("Center Position", Vector) = (0, 0, 0, 0)
        _Radius ("Current Radius", Float) = 0
        _EdgeWidth ("Edge Width", Float) = 2
        _Darkness ("World Darkness", Range(0, 1)) = 0.9
        
        // 2D Support
        _CameraPos ("Camera Position", Vector) = (0,0,0,0)
        _OrthographicSize ("Ortho Size", Float) = 5
        _AspectRatio ("Aspect Ratio", Float) = 1.77
        _IsOrtho ("Is Orthographic", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Name "EcholocationPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
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
            float _Darkness;
            
            float4 _CameraPos;
            float _OrthographicSize;
            float _AspectRatio;
            float _IsOrtho;

            Varyings vert (Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                float3 worldPos;
                
                // --- World Position Reconstruction ---
                if (_IsOrtho > 0.5)
                {
                    // --- 2D Orthographic Logic ---
                    float2 uvCentered = input.uv - 0.5;
                    float height = _OrthographicSize * 2.0;
                    float width = height * _AspectRatio;
                    
                    float2 worldOffset = uvCentered * float2(width, height);
                    worldPos = float3(_CameraPos.x + worldOffset.x, _CameraPos.y + worldOffset.y, 0); 
                }
                else
                {
                    // --- 3D Depth Logic (Fallback) ---
                    float depth = SampleSceneDepth(input.uv);
                    worldPos = ComputeWorldSpacePosition(input.uv, depth, UNITY_MATRIX_I_VP);
                }

                // --- Darkness & Reveal Logic ---
                
                // 1. Calculate Distance
                float dist = distance(worldPos.xy, _Center.xy); 
                if (_IsOrtho < 0.5) dist = distance(worldPos, _Center.xyz);

                // 2. Base State: Dark Overlay
                float3 finalColor = float3(0, 0, 0);
                float finalAlpha = _Darkness; // Default darkness (e.g. 0.9 or 1.0)
                
                // 3. Reveal Logic (Flashlight Hole)
                // If inside the current radius, make it transparent (reveal game)
                if (dist < _Radius)
                {
                     // Inside the circle: Fully revealed
                     finalAlpha = 0; 
                }

                // 4. Draw the Ripple Ring (The "Wave" front)
                float halfWidth = _EdgeWidth * 0.5;
                float lowerBound = _Radius - halfWidth;
                float upperBound = _Radius + halfWidth;
                
                if (dist > lowerBound && dist < upperBound)
                {
                    float distFromCenter = abs(dist - _Radius);
                    float ringAlpha = 1.0 - (distFromCenter / halfWidth);
                    ringAlpha = pow(ringAlpha, 2); // Smooth falloff
                    
                    // Add the ring color on top
                    finalColor = _Color.rgb;
                    
                    // Make sure the ring itself is visible (opaque)
                    // If we want the ring to be visible over darkness:
                    finalAlpha = max(finalAlpha, ringAlpha * _Color.a);
                }

                return float4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}
