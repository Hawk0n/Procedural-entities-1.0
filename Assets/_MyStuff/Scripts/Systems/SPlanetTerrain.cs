using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Helpers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Terrain
{
    public partial class SPlanetTerrain : SystemBase
    {
        public MDPlanetTerrain mdPlanetTerrain;
        private SGameManager _sGameManager;
        private EndSimulationEntityCommandBufferSystem _ecbSystem;
        private EntityCommandBuffer _ecb;
        public Dictionary<Entity, AsyncGPUReadbackRequest> chunkEntityDirtyTriangleArrayMap;


        protected override void OnStartRunning()
        {
            mdPlanetTerrain = EntityManager.GetComponentData<MDPlanetTerrain>(SystemAPI.GetSingletonEntity<TTerrainManager>());
            _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            _sGameManager = World.GetExistingSystemManaged<SGameManager>();
            chunkEntityDirtyTriangleArrayMap = new Dictionary<Entity, AsyncGPUReadbackRequest>();
        }


        protected override void OnUpdate()
        {
            // Create a new CommandBuffer each frame
            _ecb = _ecbSystem.CreateCommandBuffer();
        }
        
        
        
        public (bool, int3) FindFirstPlanetChunk(CPlanet cPlanet, Entity planetEntity)
        {
            // Find the direction from planet center to player position
            float3 playerPos = EntityManager.GetComponentData<LocalTransform>(_sGameManager.playerEntity).Position;
            float3 planetPos = EntityManager.GetComponentData<LocalTransform>(cPlanet.mainPlanetEntity).Position;
            float3 direction = math.normalize(playerPos - planetPos);
            
            
            // Find the first useful chunk if the chunk exist within x chunks from planet center
            int checkedIndexes = 1;
            int3 currentIndex = new int3(0, 0, 0);
            float planetChunkSize = cPlanet.dPlanetSettings.chunkSize;
            bool foundFirstChunk = ComputeDensityPoints(cPlanet, currentIndex, true).Item1;
            while (!foundFirstChunk && checkedIndexes < 100)
            {
                checkedIndexes++;
                currentIndex = GetIndexFromCoord(direction * planetChunkSize * checkedIndexes, cPlanet);
                foundFirstChunk = ComputeDensityPoints(cPlanet, currentIndex, true).Item1;
            }


            return (foundFirstChunk, currentIndex);
        }
        
        
        private static int3 GetIndexFromCoord(float3 position, CPlanet cPlanet) 
            => (int3) math.floor(position / cPlanet.dPlanetSettings.chunkSize);
        
        
        public (bool, ComputeBuffer) ComputeDensityPoints(CPlanet cPlanet, int3 chunkIndex, bool probing)
        {
            Entity planetEntity = cPlanet.mainPlanetEntity;
            var noiseSettingsBuffer = EntityManager.GetBuffer<DTerrainNoiseLayer>(planetEntity);

            var planetSettingsRO = cPlanet.dPlanetSettings;
            
            // Get the position of the chunk
            float3 planetPosition = EntityManager.GetComponentData<LocalToWorld>(planetEntity).Position;
            float3 chunkPosition = (float3) chunkIndex * planetSettingsRO.chunkSize;
            
            
            // Allocate and set computeBuffers
            int resolution = planetSettingsRO.chunkResolution;
            int pointCount = resolution * resolution * resolution;
            ComputeBuffer pointsBuffer = new ComputeBuffer(pointCount, sizeof(float) * 4);
            ComputeBuffer emptyCheckBuffer = new ComputeBuffer(pointCount, sizeof(int));
            ComputeBuffer noiseLayerBuffer = new ComputeBuffer(math.max(1, cPlanet.dTerrainNoiseBuffer(EntityManager).Length), sizeof(float) * 4 + sizeof(int) * 3);
            noiseLayerBuffer.SetData(EntityManager.GetBuffer<DTerrainNoiseLayer>(cPlanet.mainPlanetEntity).AsNativeArray());
            
            
            // Send data to GPU
            ComputeShader densityShader = mdPlanetTerrain.densityShader;
            densityShader.SetInt("noiseLayerCount", cPlanet.dTerrainNoiseBuffer(EntityManager).Length);
            densityShader.SetInt("vertexPerAxis", resolution);
            densityShader.SetFloat("chunkSize", planetSettingsRO.chunkSize);
            densityShader.SetFloat("hardGroundLevel", planetSettingsRO.hardGroundLevel);
            densityShader.SetFloat("groundLevelWeight", planetSettingsRO.groundLevelWeight);
            densityShader.SetVector("chunkPos", new float4(chunkPosition, 1)); // 4th number is the chunk's Lod
            densityShader.SetVector("planetPos", new float4(planetPosition, 0));
            densityShader.SetBuffer(0, "points", pointsBuffer);
            densityShader.SetBuffer(0, "empty", emptyCheckBuffer);
            densityShader.SetBuffer(0, "noiseLayers", noiseLayerBuffer);
            
            
            // Dispatch computeShader
            int numThreadsPerAxis = Mathf.CeilToInt(resolution / 8f);
            densityShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);
            noiseLayerBuffer.Release();
            
            
            // Determine if the chunk is empty or not. true == not empty
            if (probing) 
            {
                AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(emptyCheckBuffer);
                request.WaitForCompletion();
                NativeArray<int>.ReadOnly emptyCheckArray = request.GetData<int>().AsReadOnly();
                int total = emptyCheckArray.Sum();
                emptyCheckBuffer.Release();
                
                float4[] array = new float4[pointsBuffer.count];
                pointsBuffer.GetData(array);

                return (math.abs(total) != pointCount, pointsBuffer);
            }

            return (true, pointsBuffer);
        }

        
        public void ComputeMarch(ComputeBuffer densityPointsBuffer, CPlanet cPlanet, CPlanetChunk cPlanetChunk)
        {
            int resolution = cPlanet.dPlanetSettings.chunkResolution;

            
            int maxTriangleCountPerChunk = (resolution - 1) * (resolution - 1) * (resolution - 1) * 5;
            ComputeBuffer dirtyTrianglesBuffer = new ComputeBuffer(maxTriangleCountPerChunk, sizeof(float) * 9 + sizeof(int), ComputeBufferType.Append);
            dirtyTrianglesBuffer.SetCounterValue(0);
            ComputeBuffer isDoneBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

            
            ComputeShader marchShader = mdPlanetTerrain.marchShader;
            marchShader.SetBuffer(0, "is_done_buffer", isDoneBuffer);
            marchShader.SetBuffer(0, "triangles", dirtyTrianglesBuffer);
            marchShader.SetBuffer(0, "points", densityPointsBuffer);
            marchShader.SetInt("vertexPerAxis", resolution);
            marchShader.SetFloat("isoLevel", cPlanet.dPlanetSettings.isoLevel);

            int numThreadsPerAxis = Mathf.CeilToInt(resolution - 1 / 8f);
            marchShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);
            densityPointsBuffer.Release();
            
            
            // Get triangles asynchronously
            AsyncGPUReadback.Request(isDoneBuffer, _ =>
            {
                // Determine if the chunk is empty and return if so
                var chunkTriangleCount = NumberOfTrianglesInChunk(dirtyTrianglesBuffer);
                if (chunkTriangleCount == 0) return;
                    
                // Get the relevant triangles from the buffer
                AsyncGPUReadback.Request(dirtyTrianglesBuffer, chunkTriangleCount, 0, trianglesRequest =>
                {
                    chunkEntityDirtyTriangleArrayMap.Add(cPlanetChunk.chunkEntity, trianglesRequest);
                    EntityManager.AddComponent<THasDirtyTriangles>(cPlanetChunk.chunkEntity);
                });
            }); 
        }
        
        
        private int NumberOfTrianglesInChunk(ComputeBuffer dirtyTrianglesBuffer)
        {
            ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);


            // Get number of tris
            ComputeBuffer.CopyCount(dirtyTrianglesBuffer, countBuffer, 0);
            int[] counter = {0};
            countBuffer.GetData(counter);
            countBuffer.Dispose();
            
            
            return (sizeof(float) * 9 + sizeof(int)) * counter[0]; // Determine how many vertices exist
        }
        

        public async Task<(NativeArray<int>, NativeArray<float3>)> CleanDirtyTriangleArrayAsync(NativeArray<Triangle> dirtyTrianglesArray)
        {
            // Create arrays
            var newSides = new NativeArray<int>(1, Allocator.TempJob);
            var vertices = new NativeList<float3>(100, Allocator.TempJob);
            var indexes = new NativeList<int>(100, Allocator.TempJob);
            var verticesMap = new NativeHashMap<float3, int>(100, Allocator.TempJob);
            var triangles3D = new NativeHashSet<int3>(100, Allocator.TempJob);
            
            
            // Remove duplicate data and reorganize arrays
            SimplifyTrisJob simplifyTrisJob = new SimplifyTrisJob
            {
                dirtyTrianglesArray = dirtyTrianglesArray,
                newSides = newSides,
                vertices = vertices,
                triangles = indexes,
                verticesMap = verticesMap,
                triangles3D = triangles3D
            };
            
            JobHandle trisHandle = simplifyTrisJob.Schedule(dirtyTrianglesArray.Length, Dependency);
            // while (!trisHandle.IsCompleted) await Task.Yield();
            trisHandle.Complete();

            dirtyTrianglesArray.Dispose();
            verticesMap.Dispose();
            triangles3D.Dispose();

            return (indexes.AsArray(), vertices);
        }


        public Mesh CreateChunkMesh(NativeArray<int> indexes, NativeArray<float3> vertices)
        {
            // Create mesh
            Mesh mesh = new Mesh();
        
            // Set mesh parameters
            mesh.SetVertexBufferParams(vertices.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1));
            mesh.SetIndexBufferParams(indexes.Length, IndexFormat.UInt32);
        
            // Set mesh data
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length); 
            mesh.SetIndexBufferData(indexes, 0, 0, indexes.Length, MeshUpdateFlags.DontValidateIndices);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexes.Length));
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
        

        public void AddMeshToChunk(Entity chunkEntity, Mesh meshToAdd)
        {
            // var newChunkEntity = mdPlanetTerrain.baseChunkPrefab;
            
            var renderMeshArray = EntityManager.GetSharedComponentManaged<RenderMeshArray>(chunkEntity);
            renderMeshArray.Meshes[0] = meshToAdd;
            EntityManager.SetSharedComponentManaged(chunkEntity, renderMeshArray);
            MaterialMeshInfo materialMeshInfo = EntityManager.GetComponentData<MaterialMeshInfo>(chunkEntity);
            materialMeshInfo.Mesh = 3;
            EntityManager.SetComponentData(chunkEntity, materialMeshInfo);
        }


        public void CreateAndAddChunkToPlanet(int3 chunkIndex, CPlanet cPlanet)
        {
            Entity newChunkEntity = EntityManager.Instantiate(mdPlanetTerrain.baseChunkPrefab); //EntityManager.CreateEntity(typeof(LocalToWorld), typeof(CPlanetChunk), typeof(TTerrainUpdateRequest));
            CPlanetChunk newCPlanetChunk = new CPlanetChunk(cPlanet, cPlanet.mainPlanetEntity, newChunkEntity, chunkIndex);
            _ecb.AddComponent(newChunkEntity, newCPlanetChunk);
            _ecb.AddComponent<TTerrainUpdateRequest>(newChunkEntity);
            cPlanet.ownedChunksBuffer(EntityManager).Add(newChunkEntity);
        }
        
        
        [BurstCompile]
        private struct SimplifyTrisJob : IJobFor
        {
            [ReadOnly] public NativeArray<Triangle> dirtyTrianglesArray;
            [WriteOnly] public NativeList<int> triangles;
        
            public NativeList<float3> vertices;
            public NativeArray<int> newSides;
            public NativeHashMap<float3, int> verticesMap;
            public NativeHashSet<int3> triangles3D;
        
            public void Execute(int i)
            {
                // Remove extra triangles and reorganize arrays
                int3 triangle = 0;
                for (int j = 0; j < 3; j++) 
                {
                    vertices.Add(dirtyTrianglesArray[i][j]);
                    triangles.Add(vertices.Length - 1);
                    // newSides[0] |= tris[i].side;
                
                    // if (verticesMap.TryGetValue(tris[i][j], out int k)) 
                    //     triangle[j] = k;
                    // else {
                    //     vertices.Add(tris[i][j]);
                    //     verticesMap.Add(tris[i][j], vertices.Length - 1);
                    //     triangle[j] = vertices.Length - 1;
                    //
                    //     newSides[0] |= tris[i].side;
                    // }
                }

                // if (!triangles3D.Contains(triangle) && IsValid(triangle)) {
                //     triangles3D.Add(triangle);
                //     triangles.Add(triangle.x);
                //     triangles.Add(triangle.y);
                //     triangles.Add(triangle.z);
                // }
                // bool IsValid(int3 tri) => tri.x != tri.y && tri.x != tri.z && tri.y != tri.z;
            }
        }
        
        
        public struct Triangle
        {
#pragma warning disable 649 // disable unassigned variable warning
            private float3 _va;
            private float3 _vb;
            private float3 _vc;
            public int side;

            public float3 this[int i] {
                get {
                    return i switch {
                        0 => _va,
                        1 => _vb,
                        _ => _vc
                    };
                }
            }
        }
    }
}