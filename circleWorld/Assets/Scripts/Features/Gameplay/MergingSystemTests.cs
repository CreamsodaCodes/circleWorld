using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using NUnit.Framework;
using CircleWorld.Features.Cell;
using CircleWorld.Features.Gameplay;
using CircleWorld.Features.Physics;

namespace CircleWorld.Features.Gameplay.Tests
{
    public class MergingSystemTests
    {
        private World _world;
        private EntityManager _entityManager;
        private SystemHandle _mergingSystem;

        [SetUp]
        public void Setup()
        {
            _world = new World("TestWorld");
            _entityManager = _world.EntityManager;
            
            // Create the system
            _mergingSystem = _world.CreateSystem<MergingSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void MergingSystem_ConnectorTouchesStructure_MergesAndCreatesConstraint()
        {
            // 1. Setup two organisms
            // Organism A: Structure + Connector
            // Organism B: Structure
            
            var organismA_ID = _entityManager.CreateEntity(); // Dummy root for A
            var organismB_ID = _entityManager.CreateEntity(); // Dummy root for B

            // Entity A1: Connector
            var entityA_Connector = _entityManager.CreateEntity(
                typeof(Position),
                typeof(PreviousPosition),
                typeof(PhysicsProperties),
                typeof(OrganismID),
                typeof(CellType),
                typeof(Constraint) // Buffer
            );
            _entityManager.SetComponentData(entityA_Connector, new Position { Value = new float2(0, 0) });
            _entityManager.SetComponentData(entityA_Connector, new PhysicsProperties { Radius = 1f });
            _entityManager.SetComponentData(entityA_Connector, new OrganismID { Value = organismA_ID });
            _entityManager.SetComponentData(entityA_Connector, new CellType { Value = CellKind.Connector });

            // Entity B1: Structure
            var entityB_Structure = _entityManager.CreateEntity(
                typeof(Position),
                typeof(PreviousPosition),
                typeof(PhysicsProperties),
                typeof(OrganismID),
                typeof(CellType),
                typeof(Constraint) // Buffer
            );
            // Place it overlapping with A1. Radius is 1, so dist < 2 means overlap.
            _entityManager.SetComponentData(entityB_Structure, new Position { Value = new float2(1.5f, 0) }); 
            _entityManager.SetComponentData(entityB_Structure, new PhysicsProperties { Radius = 1f });
            _entityManager.SetComponentData(entityB_Structure, new OrganismID { Value = organismB_ID });
            _entityManager.SetComponentData(entityB_Structure, new CellType { Value = CellKind.Structure });

            // 2. Add properties required for system update
            // (Assumed query uses Position, PhysicsProperties, OrganismID, CellType)
            
            // 3. Run System
            _world.Update();

            // 4. Assert
            
            // A. Check IDs merged
            var idA = _entityManager.GetComponentData<OrganismID>(entityA_Connector).Value;
            var idB = _entityManager.GetComponentData<OrganismID>(entityB_Structure).Value;
            
            Assert.AreEqual(idA, idB, "Organism IDs should be the same after merge.");
            
            // B. Check Constraint created on A linking to B (or vice versa)
            var bufferA = _entityManager.GetBuffer<Constraint>(entityA_Connector);
            var bufferB = _entityManager.GetBuffer<Constraint>(entityB_Structure);
            
            bool aConnectedToB = false;
            foreach(var c in bufferA) { if(c.Target == entityB_Structure) aConnectedToB = true; }
            
            bool bConnectedToA = false;
            foreach(var c in bufferB) { if(c.Target == entityA_Connector) bConnectedToA = true; }

            Assert.IsTrue(aConnectedToB || bConnectedToA, "A new constraint should link the two entities.");
        }
    }
}
