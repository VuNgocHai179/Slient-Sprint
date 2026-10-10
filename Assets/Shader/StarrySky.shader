Shader "Custom/StarrySky"
{
    Properties
    {
        _SkyColor ("Sky Color", Color) = (0.02, 0.02, 0.06, 1)
        _StarColor ("Star Color", Color) = (1, 1, 1, 1)
        _StarDensity ("Star Density", Range(10, 500)) = 150.0
        _TwinkleSpeed ("Twinkle Speed", Range(0, 5)) = 1.0

        [Header(Moon)]
        [NoScaleOffset] _MoonTex ("Moon Texture", 2D) = "black" {}
        _MoonTexCenterX ("Moon Tex Center X", Range(0, 1)) = 0.46
        _MoonTexCenterY ("Moon Tex Center Y", Range(0, 1)) = 0.5
        _MoonColor ("Moon Color", Color) = (1, 0.97, 0.9, 1)
        _MoonBrightness ("Moon Brightness", Range(0, 5)) = 1.2
        _MoonSize ("Moon Size (deg radius)", Range(0.2, 30)) = 6.0
        _MoonSoftness ("Moon Edge Softness", Range(0.01, 0.5)) = 0.4
        _MoonElevation ("Moon Elevation (deg)", Range(-90, 90)) = 40.0
        _MoonAzimuth ("Moon Azimuth (deg)", Range(0, 360)) = 200.0
        _MoonGlowColor ("Moon Glow Color", Color) = (0.55, 0.7, 1, 1)
        _MoonGlow ("Moon Glow Strength", Range(0, 3)) = 0.35
        _MoonGlowSize ("Moon Glow Size (x Moon Size)", Range(1, 8)) = 3.0
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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 directionOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyColor;
                half4 _StarColor;
                float _StarDensity;
                float _TwinkleSpeed;
                half4 _MoonColor;
                half4 _MoonGlowColor;
                float _MoonBrightness;
                float _MoonSize;
                float _MoonSoftness;
                float _MoonElevation;
                float _MoonAzimuth;
                float _MoonGlow;
                float _MoonGlowSize;
                float _MoonTexCenterX;
                float _MoonTexCenterY;
            CBUFFER_END

            TEXTURE2D(_MoonTex);
            SAMPLER(sampler_MoonTex);

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
                output.directionOS = input.positionOS.xyz; // Lấy toạ độ không gian vật thể làm vector hướng
                return output;
            }

            // Dựng vector hướng của mặt trăng từ góc ngẩng (elevation) và góc phương vị (azimuth).
            float3 MoonDirection()
            {
                float elev = radians(_MoonElevation);
                float azim = radians(_MoonAzimuth);
                return normalize(float3(cos(elev) * sin(azim), sin(elev), cos(elev) * cos(azim)));
            }

            half4 frag (Varyings input) : SV_Target
            {
                // Chuẩn hoá vector để phóng chiếu hoàn hảo lên một khối cầu (Skybox)
                float3 dir = normalize(input.directionOS);

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

                // ---------------------------------------------------------------- Mặt trăng

                float3 moonDir = MoonDirection();

                // Góc giữa hướng đang nhìn và hướng mặt trăng (radian)
                float moonAngle = acos(clamp(dot(dir, moonDir), -1.0, 1.0));
                float moonRadius = radians(_MoonSize);

                // Dựng hệ trục phẳng vuông góc với hướng mặt trăng để vẽ đĩa.
                // Đổi trục mốc khi mặt trăng ở gần thiên đỉnh để tích có hướng không bị suy biến.
                float3 refUp = abs(moonDir.y) > 0.999 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
                float3 moonRight = normalize(cross(refUp, moonDir));
                float3 moonUp = cross(moonDir, moonRight);

                // Toạ độ cục bộ trên đĩa: (0,0) là tâm mặt trăng, dài 1.0 ở rìa đĩa
                float2 disk = float2(dot(dir, moonRight), dot(dir, moonUp)) / moonRadius;
                float diskRadius = length(disk);

                // Trải ảnh vừa khít vào hình vuông nội tiếp đĩa. Tâm ảnh lấy từ
                // _MoonTexCenterX/Y vì đĩa mặt trăng trong moon.dds không nằm chính giữa texture.
                float2 moonUV = float2(_MoonTexCenterX, _MoonTexCenterY) + disk * 0.5;
                float3 moonTex = SAMPLE_TEXTURE2D(_MoonTex, sampler_MoonTex, moonUV).rgb;

                // moon.dds không có kênh alpha nên dùng độ sáng làm mặt nạ: nền đen tự biến mất,
                // nhờ vậy không lộ viền hình vuông của texture và không cần mask hình tròn cứng.
                float moonLum = max(moonTex.r, max(moonTex.g, moonTex.b));
                float moonBody = smoothstep(0.01, 0.01 + _MoonSoftness * 0.25, moonLum);

                // Bo mềm rìa đĩa, chỉ để chắc chắn mép hình vuông của ảnh không lọt ra ngoài
                float moonRim = 1.0 - smoothstep(0.95, 1.0, diskRadius);
                float moonMask = saturate(moonBody * moonRim);

                float3 moon = moonTex * _MoonColor.rgb * _MoonBrightness * moonMask;

                // Quầng sáng mờ quanh mặt trăng, tắt dần theo khoảng cách góc
                float glowRadius = max(moonRadius * _MoonGlowSize, 0.0001);
                float glow = saturate(1.0 - moonAngle / glowRadius);
                glow = glow * glow * glow * _MoonGlow * (1.0 - moonMask);

                // Màu sắc cuối cùng = Màu trời + Sao (bị mặt trăng che) + Mặt trăng + Quầng sáng
                half4 finalColor;
                finalColor.rgb = _SkyColor.rgb
                               + _StarColor.rgb * starIntensity * (1.0 - moonMask)
                               + moon
                               + _MoonGlowColor.rgb * glow;
                finalColor.a = 1.0;

                return finalColor;
            }
            ENDHLSL
        }
    }
}