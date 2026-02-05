# Circle World - Detailed Implementation Plan (ECS / DOTS)

## 1. Core Data (Components)
All data structures must be **unmanaged structs**.

### 1.1 Cell Data [Implemented]
*   **File**: `Assets/Scripts/ECS/Components/CellComponents.cs`
*   **`struct Position : IComponentData`**
    *   `float2 Value;`
*   **`struct PreviousPosition : IComponentData`**
    *   `float2 Value;` (Used for Verlet Velocity)
*   **`struct PhysicsProperties : IComponentData`**
    *   `float Radius;`
    *   `float Friction;`
    *   `float Bounciness;`
*   **`struct OrganismID : IComponentData`**
    *   `int Value;` (Points to the Organism Entity)

### 1.2 Behavior Tags
*   **`struct CellType : IComponentData`**
    *   `enum Kind { Structure, Connector, Mouth, Storage, Reproducer, Food }`
*   **`struct Energy : IComponentData`**
    *   `float Value;` (Only for Food or OrganismRoot)

### 1.3 Topology (The Graph)
*   **`struct Constraint : IBufferElementData`**
    *   `Entity TargetEntity;`
    *   `float RestLength;`
    *   `float Stiffness;`

---

## 2. Systems Architecture (The Logic)

### 2.1 Physics Loop (SystemGroup: FixedStepSimulation)
*   **1. `IntegrationSystem`** `[BurstCompile]`
    *   **Job**: Iterate all cells.
    *   `Velocity = Position - PrevPosition`
    *   `PrevPosition = Position`
    *   `Position += Velocity + (Forces * DeltaTime)`
    *   Apply Bounds checks (box containment).

*   **2. `ConstraintRelaxationSystem`** `[BurstCompile]`
    *   **Job**: Iterate all Entities with `Constraint` buffer.
    *   For each constraint:
        *   Fetch `Position` of Self and Target.
        *   Calculate Delta and distance.
        *   Correct positions of BOTH to satisfy `RestLength`.
    *   *Note*: Run this job 4-8 times per frame for stiffness.

*   **3. `CollisionSystem`** `[BurstCompile]`
    *   **Data**: `NativeMultiHashMap<int, Entity>` (Spatial Hash).
    *   **Job 1 (Hash)**: Map every cell to a Grid ID.
    *   **Job 2 (Check)**: Check neighbors in hash map. If Distance < (R1 + R2), push apart.

### 2.2 Gameplay Systems (SystemGroup: Simulation)
*   **4. `EatingSystem`**
    *   **Job**: Iterate pairs from `CollisionSystem`.
    *   If `Mouth` touches `Food`:
        *   Mark Food for destruction.
        *   Add Energy to `Mouth.OrganismID` (Buffer or component lookup).

*   **5. `MergingSystem`** [Implemented]
    *   **Job**: Iterate pairs from `CollisionSystem`.
    *   If `Connector` touches `Structure` (and different OrganismID):
        *   **Event**: Schedule Merge (`EntityCommandBuffer`).
        *   **Merge Logic**: Reassign IDs of smaller organism to larger. Create a `Constraint` link.

*   **6. `ReproductionSystem`**
    *   **Job**: Query `OrganismRoots` with `Energy > Cost`.
    *   **Logic**:
        *   **Serializer**: Graph traversal to capture the blueprint of the organism.
        *   **Instantiator**: `EntityManager.Instantiate` batch.
        *   **Mapper**: Remap the `TargetEntity` in all new Constraints to point to the *new* clones, not the old originals.

---

## 3. Rendering (Hybrid)
Pure ECS rendering can be complex. We will use **Unity.Rendering** (Hybrid Renderer).
*   **`RenderMesh`**: Standard DOTS component.
*   **`LocalToWorld`**: Updated by Unity's internal transform systems (we just write to `Translation`).
*   **Visuals**:
    *   Circles: Standard Mesh.
    *   Connections: `LineRenderer` is NOT ECS friendly.
    *   **Solution**: Use a stretched "Quad" Mesh Entity for every constraint? Or use `DrawMeshInstanced` in a Monobehaviour that reads ECS data. **Choice**: **Mesh Entities** for lines (thin rectangles).

---

## 4. Execution Plan
1.  **Project Setup**: Install `Entities`, `Burst`, `Collections`, `Jobs` packages. [Completed]
2.  **Basic Physics**: Implement `IntegrationSystem` and `RenderSystem` (Balls moving).
3.  **Interaction**: Implement `CollisionSystem` (Balls bouncing).
4.  **Structure**: Implement `ConstraintSystem` and `Constraint` buffer (Balls sticking).
5.  **Logic**: Implement IDs, Merging, and Reproduction.
