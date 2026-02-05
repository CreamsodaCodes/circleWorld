using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using CircleWorld.Features.Cell;

namespace CircleWorld.Features.Rendering
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct CellTransformSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Position>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new UpdateCellTransformJob().ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct UpdateCellTransformJob : IJobEntity
    {
        public void Execute(ref LocalTransform transform, in Position position)
        {
            // Sync logic: Position (float2) -> LocalTransform (float3)
            // We keep z as is, or set to 0. Let's preserve existing Z in case of layering, 
            // but usually for 2D we allow Z to be set by spawner. 
            // However, strictly setting it ensures consistency.
            transform.Position.x = position.Value.x;
            transform.Position.y = position.Value.y;
        }
    }
}
