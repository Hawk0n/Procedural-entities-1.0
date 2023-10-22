#include "Assets/_MyStuff/Scripts/Shaders/ComputeShaders/Includes/GradientNoise.compute"


float SoftNoise(float3 pointPos, int octaves, float noiseMultiplier, float noiseScale, float persistence,
                float lacunarity)
{
    float noiseSum = 0;
    float amplitude = noiseMultiplier;
    float frequency = noiseScale;

    for (int i = 0; i < octaves; i++)
    {
        noiseSum += snoise(pointPos * frequency) * amplitude;
        frequency *= persistence;
        amplitude *= lacunarity;
    }

    return noiseSum;
}


float RidgeNoise(float3 pointPos, int octaves, float noiseMultiplier, float noiseScale, float persistence,
                 float lacunarity)
{
    float noiseSum = 0;
    float amplitude = noiseMultiplier;
    float frequency = noiseScale;

    for (int i = 0; i < octaves; i++)
    {
        noiseSum += 1 - abs(snoise(pointPos * frequency) * amplitude);
        frequency *= persistence;
        amplitude *= lacunarity;
    }

    return noiseSum;
}
