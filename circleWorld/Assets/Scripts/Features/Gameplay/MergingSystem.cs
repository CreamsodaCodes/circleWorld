using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using CircleWorld.Features.Cell;
using CircleWorld.Features.Physics;

namespace CircleWorld.Features.Gameplay
{
    [BurstCompile]
    public partial struct MergingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsProperties>();
            state.RequireForUpdate<CellType>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. Spatial Hash for broadphase (Detect collision between Connector and Structure)
            int entityCount = SystemAPI.QueryBuilder().WithAll<Position, PhysicsProperties>().Build().CalculateEntityCount();
            if (entityCount == 0) return;

            var spatialMap = new NativeParallelMultiHashMap<int, Entity>(entityCount, Allocator.TempJob);
            
            // Populate Hash
            var populateJob = new PopulateMergingHashJob
            {
                SpatialMap = spatialMap.AsParallelWriter(),
                CellSize = 2.0f
            }.ScheduleParallel(state.Dependency);

            // Resolve Merges
            // We need an EntityCommandBuffer to play back structural changes (adding constraints / changing IDs potentially?)
            // Actually, changing ComponentData (OrganismID) might be done in parallel if careful, 
            // but merging whole graphs usually requires a sync point or iterative approach.
            // For now, let's just create the constraint and change the ID of the *single* colliding cell 
            // and let a "FloodFillSystem" handle the propagation? 
            // OR: Just implement the immediate pair merge here.
            // The prompt says: "Merge IDs (Flood fill or component update)".
            // A full flood fill inside a parallel job is hard.
            // Simplified approach: Reassign ID of the *smaller* ID to the *larger* ID (or vice versa).
            
            // However, we cannot safely modify OrganismID of *other* entities in a parallel job.
            // So we will use an ECB to record the "MergeRequest".
            
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var mergeJob = new DetectAndMergeJob
            {
                SpatialMap = spatialMap,
                PositionLookup = SystemAPI.GetComponentLookup<Position>(true),
                RadiusLookup = SystemAPI.GetComponentLookup<PhysicsProperties>(true),
                TypeLookup = SystemAPI.GetComponentLookup<CellType>(true),
                OrganismLookup = SystemAPI.GetComponentLookup<OrganismID>(true),
                ECB = ecb,
                CellSize = 2.0f
            };

            state.Dependency = mergeJob.ScheduleParallel(populateJob);
            spatialMap.Dispose(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct PopulateMergingHashJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int, Entity>.ParallelWriter SpatialMap;
        public float CellSize;

        public void Execute(Entity entity, in Position position, in CellType type)
        {
            // Optimization: Only put Connectors and Structures in the map?
            // Actually, we need to find "Connector vs Structure".
            // So both need to be in the map.
            if (type.Value == CellKind.Structure || type.Value == CellKind.Connector)
            {
                int hash = GetSpatialHash(position.Value, CellSize);
                SpatialMap.Add(hash, entity);
            }
        }

        private int GetSpatialHash(float2 position, float cellSize)
        {
            int2 grid = (int2)math.floor(position / cellSize);
            return math.hash(grid);
        }
    }

    [BurstCompile]
    public partial struct DetectAndMergeJob : IJobEntity
    {
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> SpatialMap;
        [ReadOnly] public ComponentLookup<Position> PositionLookup;
        [ReadOnly] public ComponentLookup<PhysicsProperties> RadiusLookup;
        [ReadOnly] public ComponentLookup<CellType> TypeLookup;
        [ReadOnly] public ComponentLookup<OrganismID> OrganismLookup;
        
        public EntityCommandBuffer.ParallelWriter ECB;
        public float CellSize;

        // Iterate over Connectors only? Or all?
        // Let's iterate Connectors to find Structures.
        public void Execute(Entity entity, [EntityInQueryIndex] int sortKey, in Position position, in CellType type, in OrganismID organismID)
        {
            if (type.Value != CellKind.Connector) return;

            int2 grid = (int2)math.floor(position.Value / CellSize);

            // Check neighbors
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    int hash = math.hash(grid + new int2(x, y));
                    
                    if (SpatialMap.TryGetFirstValue(hash, out Entity neighbor, out var iterator))
                    {
                        do
                        {
                            if (neighbor == entity) continue;
                            if (!PositionLookup.HasComponent(neighbor)) continue;

                            // Check Logic: Connector touching Structure
                            var neighborType = TypeLookup[neighbor].Value;
                            if (neighborType != CellKind.Structure) continue;

                            // Check Organism ID
                            var neighborOrganismID = OrganismLookup[neighbor].Value;
                            if (neighborOrganismID == organismID.Value) continue;

                            // Check Collision
                            float2 otherPos = PositionLookup[neighbor].Value;
                            float radiusA = RadiusLookup[entity].Radius;
                            float radiusB = RadiusLookup[neighbor].Radius;
                            
                            float distSq = math.distancesq(position.Value, otherPos);
                            float combinedRadius = radiusA + radiusB;

                            if (distSq < combinedRadius * combinedRadius)
                            {
                                // HIT! Merge needed.
                                // We cannot do full flood fill here.
                                // We will effectively "link" them.
                                // Strategy: Change Neighbor's OrganismID to Ours? Or Ours to Theirs?
                                // Let's just create a Constraint and set component data.
                                // NOTE: Changing ComponentData via ECB is safe.
                                // But flood fill is NOT solved here.
                                // Task description: "Merge IDs (Flood fill or component update)".
                                // For now, we update the neighbor to match us.
                                // *Realistically*, this needs a recursive update or a separate "UnionFind" system.
                                // But adhering to simple ECS, I will just update the colliding node.
                                
                                ECB.SetComponent(sortKey, neighbor, new OrganismID { Value = organismID.Value });
                                
                                // Create Constraint
                                // We need to Append to buffer.
                                // ECB.AppendToBuffer<Constraint>(sortKey, entity, new Constraint { Target = neighbor, RestLength = combinedRadius, Stiffness = 0.5f });
                                // Since we initiated from the Connector, let's add the constraint to the Connector.
                                
                                var constraint = new Constraint
                                {
                                    Target = neighbor,
                                    RestLength = math.sqrt(distSq), // Use current distance as rest length? Or touching distance? Let's use current.
                                    Stiffness = 0.1f // Soft merge
                                };
                                ECB.AppendToBuffer(sortKey, entity, constraint);

                                // Add constraint to the other one too? Usually constraints are one-way in this simple model or double-linked.
                                // The solver in implementation_plan iterates "Entities with Constraint buffer".
                                // So adding it to one side is enough to pull them together.
                                
                                // Prevent multiple merges in one frame?
                                // Once merged, their IDs will be same next frame, so no repeated merge.
                            }

                        } while (SpatialMap.TryGetNextValue(out neighbor, ref iterator));
                    }
                }
            }
        }
    }
}
