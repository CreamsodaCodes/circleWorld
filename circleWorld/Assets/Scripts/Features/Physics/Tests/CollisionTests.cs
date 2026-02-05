using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using CircleWorld.Features.Cell;

namespace CircleWorld.Features.Physics.Tests
{
    public class CollisionTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void Setup()
        {
            _world = World.DefaultGameObjectInjectionWorld = World.Create();
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void CollisionSystem_SeparatesOverlappingEntities()
        {
            // 1. Setup
            var archetype = _entityManager.CreateArchetype(
                typeof(Position), 
                typeof(PreviousPosition), 
                typeof(PhysicsProperties)
            );
            
            Entity entityA = _entityManager.CreateEntity(archetype);
            Entity entityB = _entityManager.CreateEntity(archetype);
            
            // Initial State: A at (0,0), B at (0.5, 0).
            // Radius 1.0 each. Rest distance > 2.0.
            // Overlap = 1.5. 
            _entityManager.SetComponentData(entityA, new Position { Value = new float2(0, 0) });
            _entityManager.SetComponentData(entityA, new PhysicsProperties { Radius = 1.0f, Friction = 0.5f });
            
            _entityManager.SetComponentData(entityB, new Position { Value = new float2(0.5f, 0) });
            _entityManager.SetComponentData(entityB, new PhysicsProperties { Radius = 1.0f, Friction = 0.5f });

            // 2. Execute
            var system = _world.GetOrCreateSystem<CollisionSystem>();
            system.Update(_world.Unmanaged);

            // 3. Assert
            // They should push apart.
            // A should move Left (< 0). B should move Right (> 0.5).
            
            var posA = _entityManager.GetComponentData<Position>(entityA).Value;
            var posB = _entityManager.GetComponentData<Position>(entityB).Value;
            
            Assert.Less(posA.x, 0f);
            Assert.Greater(posB.x, 0.5f);
            
            // Distance should increase
            float dist = math.distance(posA, posB);
            Assert.Greater(dist, 0.5f);
        }
    }
}
