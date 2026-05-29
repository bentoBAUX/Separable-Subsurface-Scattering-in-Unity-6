Shader "bentoBAUX/SSSS Util/Compositor"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    TEXTURE2D(_SceneTex);
    SAMPLER(sampler_SceneTex);

    TEXTURE2D(_DiffuseTex);
    SAMPLER(sampler_DiffuseTex);

    TEXTURE2D(_ProcessedDiffuseTex);
    SAMPLER(sampler_ProcessedDiffuseTex);

    TEXTURE2D(_SpecularTex);
    SAMPLER(sampler_SpecularTex);

    TEXTURE2D(_AmbientTex);
    SAMPLER(sampler_AmbientTex);

    float _SSSS_SubsurfaceWeight;

    float4 CompositeFrag(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;

        float3 sceneColour = SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, uv).rgb;
        float3 diffuse = SAMPLE_TEXTURE2D(_DiffuseTex, sampler_DiffuseTex, uv).rgb;
        float3 processed = SAMPLE_TEXTURE2D(_ProcessedDiffuseTex, sampler_ProcessedDiffuseTex, uv).rgb;
        float3 specular = SAMPLE_TEXTURE2D(_SpecularTex, sampler_SpecularTex, uv).rgb;
        float3 ambient = SAMPLE_TEXTURE2D(_AmbientTex, sampler_AmbientTex, uv).rgb;

        // Use the diffuse buffer as a simple mask for pixels rendered by the SSS shader.
        float maskSource = max(diffuse.r, max(diffuse.g, diffuse.b));
        //float mask = step(0.001, maskSource);
        float mask = smoothstep(0.001,0.5, maskSource);

        // Blend between the original diffuse lighting and the blurred SSS result.
        float3 sssRatio = lerp(diffuse, processed, _SSSS_SubsurfaceWeight);

        // Recombine the blurred diffuse with the sharp lighting components.
        float3 sssColour = sssRatio + specular + ambient;

        // Keep non-SSS scene objects untouched.
        float3 finalColour = lerp(sceneColour, sssColour, mask);

        return float4(finalColour, 1.0);
    }

    float4 CopyFrag(Varyings input) : SV_Target
    {
        return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        // Material pass 0 is our compositor.
        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFrag
            ENDHLSL
        }

        // Material pass 1 copies the output from the compositor into the active screen texture.
        Pass
        {
            Name "Copy"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CopyFrag
            ENDHLSL
        }
    }
}