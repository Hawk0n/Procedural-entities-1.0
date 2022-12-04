using Unity.Entities;
using Unity.Mathematics;

namespace Terrain
{
    public struct CPlanetChunk : IComponentData
    {
        public readonly CPlanet ownedByCPlanet;
        public Entity mainPlanetEntity;
        public Entity chunkEntity;
        public int3 index;
        
        public CPlanetChunk(CPlanet ownedByCPlanet, Entity mainPlanetEntity, Entity chunkEntity, int3 index)
        {
            this.ownedByCPlanet = ownedByCPlanet;
            this.mainPlanetEntity = mainPlanetEntity;
            this.chunkEntity = chunkEntity;
            this.index = index;
        }
    }
}