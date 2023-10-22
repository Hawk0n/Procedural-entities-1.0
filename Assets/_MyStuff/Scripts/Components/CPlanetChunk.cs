using Helpers;
using Unity.Entities;
using Unity.Mathematics;

namespace Terrain
{
    public struct CPlanetChunk : IComponentData
    {
        public readonly CPlanet OwnedByCPlanet;
        public Entity MainPlanetEntity;
        public Entity ChunkEntity;
        public int3 Index;
        
        public CPlanetChunk(CPlanet ownedByCPlanet, Entity mainPlanetEntity, Entity chunkEntity, int3 index)
        {
            OwnedByCPlanet = ownedByCPlanet;
            MainPlanetEntity = mainPlanetEntity;
            ChunkEntity = chunkEntity;
            Index = index;
        }
        
        
        public readonly DynamicBuffer<DTerrainNoiseLayer> DTerrainNoiseBuffer(EntityManager entityManager) => 
            entityManager.GetBuffer<DTerrainNoiseLayer>(MainPlanetEntity);
        public readonly DynamicBuffer<EntityBuffer> OwnedChunkEntitiesBuffer(EntityManager entityManager) => 
            entityManager.GetBuffer<EntityBuffer>(MainPlanetEntity);
        public readonly DynamicBuffer<int4Buffer> OwnedChunkIndexesBuffer(EntityManager entityManager) => 
            entityManager.GetBuffer<int4Buffer>(MainPlanetEntity);
    }
}