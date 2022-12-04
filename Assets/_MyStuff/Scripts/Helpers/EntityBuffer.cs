using Unity.Entities;

namespace Helpers
{
    [InternalBufferCapacity(1)]
    public struct EntityBuffer : IBufferElementData
    {
        public static implicit operator Entity(EntityBuffer e) { return e.value; }
        public static implicit operator EntityBuffer(Entity e) { return new EntityBuffer { value = e }; }

        public Entity value;
    }
}