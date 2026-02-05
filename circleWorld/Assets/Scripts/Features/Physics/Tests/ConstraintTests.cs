using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using CircleWorld.Features.Cell;

namespace CircleWorld.Features.Physics.Tests
{
    public class ConstraintTests
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
        public void ConstraintSystem_CorrectsPositions_TowardsRestLength()
        {
            // 1. Setup
            var archetype = _entityManager.CreateArchetype(
                typeof(Position), 
                typeof(PreviousPosition), 
                typeof(PhysicsProperties)
            );
            
            Entity entityA = _entityManager.CreateEntity(archetype);
            Entity entityB = _entityManager.CreateEntity(archetype);
            
            // Add Constraints buffer
            _entityManager.AddBuffer<Constraint>(entityA);
            _entityManager.AddBuffer<Constraint>(entityB);

            // Initial State: B is at (2,0). A is at (0,0). Distance = 2.
            _entityManager.SetComponentData(entityA, new Position { Value = new float2(0, 0) });
            _entityManager.SetComponentData(entityB, new Position { Value = new float2(2, 0) });
            
            // Constraint: RestLength = 1.0. Error = 1.0.
            // Bi-directional constraint
            _entityManager.GetBuffer<Constraint>(entityA).Add(new Constraint { Target = entityB, RestLength = 1.0f, Stiffness = 1.0f });
            _entityManager.GetBuffer<Constraint>(entityB).Add(new Constraint { Target = entityA, RestLength = 1.0f, Stiffness = 1.0f });

            // 2. Execute
            var system = _world.GetOrCreateSystem<ConstraintRelaxationSystem>();
            system.Update(_world.Unmanaged);

            // 3. Assert
            // Each entity should correct 50% of the error (weighted by stiffness).
            // Error = 1.0. Correction = 0.5.
            // A moves +0.5 towards B. New A = (0.5, 0).
            // B moves -0.5 towards A. New B = (1.5, 0).
            
            var posA = _entityManager.GetComponentData<Position>(entityA).Value;
            var posB = _entityManager.GetComponentData<Position>(entityB).Value;
            
            Assert.AreEqual(0.5f, posA.x, 0.001f);
            Assert.AreEqual(1.5f, posB.x, 0.001f);
        }
    }
}
