using System;
using Unity.Entities;
using UnityEngine;

namespace Terrain
{
    [Serializable]
    public struct DPlanetSettings : IComponentData
    {
        public float chunkSize;
        public int chunkResolution;
        public float hardGroundLevel;
        public float groundLevelWeight;
        public float isoLevel;
    }
}