using Unity.Entities;

namespace Terrain
{
    public struct TTerrainUpdateRequest : ISharedComponentData
    {
        public int Priority;

        public TTerrainUpdateRequest(int priority)
        {
            Priority = priority;
        }
    }
}