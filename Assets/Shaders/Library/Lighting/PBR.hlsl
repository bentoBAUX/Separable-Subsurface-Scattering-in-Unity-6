#ifndef PBR_INCLUDED
#define PBR_INCLUDED

#include "Assets/Shaders/Library/Maths/PBRHelper.hlsl"

// https://learnopengl.com/PBR/Lighting
inline float3 PBR(Surf surfaceData, Light lightData)
{
    float3 c = surfaceData.baseColor.rgb;
    float3 n = normalize(surfaceData.normalWS);
    float3 v = normalize(surfaceData.viewDirectionWS);
    float3 l = normalize(lightData.direction);
    float3 h = normalize(v + l);
    
    float shadow = lerp(0.005, 1, lightData.shadowAttenuation); // Avoid total black (looks shit)
    float3 radiance = lightData.color.rgb * lightData.distanceAttenuation * shadow;
    float roughness = max(surfaceData.roughness, 0.02);
    float metallic = surfaceData.metallic;

    float3 F0 = lerp(0.04, c, metallic);

    float NDF = DistributionGGX(n, h, roughness);
    float G = GeometrySmith(n, v, l, roughness);
    float3 F = fresnelSchlick(max(dot(h, v), 0.0), F0);

    float3 kS = F;
    float3 kD = float3(1, 1, 1) - kS;
    kD *= 1.0 - metallic;

    float3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(n, v), 0.0) * max(dot(n, l), 0.0) + 0.0001;
    float3 specular = numerator / denominator;

    // add to outgoing radiance Lo
    float NdotL = max(dot(n, l), 0.0);
    float3 Lo = (kD * c / PI + specular) * radiance * NdotL;

    return Lo;
}


#endif
