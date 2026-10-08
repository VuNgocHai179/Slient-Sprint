Shader "Custom/StarrySky"
{
    Properties
    {
        _SkyColor ("Sky Color", Color) = (0.02, 0.02, 0.06, 1)
        _StarColor ("Star Color", Color) = (1, 1, 1, 1)
        _StarDensity ("Star Density", Range(10, 500)) = 150.0
        _TwinkleSpeed ("Twinkle Speed", Range(0, 5)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 uv : TEXCOORD0; 
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyColor;
                half4 _StarColor;
                float _StarDensity;
                float _TwinkleSpeed;
            CBUFFER_END

            // Hàm tạo số ngẫu nhiên (Hash function) từ không gian 3 chiều
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.positionOS.xyz; // Lấy toạ độ không gian vật thể làm vector hướng
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // Chuẩn hoá vector để phóng chiếu hoàn hảo lên một khối cầu (Skybox)
                float3 dir = normalize(input.uv);

                // Nhân vector hướng với mật độ để chia không gian thành các ô lưới (grid cells)
                float3 pos = dir * _StarDensity;

                float3 id = floor(pos);     // ID của ô lưới
                float3 f = frac(pos) - 0.5; // Toạ độ cục bộ bên trong ô lưới (-0.5 đến 0.5)

                // Lấy giá trị ngẫu nhiên cho ô lưới hiện tại
                float randVal = hash(id);

                // Chỉ cho phép sao xuất hiện ở 3% số ô lưới để tạo sự thưa thớt tự nhiên
                float starProbability = step(0.97, randVal); 

                // Tính toán hình dáng của ngôi sao (dạng chấm sáng)
                float starDist = length(f);
                float starGlow = smoothstep(0.15, 0.0, starDist);

                // Tạo hiệu ứng lấp lánh (Twinkle) dựa trên thời gian và giá trị ngẫu nhiên
                // Random lệch pha giúp các sao không nhấp nháy cùng lúc
                float twinkle = sin(_Time.y * _TwinkleSpeed + randVal * 100.0) * 0.4 + 0.6;

                // Tổng hợp cường độ sáng của ngôi sao
                float starIntensity = starGlow * starProbability * twinkle * 1.5;

                // Màu sắc cuối cùng = Màu trời + Màu sao
                half4 finalColor = _SkyColor + (_StarColor * starIntensity);

                return finalColor;
            }
            ENDHLSL
        }
    }
}