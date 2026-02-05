# Coding Rules & Development Guidelines

This document establishes the non-negotiable rules for development in **Circle World**.

## 1. Minimal & Maintainable Code
*   **YAGNI (You Ain't Gonna Need It)**: Do not implement features "just in case". Write the minimum code required to pass the current requirement or test.
*   **Simple over Clever**: Prefer readable, boring code over complex "one-liners".
*   **Delete aggressively**: If a feature is removed, remove its code. Do not comment it out.

## 2. Separation of Concerns (SRP)
*   **One File, One Responsibility**:
    *   Each System handles **one** specific logical loop (e.g., `MotionSystem` moves things, it does not check collisions).
    *   Each File contains exactly one main class or struct.
*   **Strict ECS Separation**:
    *   **Components**: Pure Data. No methods.
    *   **Systems**: Pure Logic. No state (except cached query handles).

## 3. Adaptability & Extensibility (Open/Closed)
*   **Data-Driven**: Behavior should be tuned via data (Components/ScriptableObjects), not by changing code.
*   **Modular Systems**: New features should be added by creating **new Systems**, not by modifying existing massive ones.

## 4. Test-Driven Development (TDD)
*   **The Golden Rule**: Write the test **before** the implementation.
    1.  **Red**: Write a failing test for the desired behavior.
    2.  **Green**: Write the minimal code to make the test pass.
    3.  **Refactor**: Clean up the code while keeping the test green.
*   **Testable Logic**: Isolate math and logic into `static` helper functions or unmanaged Jobs that can be unit-tested without running the full Unity Editor.

## 5. Clean Project Structure
*   **Domain-Based Grouping**: Group files by **Feature**, not just type.
    *   *Bad*: `AllSystems/`, `AllComponents/`
    *   *Good*: `Features/Movement/` (contains `MovementSystem.cs`, `VelocityComponent.cs`, `MovementTests.cs`)
*   **Consistent Naming**: Follow the naming conventions defined in `highlevel_decisions_and_paradigms.md`.

## 6. Living Documentation
*   **Minimal**: Documentation should explain **Requirements** and **Architecture** (The "Why"), not line-by-line implementation (The "How").
*   **Sync**: If you change the behavior of a System, you **MUST** update the corresponding section in `overview.md` or `implementation_plan_detailed.md` in the same commit.
