#ifndef LAMBERT_INCLUDED
#define LAMBERT_INCLUDED

inline FragOutput Lambert(float3 normalWS, Light light)
{
    FragOutput o;
    float NdotL = dot(normalWS, normalize(light.direction));

    float shadow = lerp(0.005, 1, light.shadowAttenuation); // Avoid total black (looks shit)
    o.diffuseBuffer = float4(saturate(NdotL) * light.color * light.distanceAttenuation * shadow, 1);
    o.specularBuffer = 0;
    return o;
}

#endif
