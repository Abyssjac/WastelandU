# WastelandU Agent Instructions

## PropertyDatabase rules

Before analysing, creating, or modifying a feature that mentions `PropertyDatabase`, `PropertyDatabaseManager`, `property database logic`, `propertyDataabse`, or `使用 propertyDatabase 逻辑`, read [Assets/_AIRules/SKILL.md](Assets/_AIRules/SKILL.md).

Do not apply this rule to an unrelated generic database, SQL, or save-data task merely because it uses the words `database` or `DB`.

## Default collaboration and change authority

### Before explicit implementation approval

- Default to read-only analysis and discussion.
- Do not modify any project file, scene, prefab, ScriptableObject, Inspector assignment, project setting, package/configuration, or external state.
- Read-only code search, asset inspection, and architecture analysis are allowed.
- Identify missing requirements, risks, and reasonable options before proposing changes.
- Do not begin implementation merely because a solution is understood. Wait for an explicit instruction to modify or implement.

### Final change-scope inventory on request

Enter the full change-scope inventory mode only when the user's request contains both:

1. A finalization term such as `最终`, `最后`, `最终版`, or `最后一轮`; and
2. A planning term such as `梳理`, `列出`, `计划`, `修改清单`, or `改动方案`.

A planning term alone must not trigger the full inventory. A finalization term alone must not trigger the full inventory.

Before producing the inventory, re-check the latest relevant project state when earlier findings may be stale. The inventory remains read-only and is not implementation authorization.

The final inventory must include:

- Goal, confirmed constraints, assumptions, unresolved questions, and risks.
- Every affected or new script: exact path, class/type, whether it is new or modified, responsibility, intended change, and relevant APIs, methods, events, and call flow.
- Naming and file-placement decisions for enums, classes, methods, assets, prefabs, and compatibility constraints.
- A concise relationship map showing how the changed scripts and systems communicate.
- Every Unity asset impact, separated into Scene, GameObject/component, Prefab, ScriptableObject, Inspector reference, and Project Settings. For each, state whether it would be created, modified, moved, deleted, or intentionally left unchanged, and whether explicit asset-operation authorization is required.
- Recommended implementation order: data model/enums; PropertyDatabase or runtime state; Manager APIs and call chain; UI presenter/UI scripts; Prefab, SO, and Inspector steps; then testing and save-compatibility verification.
- Planned code/build checks, runtime tests, required Inspector setup, and manual Unity steps.

End the inventory by stating that no project files or Unity assets were changed, then wait for explicit implementation approval.

### Implementation approval

- File changes require an explicit request such as `修改项目`, `开始实现`, `请实现`, or `输出代码并修改`.
- `输出代码` alone means provide code in the reply only; it does not authorize file edits.
- Unless the user explicitly expands the scope, implementation may modify only requested source scripts and closely related source-code files.
- Do not use MCP or the Unity UI to create, delete, or edit scenes, GameObjects, Prefabs, ScriptableObjects, Inspector references, or project settings unless the user explicitly authorizes those asset operations.
- Do not add, delete, duplicate, move, or assign SO assets by default.
- Read-only Unity/Inspector inspection is allowed when useful; persistent Unity changes are not.

### Required handoff after every implementation

- Summarize every changed file and why it changed.
- State required Inspector setup; explicitly say `无需 Inspector 设置` when none is needed.
- Provide concrete testing steps and state what was or was not verified.
- Clearly identify any manual Unity, scene, Prefab, or SO steps that remain for the user.
