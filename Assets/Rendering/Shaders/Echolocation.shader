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

            float4 _PlayerPos;
            float _PlayerRadius;

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

                // 2. Base State: Dark Overlay (Default)
                float3 finalColor = float3(0, 0, 0);
                float finalAlpha = _Darkness; 
                
                // --- Player Permanent Light Logic ---
                float distPlayer = distance(worldPos.xy, _PlayerPos.xy);
                // Simple reveal circle with slight soft edge
                if (distPlayer < _PlayerRadius)
                {
                     // Soft edge math - Vision-like fade
                     // Start fading at 30% of radius, fully dark at 100%
                     float alphaFactor = smoothstep(_PlayerRadius * 0.3, _PlayerRadius, distPlayer);
                     
                     // We want center = Visible (Alpha 0), Outside = Dark
                     // So mix current Alpha with 0 based on Factor
                     finalAlpha = min(finalAlpha, lerp(0.0, _Darkness, alphaFactor));
                }

                // 3. Ring Logic (Reveal + Color)
                float halfWidth = _EdgeWidth * 0.5;
                float lowerBound = _Radius - halfWidth;
                float upperBound = _Radius + halfWidth;
                
                if (dist < lowerBound)
                {
                    // Inside the Inner Circle (Behind the wave):
                    // User wants this to remain dark (no trail).
                    // So we keep finalAlpha as determined by Base State + Player Logic.
                }
                else if (dist < upperBound)
                {
                    // Inside the Ring (The Wave Front):
                    
                    // A. Calculate Ring Gradient
                    float distFromCenterOfRing = abs(dist - _Radius);
                    float ringGradient = 1.0 - (distFromCenterOfRing / halfWidth);
                    // Use sqrt (power 0.5) to make the curve "fat" (plateau-like) instead of "peaky"
                    ringGradient = pow(ringGradient, 0.5); 
                    
                    // B. Reveal Logic:
                    float targetAlpha = 0.0;
                    float ringAlpha = lerp(_Darkness, targetAlpha, ringGradient);
                    
                    finalAlpha = min(finalAlpha, ringAlpha);
                    
                    // C. Color Overlay (Only apply color if Ring is dominant source of visibility?)
                    // If we are close to player, maybe don't tint?
                    // For now, tint if inside ring band.
                    finalColor = _Color.rgb;
                }

                return float4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}
