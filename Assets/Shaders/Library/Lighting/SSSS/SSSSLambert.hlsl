#ifndef LAMBERT_INCLUDED
#define LAMBERT_INCLUDED

inline FragOutput Lambert(float3 normalWS, Light light)
{
    FragOutput o;
    float NdotL = dot(normalWS, normalize(light.direction));

    o.diffuseBuffer = float4(saturate(NdotL) * light.color * light.distanceAttenuation * light.shadowAttenuation, 1);
    o.specularBuffer = 0;
    return o;
}

#endif
