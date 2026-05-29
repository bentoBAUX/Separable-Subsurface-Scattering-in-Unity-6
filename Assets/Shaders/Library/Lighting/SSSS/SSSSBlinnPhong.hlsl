#ifndef BLINN_PHONG_INCLUDED
#define BLINN_PHONG_INCLUDED

// Blinn-Phong lighting model
inline FragOutput BlinnPhong(Surf surfaceData, Light lightData)
{
    FragOutput o;
    float4 c = surfaceData.baseColor;
    float3 n = normalize(surfaceData.normalWS);
    float3 v = normalize(surfaceData.viewDirectionWS);
    half3 l = normalize(lightData.direction);
    half3 h = normalize(l + v);

    half NdotL = saturate(dot(n, l));

    half Id = _k.y * NdotL;
    half Is = _k.z * pow(saturate(dot(h, n)), _SpecularExponent);

    float shadow = lerp(0.005, 1, lightData.shadowAttenuation); // Avoid total black (looks shit)
    float atten = lightData.distanceAttenuation * shadow;

    half3 diffuse = Id * c * lightData.color * atten;
    half3 specular = Is * lightData.color * atten;
    o.diffuseBuffer = float4(diffuse, 0);
    o.specularBuffer = float4(specular, 0);
    return o;
}

#endif // BLINN_PHONG_INCLUDED