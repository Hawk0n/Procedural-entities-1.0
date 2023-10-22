using System.Collections;
using System.Collections.Generic;
using Helpers;
using Unity.Entities;
using UnityEngine;

namespace Terrain
{
    public struct CPlanet : IComponentData
    {
        public Entity MainPlanetEntity;
        public DPlanetSettings DPlanetSettings;
        
        // public DynamicBuffer<DTerrainNoiseLayer> dTerrainNoiseBuffer;
        // public DynamicBuffer<EntityBuffer> ownedChunksBuffer;
        
        public CPlanet(Entity mainPlanetEntity, DPlanetSettings dPlanetSettings)
        {
            MainPlanetEntity = mainPlanetEntity;
            DPlanetSettings = dPlanetSettings;
        }
        

        public readonly DynamicBuffer<DTerrainNoiseLayer> DTerrainNoiseBuffer(EntityManager entityManager) => 
            entityManager.GetBuffer<DTerrainNoiseLayer>(MainPlanetEntity);
        public readonly DynamicBuffer<EntityBuffer> OwnedChunkEntitiesBuffer(EntityManager entityManager) => 
            entityManager.GetBuffer<EntityBuffer>(MainPlanetEntity);
        public readonly DynamicBuffer<int4Buffer> OwnedChunkIndexesBuffer(EntityManager entityManager) => 
            entityManager.GetBuffer<int4Buffer>(MainPlanetEntity);

    }
}
