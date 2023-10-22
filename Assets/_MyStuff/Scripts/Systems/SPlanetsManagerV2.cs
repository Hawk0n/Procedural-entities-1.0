using System.Collections;
using System.Collections.Generic;
using Helpers;
using Terrain;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Transforms;
using UnityEngine;

namespace Terrain
{
    public partial struct SPlanetsManagerV2 : ISystem, ISystemStartStop
    {
        private EntityCommandBuffer _ecb;
        private Entity _terrainManagerEntity;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TTerrainManager>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }
        
        public void OnStartRunning(ref SystemState state)
        {
            _terrainManagerEntity = SystemAPI.GetSingletonEntity<TTerrainManager>();
            CreateNewPlanet(state.EntityManager, ref state);
        }

        public void OnStopRunning(ref SystemState state) { }


        public void OnUpdate(ref SystemState state)
        {
            _ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            

            foreach (var planet in SystemAPI.Query<RefRW<CPlanet>>().WithAll<TNewPlanet>())
            {
                // TODO: Find first chunk and send it to creation queue
                
            }
        }
        
        
        public void CreateNewPlanet(EntityManager entityManager, ref SystemState state)
        {
            if (_terrainManagerEntity == Entity.Null) return;

            var terrainManager = entityManager.GetComponentData<MDPlanetTerrain>(_terrainManagerEntity);
            
            Entity newPlanetEntity =  entityManager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(EntityBuffer), typeof(int4Buffer), 
                typeof(DTerrainNoiseLayer), typeof(DPlanetSettings), typeof(CPlanet), typeof(TNewPlanet));
            
            
            // Add noiseLayers and planetSettings
            var dPlanetSettings = terrainManager.dPlanetSettings;
            CPlanet newPlanet = new CPlanet(newPlanetEntity, dPlanetSettings);
            entityManager.SetComponentData(newPlanetEntity, dPlanetSettings);
            entityManager.SetComponentData(newPlanetEntity, newPlanet);
            var dTerrainNoiseBuffer = newPlanet.DTerrainNoiseBuffer(entityManager);
            dTerrainNoiseBuffer.AddRange(new NativeArray<DTerrainNoiseLayer>(terrainManager.dTerrainNoiseLayers, Allocator.Temp));
        }
    }
}
