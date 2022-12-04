using Helpers;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Terrain
{
    public partial class SPlanetsManager : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _ecbSystem;
        private EntityCommandBuffer _ecb;

        private SGameManager _sGameManager;
        private SPlanetTerrain _sPlanetTerrain;
        public NativeList<CPlanet> createdPlanets;
        
        
        protected override void OnStartRunning()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            _sPlanetTerrain = World.GetExistingSystemManaged<SPlanetTerrain>();
            _sGameManager = World.GetExistingSystemManaged<SGameManager>();
            createdPlanets = new NativeList<CPlanet>(1, Allocator.Persistent);
            
            CreateNewPlanet();
        }
        
        
        public void CreateNewPlanet()
        {
            Entity newPlanetEntity = EntityManager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(EntityBuffer), 
                typeof(DTerrainNoiseLayer), typeof(DPlanetSettings), typeof(TNewPlanet));
            DynamicBuffer<EntityBuffer> ownedChunksBuffer = EntityManager.GetBuffer<EntityBuffer>(newPlanetEntity);
            
            
            // Add noiseLayers and planetSettings
            var dTerrainNoiseBuffer = EntityManager.GetBuffer<DTerrainNoiseLayer>(newPlanetEntity);
            dTerrainNoiseBuffer.AddRange(new NativeArray<DTerrainNoiseLayer>(_sPlanetTerrain.mdPlanetTerrain.dTerrainNoiseLayers, Allocator.Temp));
            var dPlanetSettings = _sPlanetTerrain.mdPlanetTerrain.dPlanetSettings;
            EntityManager.AddComponentData(newPlanetEntity, dPlanetSettings);


            CPlanet newPlanet = new CPlanet(newPlanetEntity, dPlanetSettings);
            EntityManager.AddComponentData(newPlanetEntity, newPlanet);
            
            
            createdPlanets.Add(newPlanet);
        }
        
        
        private void UpdateAllNewPlanets()
        {
            foreach (var (cPlanet, tNewPlanet, planetEntity) in 
                     SystemAPI.Query<RefRW<CPlanet>, RefRO<TNewPlanet>>().WithEntityAccess())
            {
                // Find the first chunk of this planet to be created.
                // If first chunk is found; Create the chunk with the found chunkIndex
                (bool, int3) firstChunk = _sPlanetTerrain.FindFirstPlanetChunk(cPlanet.ValueRO, planetEntity);
                if (firstChunk.Item1) _sPlanetTerrain.CreateAndAddChunkToPlanet(firstChunk.Item2, cPlanet.ValueRW);


                // Remove tag so that we don't try to create the same planet multiple times
                _ecb.RemoveComponent<TNewPlanet>(planetEntity);
            }
        }


        private void ComputeDensityAndMarchChunk()
        {
            foreach (var (cPlanetChunk, tTerrainUpdateRequest, chunkEntity) in 
                     SystemAPI.Query<RefRW<CPlanetChunk>, EnabledRefRO<TTerrainUpdateRequest>>().WithEntityAccess())
            {
                CPlanet cPlanet = cPlanetChunk.ValueRO.ownedByCPlanet;
                var density = _sPlanetTerrain.ComputeDensityPoints(cPlanet, cPlanetChunk.ValueRO.index, false);
                _sPlanetTerrain.ComputeMarch(density.Item2, cPlanet, cPlanetChunk.ValueRO);
                
                _ecb.RemoveComponent<TTerrainUpdateRequest>(chunkEntity);
            }
        }


        private void CleanDirtyTrianglesAndCreateMesh()
        {
            Entities.WithAll<THasDirtyTriangles>().ForEach((Entity chunkEntity, ref CPlanetChunk cPlanetChunk) =>
            {
                NativeArray<SPlanetTerrain.Triangle> dirtyTrianglesArray = _sPlanetTerrain.chunkEntityDirtyTriangleArrayMap[chunkEntity].GetData<SPlanetTerrain.Triangle>();
                (NativeArray<int>, NativeArray<float3>) cleanVerticesAndTriangles = _sPlanetTerrain.CleanDirtyTriangleArrayAsync(dirtyTrianglesArray).Result;
                Mesh chunkMesh = _sPlanetTerrain.CreateChunkMesh(cleanVerticesAndTriangles.Item1, cleanVerticesAndTriangles.Item2);
                _sPlanetTerrain.AddMeshToChunk(chunkEntity, chunkMesh);
                
                
                _ecb.RemoveComponent<THasDirtyTriangles>(chunkEntity);
            }).WithStructuralChanges().WithoutBurst().Run();
        }


        protected override void OnUpdate()
        {
            _ecb = _ecbSystem.CreateCommandBuffer();
            
            // Find the first chunk of new planets
            UpdateAllNewPlanets();
            ComputeDensityAndMarchChunk();
            CleanDirtyTrianglesAndCreateMesh();
        }
    }
}