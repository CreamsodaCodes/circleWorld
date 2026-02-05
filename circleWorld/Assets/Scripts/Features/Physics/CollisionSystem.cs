using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using CircleWorld.Features.Cell;

namespace CircleWorld.Features.Physics
{
    [BurstCompile]
    public partial struct CollisionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsProperties>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. Create HashMap for this frame
            // Using WorldUpdateAllocator (reset every frame) or TempJob.
            // Estimate count: query.CalculateEntityCount().
            int entityCount = SystemAPI.QueryBuilder().WithAll<Position, PhysicsProperties>().Build().CalculateEntityCount();
            var spatialMap = new NativeParallelMultiHashMap<int, Entity>(entityCount, Allocator.TempJob);

            // 2. Schedule Populate Job
            var populateHandle = new PopulateSpatialHashJob
            {
                SpatialMap = spatialMap.AsParallelWriter(),
                CellSize = 2.0f // Cell Size > Max Diameter of entity
            }.ScheduleParallel(state.Dependency);

            // 3. Schedule Resolve Job
            // We need to pass the Map as [ReadOnly]
            var resolveJob = new ResolveCollisionsJob
            {
                SpatialMap = spatialMap, // ReadOnly
                PositionLookup = SystemAPI.GetComponentLookup<Position>(true), // ReadOnly neighbor pos
                RadiusLookup = SystemAPI.GetComponentLookup<PhysicsProperties>(true), // ReadOnly neighbor radius
                CellSize = 2.0f
            };
            
            // Resolve Job depends on Populate
            state.Dependency = resolveJob.ScheduleParallel(populateHandle);

            // 4. Dispose Map after jobs complete (using DeallocateOnJobCompletion or just Dispose if we wait)
            // NativeParallelMultiHashMap needs explicit disposal.
            // Since we use the map in a job, we can't dispose immediately on main thread unless we wait.
            // But we want to chain.
            spatialMap.Dispose(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct PopulateSpatialHashJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int, Entity>.ParallelWriter SpatialMap;
        public float CellSize;

        public void Execute(Entity entity, in Position position)
        {
            int hash = GetSpatialHash(position.Value, CellSize);
            SpatialMap.Add(hash, entity);
        }

        private int GetSpatialHash(float2 position, float cellSize)
        {
            int2 grid = (int2)math.floor(position / cellSize);
            return math.hash(grid);
        }
    }

    [BurstCompile]
    public partial struct ResolveCollisionsJob : IJobEntity
    {
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> SpatialMap;
        [ReadOnly] public ComponentLookup<Position> PositionLookup;
        [ReadOnly] public ComponentLookup<PhysicsProperties> RadiusLookup;
        public float CellSize;

        public void Execute(Entity entity, ref Position position, in PhysicsProperties properties)
        {
            int CenterHash = GetSpatialHash(position.Value, CellSize);

            // Check 9 neighbors (3x3)
            // (dx, dy) from -1 to 1
            float2 totalImpulse = float2.zero;

            // To iterate neighbors, we need to know the grid coord, not just hash.
            // Let's re-calculate grid coord.
             int2 grid = (int2)math.floor(position.Value / CellSize);

             for (int x = -1; x <= 1; x++)
             {
                 for (int y = -1; y <= 1; y++)
                 {
                     int2 neighborGrid = grid + new int2(x, y);
                     int neighborHash = math.hash(neighborGrid);

                     if (SpatialMap.TryGetFirstValue(neighborHash, out Entity neighbor, out var iterator))
                     {
                         do
                         {
                             // Avoid Self
                             if (neighbor == entity) continue;

                             // Check collision
                             if (!PositionLookup.HasComponent(neighbor)) continue;
                             
                             float2 otherPos = PositionLookup[neighbor].Value;
                             float otherRadius = RadiusLookup[neighbor].Radius;

                             float2 delta = position.Value - otherPos;
                             float distSq = math.lengthsq(delta);
                             float minDist = properties.Radius + otherRadius;
                             
                             if (distSq < minDist * minDist && distSq > 0.00001f)
                             {
                                 float dist = math.sqrt(distSq);
                                 float overlap = minDist - dist;
                                 
                                 // Simple Separation Impulse
                                 // Push ME away from THEM.
                                 // Each side does this.
                                 float2 direction = delta / dist;
                                 
                                 // Factor 0.5 because other will also push me? 
                                 // Yes, symmetric.
                                 totalImpulse += direction * overlap * 0.5f; 
                             }

                         } while (SpatialMap.TryGetNextValue(out neighbor, ref iterator));
                     }
                 }
             }
             
             position.Value += totalImpulse;
        }

        private int GetSpatialHash(float2 position, float cellSize)
        {
            int2 grid = (int2)math.floor(position / cellSize);
            return math.hash(grid);
        }
    }
}
