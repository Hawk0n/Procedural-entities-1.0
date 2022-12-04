using System.Collections;
using System.Collections.Generic;
using Helpers;
using Unity.Entities;
using UnityEngine;

namespace Terrain
{
    public struct CPlanet : IComponentData
    {
        public Entity mainPlanetEntity;
        public DPlanetSettings dPlanetSettings;
        
        // public DynamicBuffer<DTerrainNoiseLayer> dTerrainNoiseBuffer;
        // public DynamicBuffer<EntityBuffer> ownedChunksBuffer;
        
        public CPlanet(Entity mainPlanetEntity, DPlanetSettings dPlanetSettings)
        {
            this.mainPlanetEntity = mainPlanetEntity;
            this.dPlanetSettings = dPlanetSettings;
        }
        

        public DynamicBuffer<DTerrainNoiseLayer> dTerrainNoiseBuffer(EntityManager entityManager) => entityManager.GetBuffer<DTerrainNoiseLayer>(mainPlanetEntity);
        public DynamicBuffer<EntityBuffer> ownedChunksBuffer(EntityManager entityManager) => entityManager.GetBuffer<EntityBuffer>(mainPlanetEntity);
    }
}
