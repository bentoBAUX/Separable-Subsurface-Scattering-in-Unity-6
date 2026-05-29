Shader "bentoBAUX/SSSS Util/ArtistFriendlyKernel"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float _SSSS_NearFarBalance;
    float3 _SSSS_NearSigma;
    float3 _SSSS_FarSigma;
    float _SSSS_ScatterScale;
    int _SSSS_StepCount;

    SAMPLER(sampler_BlitTexture);

    float Gaussian1D(float r, float sigma)
    {
        const float INV_SQRT_2PI = 0.3989422804;
        return INV_SQRT_2PI / sigma * exp(-(r * r) / (2.0 * sigma * sigma));
    }

    float4 Convolve(float2 uv, float2 direction)
    {
        float2 texelSize = 1.0 / _ScreenParams.xy;

        float3 sum = 0.0;
        float3 totalWeight = 0.;

        float balance = saturate(_SSSS_NearFarBalance);

        float spreadMultiplier = 2; // I have added a spread multiplier for aesthetics reasons.
        float scatterRadius = max(_SSSS_ScatterScale * spreadMultiplier, 0.01);

        float3 nearSigma = max(_SSSS_NearSigma.rgb * scatterRadius, 0.01);
        float3 farSigma = max(_SSSS_FarSigma.rgb * scatterRadius, 0.01);

        for (int i = -_SSSS_StepCount; i <= _SSSS_StepCount; i++)
        {
            float offset = abs((float)i);

            // Move along the chosen blur axis and keep samples inside the screen.
            float2 sampleUV = clamp(uv + direction * texelSize * i, 0., 1.);
            float3 sampleColour = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, sampleUV).rgb;

            // Calculate the weights w_i of u's every neighbouring pixels u_i.
            float3 w_i;
            w_i.r = balance * Gaussian1D(offset, max(nearSigma.r, 0.01)) + (1 - balance) * Gaussian1D(offset, max(farSigma.r, 0.01));
            w_i.g = balance * Gaussian1D(offset, max(nearSigma.g, 0.01)) + (1 - balance) * Gaussian1D(offset, max(farSigma.g, 0.01));
            w_i.b = balance * Gaussian1D(offset, max(nearSigma.b, 0.01)) + (1 - balance) * Gaussian1D(offset, max(farSigma.b, 0.01));

            // Accumulate the weighted colour and track the total weight for normalisation.
            sum += sampleColour * w_i;
            totalWeight += w_i;
        }

        return float4(sum / max(totalWeight, 0.0001), 1.0);
    }

    float4 HorizontalBlur(Varyings input) : SV_Target
    {
        return Convolve(input.texcoord, float2(1, 0));
    }

    float4 VerticalBlur(Varyings input) : SV_Target
    {
        return Convolve(input.texcoord, float2(0, 1));
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite Off
        ZTest Always
        Cull Off

        // Pass 0 performs the horizontal blur
        Pass
        {
            Name "Horizontal Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment HorizontalBlur
            ENDHLSL
        }

        // Pass 1 performs the vertical blur
        Pass
        {
            Name "Vertical Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment VerticalBlur
            ENDHLSL
        }
    }
}