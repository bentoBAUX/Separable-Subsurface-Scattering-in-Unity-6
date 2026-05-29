#ifndef SSSSTRANSMISSION_INCLUDED
#define SSSSTRANSMISSION_INCLUDED

float _SSSS_GlobalEnabled;
float _SSSS_NearFarBalance;
float4 _SSSS_NearSigma;
float4 _SSSS_FarSigma;
float _SSSS_ScatterScale;
float _SSSS_SubsurfaceWeight;

float3 GetTransmissionDistance()
{
    float balance = saturate(_SSSS_NearFarBalance);

    float3 nearDistance = max(_SSSS_NearSigma * _SSSS_ScatterScale, 0.0001);
    float3 farDistance = max(_SSSS_FarSigma * _SSSS_ScatterScale, 0.0001);

    return lerp(farDistance, nearDistance, balance);
}

// Sigma-driven transmission.
// Larger sigma means that colour channel survives through more thickness.
// This falloff is based on Beer-Lambert's Law
float3 ArtistTransmissionProfile(float thickness)
{
    float3 distanceRGB = GetTransmissionDistance();

    float3 transmission = exp(-thickness / distanceRGB);

    return saturate(transmission);
}

float GetTransmissionScaleAmount()
{
    // Blender has a max of 10. Our _SSSS_ScatterScale is capped at 10 so this does not really matter but imma leave it here just in case I wanna remove the cap someday.
    const float REFERENCE_SCATTER_SCALE = 10.0;
    return saturate(_SSSS_ScatterScale / REFERENCE_SCATTER_SCALE);
}

// Code from: https://www.iryoku.com/translucency/?utm_source=openai
// Output of this should be added as an additional term in the master shader.
float3 CalculateTransmittance(Surf surfaceData, Light lightData)
{
    if (_SSSS_GlobalEnabled < 0.5)
        return 0.0;

    float3 N = normalize(surfaceData.normalWS);
    float3 L = normalize(lightData.direction);

    float backLight = saturate((dot(-N, L) + 0.3) / (1.0 + 0.3));

    float lightAtten = lightData.distanceAttenuation;

    float s = surfaceData.thickness;

    float scaleAmount = GetTransmissionScaleAmount();

    float3 transmittance = ArtistTransmissionProfile(s) * lightData.color * lightAtten * surfaceData.baseColor.rgb * backLight;

    return transmittance * scaleAmount * saturate(_SSSS_SubsurfaceWeight);
}
#endif
