using System;
using Unity.Entities;
using UnityEngine;

namespace Terrain
{
    [Serializable]
    public partial struct DPlanetSettings : IComponentData
    {
        public float chunkSize;
        public int chunkResolution;
        public float hardGroundLevel;
        public float groundLevelWeight;
        public float isoLevel;
    }
}