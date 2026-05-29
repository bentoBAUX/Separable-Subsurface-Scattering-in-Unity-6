#ifndef LAMBERT_INCLUDED
#define LAMBERT_INCLUDED

inline float3 Lambert(float3 normalWS, Light light)
{
    float shadow = lerp(0.005, 1, light.shadowAttenuation); // Avoid total black (looks shit)
    float NdotL = dot(normalWS, normalize(light.direction));
    return saturate(NdotL) * light.color * light.distanceAttenuation * shadow;
}

#endif