# Circle World: Design Document (ECS Edition)

## 1. Project Overview
**Circle World** is a minimal 2D evolution simulation where thousands of autonomous circles ("Cells") interact, merge, and evolve into complex multi-cellular organisms.

**Technical Core**: Built effectively from scratch using **Unity DOTS (ECS)** and a custom **Verlet Integration Physics** engine to support massive scale (10,000+ entities).

## 2. Core Concepts

### The Entity (Cell)
*   **Visual**: Simple 2D Circle (Instanced Mesh).
*   **Physics**: **Verlet Integration** (Implicit Velocity).
    *   No `Rigidbody2D`. Behavior is driven by position history.
*   **Movement**: Brownian motion applied purely as position offset.
*   **Identity**: Defined by `CellType Component` (Structure, Mouth, Connector).

### The Organism (Cluster)
*   **Virtual Structure**: An Organism is not a GameObject parent. It is a logical grouping defined by a shared **`OrganismID`** on multiple cells.
*   **Connections**: Cells are effectively "glued" together by **Stick Constraints**.
*   **Shared Stats**: A dedicated **OrganismEntity** holds the aggregated `Energy` and `Stats` for the group.

## 3. Global Stats & Resources
These values are aggregated on the **OrganismEntity**:

| Stat | Description | Formula / Source |
| :--- | :--- | :--- |
| **Entity Count** | Total number of connected cells. | Counted via System Query. |
| **Energy** | Survival fuel. | Consumed by Mouth Cells. |
| **Max Energy** | Analysis of maximum capacity. | Sum of Storage Cells. |

## 4. Cell Types (ID System)

| ID | Name | Role | Behavior (System Logic) |
| :-- | :-- | :--- | :--- |
| **0** | **Structure** | Body | Inert. Held by constraints. |
| **1** | **Connector** | Merging | **Collision System**: If touching a foreign cell, creates a new Constraint and merges `OrganismID`. |
| **2** | **Mouth** | Feeding | **Collision System**: If touching Food (ID 999), adds energy to `OrganismID`, destroys food. |
| **3** | **Storage** | Capacity | Passive buff to MaxEnergy. |
| **4** | **Reproducer** | Cloning | **Reproduction System**: If `Energy > Threshold`, initiates a structural copy of the entire Organism graph. |
| **999**| **Energy** | Resource | Floating particles. |

## 5. Mechanics & Algorithms

### Verlet Physics (The "Jelly" Solver)
1.  **Movement**: Update positions based on previous frame velocity.
2.  **Constraint Relaxation**: Loop X times:
    *   For every connection: Pull cells together/apart to maintain exact `RestLength`.
3.  **Collision**:
    *   **Spatial Hash**: Grid-based lookup to find neighbors.
    *   **Resolution**: Push overlapping circles apart.

### Merging Logic
1.  **Event**: Connector touches foreign Body.
2.  **Structural Change**:
    *   Determine dominant `OrganismID` (or create new).
    *   Update all cells in both groups to share the same `OrganismID`.
    *   Create a new **Constraint Edge** between the two colliding cells.
    *   Sum Energy.

### Reproduction Logic
1.  **Snapshot**: Identify all cells and constraints with `OrganismID == X`.
2.  **Batch Instantiate**: Create copies of all Cells and Constraints.
3.  **Offset**: Shift positions of the copy to avoid immediate collision.
