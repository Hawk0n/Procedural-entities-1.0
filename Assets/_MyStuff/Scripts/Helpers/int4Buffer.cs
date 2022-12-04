using Unity.Entities;
using Unity.Mathematics;

namespace Helpers
{
    [InternalBufferCapacity(1)]
    public struct int4Buffer : IBufferElementData
    {
        public static implicit operator int4(int4Buffer e) { return e.value; }
        public static implicit operator int4Buffer(int4 e) { return new int4Buffer { value = e }; }

        public int4 value;
    }
}