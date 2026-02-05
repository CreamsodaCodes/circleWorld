# High-Level Decisions & Paradigms (ECS Edition)

This document outlines the strict **Data-Oriented Technology Stack (DOTS)** adoption for **Circle World**.

## 1. Architectural Pattern: Pure DOTS (ECS)
We are moving away from GameObjects for simulation. All gameplay logic will run on the **Entity Component System**.

### Core Philosophy
*   **Performance First**: We target support for 10,000+ active cells.
*   **Systems over Objects**: There are no "Cell Objects". There are only arrays of `Position`, `Radius`, and `OrganismID` that Systems iterate over.
*   **Jobs & Burst**: All simplified logic must be Burst-compatible.

---

## 2. Physics Paradigm: Custom Verlet Integration
Standard Unity Physics2D (`Rigidbody2D`) is too heavy and not cache-friendly enough for our massive cell count.

### 2.1 The "Jelly" Physics
Instead of Rigidbodies, we use **Verlet Integration**:
1.  **Motion**: `Position_Next = Position_Current + (Position_Current - Position_Old)`.
    *   Velocity is implicit. No mass calculation needed for basic movement.
2.  **Constraints (Sticky Connections)**:
    *   Cells are connected by **Distance Constraints** (Stick Constraints).
    *   System iterates connections and forces cells to their target distance.
    *   This creates a stable, "cloth-like" or "soft-body" structure perfect for biological organisms.
3.  **Collision**:
    *   Simple Circle-Circle overlap resolution.
    *   Use a **Spatial Hash Map** (MultiHashMap) to broadcast collision pairs efficiently.

---

## 3. Data Design (Data-Oriented)

### 3.1 Component Data (Structs Only)
*   **Rule**: `IComponentData` structs MUST be unmanaged (blittable types only: `float`, `int`, `float2`).
*   **NO**: Strings, Classes, Lists inside Components.
*   **YES**: `FixedList` if strictly necessary, or `DynamicBuffer` components.

### 3.2 Archetypes
We define entities by their composition:
*   **Cell**: `Translation`, `CompositeScale`, `PhysicsState` (Pos/PrevPos), `OrganismID`, `CellTypeID`.
*   **Connection**: A separate Entity? OR a `DynamicBuffer<ConnectedCell>` on the Cell entity?
    *   **Decision**: **Edge Entities** or `DynamicBuffer<ConnectionElement>` on cells. Buffer is preferred for cache locality during constraint solving.

---

## 4. Systems Architecture

### 4.1 Frame Execution Order
1.  **InputSystem**: Reads mouse/keyboard, writes to a Singleton InputComponent.
2.  **ReproductionSystem**: Checks energy thresholds, schedules structural changes (CommandBuffer).
3.  **SpawningSystem**: Executing ECB (EntityCommandBuffer) to create interactions.
4.  **MovementSystem** (Job): Updates Verlet positions.
5.  **ConstraintSystem** (Job): Solves stick constraints (Relaxation steps).
6.  **CollisionSystem** (Job): Spatial Hash check -> overlapping resolution -> Logic Events (Eating/Merging).
7.  **RenderSystem**: Syncs ECS transform data to a minimal generic mesh instancing system (Hybrid Renderer).

---

## 5. Coding Standards (DOTS)

*   **Burst Compile Everything**: All Jobs must have `[BurstCompile]`.
*   **Native Collections**: Use `NativeArray`, `NativeHashMap` for data passing. Always Dispose.
*   **EntityCommandBuffer**: Structural changes (Add/Remove Component, Create/Destroy Entity) CANNOT happen inside parallel jobs. They must be recorded to an ECB and played back.

---


