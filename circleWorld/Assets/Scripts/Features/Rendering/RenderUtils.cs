using Unity.Entities;
using Unity.Mathematics;
using System;

namespace CircleWorld.Features.Rendering
{
    public struct EdgeKey : IEquatable<EdgeKey>
    {
        public Entity A;
        public Entity B;

        public EdgeKey(Entity a, Entity b)
        {
            // Sort to ensure A-B is same as B-A
            if (a.Index < b.Index)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public bool Equals(EdgeKey other)
        {
            return A.Equals(other.A) && B.Equals(other.B);
        }

        public override int GetHashCode()
        {
            // Combine hash codes
            return (A.Index * 397) ^ B.Index;
        }
    }

    public struct RenderConfig : IComponentData
    {
        public Entity LinePrefab;
    }
}
