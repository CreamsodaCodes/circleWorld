using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using CircleWorld.Features.Cell;
using CircleWorld.Features.Physics;

namespace CircleWorld.Features.Reproduction.Tests
{
    public class ReproductionTests
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
            if (_world.IsCreated)
                _world.Dispose();
        }

        [Test]
        public void Reproduction_ClonesConnectedCells_AndRemapsConstraints()
        {
            // 1. Setup: Create a 2-cell organism (A - B)
            var archetype = _entityManager.CreateArchetype(
                typeof(Position),
                typeof(PhysicsProperties),
                typeof(OrganismID),
                typeof(CellType),
                typeof(Energy) // Needed for reproduction logic check
            );

            Entity entityA = _entityManager.CreateEntity(archetype); // Reproducer
            Entity entityB = _entityManager.CreateEntity(archetype); // Structure

            _entityManager.AddComponentData(entityA, new OrganismID { Value = 1 });
            _entityManager.AddComponentData(entityB, new OrganismID { Value = 1 });

            // Set A as Reproducer with high energy
            _entityManager.SetComponentData(entityA, new CellType { Value = CellKind.Reproducer });
            _entityManager.SetComponentData(entityA, new Energy { Value = 100.0f });
            
            // Connect A to B
            _entityManager.AddBuffer<Constraint>(entityA).Add(new Constraint { Target = entityB, RestLength = 1f, Stiffness = 1f });
            _entityManager.AddBuffer<Constraint>(entityB).Add(new Constraint { Target = entityA, RestLength = 1f, Stiffness = 1f });

            // 2. Execute
            // We need to create and update the system
            var system = _world.GetOrCreateSystem<ReproductionSystem>();
            system.Update(_world.Unmanaged);

            // 3. Assert
            // We expect 4 entities total now (2 original + 2 clones)
            var allEntities = _entityManager.GetAllEntities(Allocator.Temp);
            Assert.AreEqual(4, allEntities.Length, "Should have 4 entities after reproduction");

            // Identify clones. Clones should be new entities.
            // We assume the system changes the clones logic or position, but for now we check existence and connectivity.
            
            // Find the new Reproducer (The one that is NOT entityA)
            Entity cloneA = Entity.Null;
            Entity cloneB = Entity.Null;

            foreach (var e in allEntities)
            {
                if (e.Index != entityA.Index && e.Index != entityB.Index)
                {
                    // It's a clone. Is it A or B?
                    var type = _entityManager.GetComponentData<CellType>(e).Value;
                    if (type == CellKind.Reproducer) cloneA = e;
                    else cloneB = e;
                }
            }

            Assert.AreNotEqual(Entity.Null, cloneA, "Clone A not found");
            Assert.AreNotEqual(Entity.Null, cloneB, "Clone B not found");

            // Check constraint remapping
            // CloneA should be connected to CloneB
            var constraintsA = _entityManager.GetBuffer<Constraint>(cloneA);
            Assert.AreEqual(1, constraintsA.Length);
            Assert.AreEqual(cloneB, constraintsA[0].Target, "Clone A should be connected to Clone B, not Original B");

            var constraintsB = _entityManager.GetBuffer<Constraint>(cloneB);
            Assert.AreEqual(1, constraintsB.Length);
            Assert.AreEqual(cloneA, constraintsB[0].Target, "Clone B should be connected to Clone A, not Original A");
            
            allEntities.Dispose();
        }
    }
}
