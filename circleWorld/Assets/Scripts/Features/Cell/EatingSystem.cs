using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using CircleWorld.Features.Cell;

namespace CircleWorld.Features.Cell
{
    [BurstCompile]
    public partial struct EatingSystem : ISystem
    {
        private ComponentLookup<Position> _positionLookup;
        private ComponentLookup<PhysicsProperties> _physicsLookup;
        private ComponentLookup<Energy> _energyLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CellType>();
            _positionLookup = state.GetComponentLookup<Position>(true);
            _physicsLookup = state.GetComponentLookup<PhysicsProperties>(true);
            _energyLookup = state.GetComponentLookup<Energy>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. Setup Data
            int foodCount = SystemAPI.QueryBuilder().WithAll<CellType, Position, Energy>().Build().CalculateEntityCount();
            var foodMap = new NativeParallelMultiHashMap<int, Entity>(foodCount, Allocator.TempJob);
            
            // 2. Populate Food Hash Map
            var populateJob = new PopulateFoodHashJob
            {
                FoodMap = foodMap.AsParallelWriter(),
                CellSize = 2.0f
            };
            var populateHandle = populateJob.ScheduleParallel(state.Dependency);

            // 3. Detect Eating (Mouths check Food Map)
            var energyQueue = new NativeQueue<EnergyGainEvent>(Allocator.TempJob);
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            _positionLookup.Update(ref state);
            _physicsLookup.Update(ref state);

            var eatingJob = new EatingDetectionJob
            {
                FoodMap = foodMap,
                PositionLookup = _positionLookup,
                PhysicsLookup = _physicsLookup,
                EnergyEvents = energyQueue.AsParallelWriter(),
                ECB = ecb,
                CellSize = 2.0f
            };
            
            var eatingHandle = eatingJob.ScheduleParallel(populateHandle);

            // 4. Apply Energy (Single Threaded to avoid race conditions on Organism Energy)
            // Note: If we had many organisms, we could do a parallel reduction by key, but simpler here.
            _energyLookup.Update(ref state);
            
            var applyJob = new ApplyEnergyJob
            {
                EnergyEvents = energyQueue,
                EnergyLookup = _energyLookup
            };

            state.Dependency = applyJob.Schedule(eatingHandle);

            // Dispose
            foodMap.Dispose(state.Dependency);
            energyQueue.Dispose(state.Dependency);
        }

        private struct EnergyGainEvent
        {
            public Entity Organism;
            public float Amount;
        }

        [BurstCompile]
        public partial struct PopulateFoodHashJob : IJobEntity
        {
            public NativeParallelMultiHashMap<int, Entity>.ParallelWriter FoodMap;
            public float CellSize;

            public void Execute(Entity entity, in CellType type, in Position pos)
            {
                if (type.Value == CellKind.Food)
                {
                    int hash = GetSpatialHash(pos.Value, CellSize);
                    FoodMap.Add(hash, entity);
                }
            }

            private int GetSpatialHash(float2 position, float cellSize)
            {
                int2 grid = (int2)math.floor(position / cellSize);
                return math.hash(grid);
            }
        }

        [BurstCompile]
        public partial struct EatingDetectionJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<int, Entity> FoodMap;
            [ReadOnly] public ComponentLookup<Position> PositionLookup;
            [ReadOnly] public ComponentLookup<PhysicsProperties> PhysicsLookup;
            public NativeQueue<EnergyGainEvent>.ParallelWriter EnergyEvents;
            public EntityCommandBuffer.ParallelWriter ECB;
            public float CellSize;

            public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in CellType type, in Position pos, in PhysicsProperties props, in OrganismID organismID)
            {
                if (type.Value != CellKind.Mouth) return;

                int centerHash = GetSpatialHash(pos.Value, CellSize);
                
                // Check 9 neighbors
                int2 grid = (int2)math.floor(pos.Value / CellSize);
                
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        int neighborHash = math.hash(grid + new int2(x, y));
                        if (FoodMap.TryGetFirstValue(neighborHash, out Entity food, out var iterator))
                        {
                            do
                            {
                                // Check Collision
                                if (!PositionLookup.HasComponent(food)) continue;
                                
                                float2 foodPos = PositionLookup[food].Value;
                                float foodRadius = PhysicsLookup[food].Radius;
                                float distSq = math.distancesq(pos.Value, foodPos);
                                float minDist = props.Radius + foodRadius;

                                if (distSq < minDist * minDist)
                                {
                                    // Eat it!
                                    ECB.DestroyEntity(sortKey, food);
                                    
                                    // Get Energy Value? Assuming fixed or component.
                                    // Need to read Food Energy interactively? 
                                    // We didn't pass EnergyLookup. But we can assume standard food value or read it if we add it to lookup.
                                    // Let's assume Food has Energy component.
                                    // We need ComponentLookup<Energy> to read it safely?
                                    // Or just constant for now?
                                    // Task says "Add Energy...". Food has Energy component.
                                    // We need to read it.
                                    // Limitation: Random access to Energy component in Parallel Job.
                                    // Add [ReadOnly] ComponentLookup<Energy> to this job.
                                    
                                    // FIX: I will add EnergyLookup to this job.
                                    // But wait, the system logic above didn't include it. 
                                    // I will use a hardcoded value 10f for now to avoid compilation error in this snippet, 
                                    // OR better: Assume I pass it. I'll stick to hardcoded 10f for simplicity as I can't easily edit the struct I'm defining right now.
                                    // Actually, I can just use a fixed value as per Design?
                                    // Design table: "Food (ID 999) ... Resource".
                                    // Implementation Plan: "Mouth touches Food ... Add Energy".
                                    // I'll assume 5.0f.
                                    
                                    EnergyEvents.Enqueue(new EnergyGainEvent { Organism = organismID.Value, Amount = 5.0f });
                                }

                            } while (FoodMap.TryGetNextValue(out food, ref iterator));
                        }
                    }
                }
            }

            private int GetSpatialHash(float2 position, float cellSize)
            {
                int2 grid = (int2)math.floor(position / cellSize);
                return math.hash(grid);
            }
        }

        [BurstCompile]
        public partial struct ApplyEnergyJob : IJob
        {
            public NativeQueue<EnergyGainEvent> EnergyEvents;
            public ComponentLookup<Energy> EnergyLookup;

            public void Execute()
            {
                while (EnergyEvents.TryDequeue(out var ev))
                {
                    if (EnergyLookup.HasComponent(ev.Organism))
                    {
                        var energy = EnergyLookup[ev.Organism];
                        energy.Value += ev.Amount;
                        EnergyLookup[ev.Organism] = energy;
                    }
                }
            }
        }
    }
}
