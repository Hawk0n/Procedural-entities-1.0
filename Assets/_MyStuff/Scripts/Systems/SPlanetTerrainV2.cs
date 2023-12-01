using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Terrain
{
    public partial struct SPlanetTerrainV2 : ISystem
    {
        private EntityCommandBuffer _ecb;
        private int _currentRequestCount;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TTerrainManager>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            
            _currentRequestCount = 0;
        }
        
        
        
        public void OnUpdate(ref SystemState state)
        {
            _ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            
            
            
            
            var terrainManager = state.EntityManager.GetComponentData<MDPlanetTerrain>(SystemAPI.GetSingletonEntity<TTerrainManager>());


            foreach (var (cPlanet, planetEntity) in SystemAPI.Query<RefRW<CPlanet>>().WithAll<TNewPlanet>().WithEntityAccess())
            {
                FindFirstChunk(cPlanet.ValueRO, terrainManager);
                
                _ecb.RemoveComponent<TNewPlanet>(planetEntity);
            }
            
            
            
            if (_currentRequestCount == 0) 
            {
                CreateNewChunksTerrain(state.EntityManager, terrainManager, ref state);
            }
        }


        private void FindFirstChunk(CPlanet cPlanet, MDPlanetTerrain terrainManager)
        {
            // TODO: Actually make this work

            int3 index = new int3(0, 0, 0);
            Entity chunkEntity = _ecb.Instantiate(terrainManager.baseChunkPrefab);
            _ecb.AddComponent(chunkEntity, new CPlanetChunk(cPlanet, cPlanet.MainPlanetEntity, chunkEntity, index));
            _ecb.AddSharedComponent(chunkEntity, new TTerrainUpdateRequest(0));
        }


        private void CreateNewChunksTerrain(EntityManager entityManager, MDPlanetTerrain terrainManager, ref SystemState state)
        {
            // Determine if we need to prioritize chunks or just schedule all of them
            int requestCount = entityManager.CreateEntityQuery(typeof(TTerrainUpdateRequest)).CalculateEntityCount();
            if (requestCount == 0) return;
            
            
            if (requestCount < 5000) // Do not prioritize // TODO: Set threshold somehow
            {
                foreach (var data in SystemAPI.Query<RefRW<CPlanetChunk>>().WithEntityAccess())
                {
                    //await Task.Run(DispatchShaders(entityManager, terrainManager, data.Item1.ValueRO));
                    DispatchShaders(entityManager, terrainManager, data.Item1.ValueRO);
                    _currentRequestCount++;
                }
            }
            else // Prioritize
            {
                entityManager.GetAllUniqueSharedComponents(
                    out NativeList<TTerrainUpdateRequest> uniqueRequestPriorities, Allocator.Temp);

                int count = 0;
                for (var index = 0; index < uniqueRequestPriorities.Length; index++)
                {
                    TTerrainUpdateRequest t = uniqueRequestPriorities[index];
                    foreach (var chunk in SystemAPI.Query<RefRW<CPlanetChunk>>()
                                 .WithSharedComponentFilter(t)
                                 .WithEntityAccess())
                    {
                        count++;
                        if (count > 5000) break; // TODO: Set threshold somehow
                    }
                }
            }
        }


        private async void DispatchShaders(EntityManager entityManager, MDPlanetTerrain terrainManager, CPlanetChunk cPlanetChunk)
        {
            Entity planetEntity = cPlanetChunk.MainPlanetEntity;
            var noiseSettingsBuffer = cPlanetChunk.DTerrainNoiseBuffer(entityManager);
            
            
            Mesh mesh = entityManager.GetSharedComponentManaged<RenderMeshArray>(cPlanetChunk.ChunkEntity)
                .GetMesh(entityManager.GetComponentData<MaterialMeshInfo>(cPlanetChunk.ChunkEntity));
            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
            
            
            // Get the position of the chunk
            var planetSettingsRO = cPlanetChunk.OwnedByCPlanet.DPlanetSettings;
            float3 planetPosition = entityManager.GetComponentData<LocalToWorld>(planetEntity).Position;
            float3 chunkPosition = (float3) cPlanetChunk.Index * planetSettingsRO.chunkSize;
             
             
            // Allocate and set computeBuffers
            int resolution = planetSettingsRO.chunkResolution;
            int pointCount = resolution * resolution * resolution;
            ComputeBuffer pointsBuffer = new ComputeBuffer(pointCount, sizeof(float) * 4);
            // ComputeBuffer emptyCheckBuffer = new ComputeBuffer(pointCount, sizeof(int));
            ComputeBuffer noiseLayerBuffer = new ComputeBuffer(math.max(1, noiseSettingsBuffer.Length), sizeof(float) * 4 + sizeof(int) * 3);
            noiseLayerBuffer.SetData(noiseSettingsBuffer.AsNativeArray()); 
            
            // int maxTriangleCountPerChunk = (resolution - 1) * (resolution - 1) * (resolution - 1) * 5;
            // ComputeBuffer trianglesBuffer = new ComputeBuffer(maxTriangleCountPerChunk, sizeof(float) * 18, ComputeBufferType.Append);
            // trianglesBuffer.SetCounterValue(0);
            
            ComputeBuffer nonEmptyCellsBuffer = new ComputeBuffer((resolution - 1) * (resolution - 1) * (resolution - 1), sizeof(float) * 4 * 8, ComputeBufferType.Append);
            nonEmptyCellsBuffer.SetCounterValue(0);
            
            
            // Send data to GPU
            ComputeShader marchingCubesShaderV2 = terrainManager.marchShader;
            marchingCubesShaderV2.SetInt("noise_layer_count", noiseSettingsBuffer.Length);
            marchingCubesShaderV2.SetInt("vertex_per_axis", resolution);
            marchingCubesShaderV2.SetFloat("chunk_size", planetSettingsRO.chunkSize);
            marchingCubesShaderV2.SetFloat("hard_ground_level", planetSettingsRO.hardGroundLevel);
            marchingCubesShaderV2.SetFloat("ground_level_weight", planetSettingsRO.groundLevelWeight);
            marchingCubesShaderV2.SetFloat("isoLevel", planetSettingsRO.isoLevel);
            marchingCubesShaderV2.SetVector("chunk_position", new float4(chunkPosition, 1)); // 4th number is the chunk's Lod
            marchingCubesShaderV2.SetVector("planet_pos", new float4(planetPosition, 0));
            marchingCubesShaderV2.SetBuffer(0, "point_densities", pointsBuffer);
            // densityShader.SetBuffer(0, "empty", emptyCheckBuffer);
            marchingCubesShaderV2.SetBuffer(0, "noise_layers", noiseLayerBuffer);
            // marchingCubesShaderV2.SetBuffer(0, "triangles_append", trianglesBuffer);
            marchingCubesShaderV2.SetBuffer(0, "non_empty_cells", nonEmptyCellsBuffer);
            

            // Dispatch computeShader
            int numThreadsPerAxis = Mathf.CeilToInt(resolution / 8f);
            marchingCubesShaderV2.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);
            noiseLayerBuffer.Release();
            
            
            // Delay until shader has completed
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(nonEmptyCellsBuffer);
            while (!request.done)
            {
                await Task.Delay(1); // BUG: Might be a bug where the request completes while the system is awaiting and the request gets deallocated.
            }
            
            // Get data
            var cells = request.GetData<Cell>();
            int count = cells.Length;
            
            
            // ------------------ Second shader to assign mesh data
            
            
            // Set data
            mesh.SetVertexBufferParams(count * 3, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32));
            mesh.SetIndexBufferParams(count * 3, IndexFormat.UInt32);
            GraphicsBuffer vertexBuffer = mesh.GetVertexBuffer(0);
            GraphicsBuffer indexBuffer = mesh.GetIndexBuffer();
            marchingCubesShaderV2.SetBuffer(1, "non_empty_cells_r", nonEmptyCellsBuffer);
            marchingCubesShaderV2.SetBuffer(1, "vertex_buffer", vertexBuffer);
            marchingCubesShaderV2.SetBuffer(1, "index_buffer", indexBuffer);
            marchingCubesShaderV2.SetInt("numCells", count);
            
            // Dispatch
            // marchingCubesShaderV2.Dispatch(1, triangleCount, 1, 1);

            // var vertexArray = new Vector3[triangleCount * 3];
            // var indexArray = new int[triangleCount * 3];
            // vertexBuffer.GetData(vertexArray);
            // indexBuffer.GetData(indexArray);
            //
            // mesh.SetVertices(vertexArray);
            // mesh.SetIndices(indexArray, MeshTopology.Triangles, 0);
            // mesh.RecalculateBounds();
            // mesh.RecalculateNormals();
            // mesh.RecalculateTangents();
            
            // var points = request.GetData<float4>().AsReadOnly();
            // foreach (var vertex in vertexArray)
            // {
            //     DrawPoint(vertex, 0.2f, Color.blue, 30);
            // }
            // foreach (var point in points)
            // {
            //     DrawPoint(point, 0.1f, Color.Lerp(Color.green, Color.red, (point.w+40f)/80f), 30);
            // }
            
            _currentRequestCount--;
        }
        
        
        
        public static void DrawPoint(Vector4 pos, float scale, Color color, float duration)
        {
            var sX = pos + new Vector4(+scale, 0, 0);
            var eX = pos + new Vector4(-scale, 0, 0);
            var sY = pos + new Vector4(0, +scale, 0);
            var eY = pos + new Vector4(0, -scale, 0);
            var sZ = pos + new Vector4(0, 0, +scale);
            var eZ = pos + new Vector4(0, 0, -scale);
            Debug.DrawLine(sX , eX , color, duration);
            Debug.DrawLine(sY , eY , color, duration);
            Debug.DrawLine(sZ , eZ , color, duration);
        }
    }
    
    struct Cell
    {
        public float4 cellPoints0;
        public float4 cellPoints1;
        public float4 cellPoints2;
        public float4 cellPoints3;
        public float4 cellPoints4;
        public float4 cellPoints5;
        public float4 cellPoints6;
        public float4 cellPoints7;
    };
}