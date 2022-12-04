using System;
using Unity.Entities;
using UnityEngine;

namespace Terrain
{
    [Serializable, InternalBufferCapacity(1)]
    public struct DTerrainNoiseLayer : IBufferElementData
    {
        public int isActive;
        public int noiseType;
        public int octaves;
        public float lacunarity;
        public float noiseScale;
        public float noiseMultiplier;
        public float persistence;
    }
}