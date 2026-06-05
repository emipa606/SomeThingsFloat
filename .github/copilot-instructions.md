# GitHub Copilot Instructions for RimWorld Modding: Some Things Float

## Mod Overview and Purpose
**Some Things Float** is a RimWorld mod that introduces dynamic interaction with water bodies within the game world. By enabling items and pawns to float in water and move with the stream, this mod adds layers of realism and gameplay opportunities. Objects can both leave the map, acting as a neat disposal system, and drift into the map, offering newfound resources or companions. This project enhances storytelling and strategical depth with water as an active agent.

## Key Features and Systems
### Features
- **Floating Mechanics**: Items and pawns buoy and flow with water currents, whether streaming (directional) or random water bodies.
- **Dynamic Disposal**: Objects can exit the map through waterways, allowing practical removal of unwanted items like corpses.
- **Resource Influx**: Items and characters can spawn in water, offering new resources or rescue opportunities for downed pawns.
- **Swim Speed Implications**: Swim speed from other mods (e.g., SwimmingKit) influences how efficiently pawns float when downed.
- **Aquatic Kits**: Apparel and traits influence drowning risks; power armor helmets and SOS2 EVA-tagged headgear provide safety.
- **Structures**: Build bars and nets in water to intercept floating large items or all types of items, respectively.

### Systems
- **Footing and Drowning**: Pawns risk losing balance or drowning in moving water; preventive measures exist through gear or mod synergies.
- **Floating Value Calculation**: Determines item movement speed on water based on material properties and item type modifications.
- **Performance Management**: Smooth floating animations can be toggled in settings to manage distractions or performance dips.

## Coding Patterns and Conventions
- **C# File Structure**: Use appropriate access modifiers, organize classes into utilities or handlers, and adhere to single responsibility principles.
- **XML Definitions**: Define items, effects, and structures hierarchically; reuse and extend base definitions wherever possible.

## XML Integration
- **Hediff Definitions**: Utilize `<HediffDef>` to manage health impacts like drowning and losing footing.
- **Building Definitions**: Capture structure properties using `<ThingDef>` for items like nets and bars, implementing inheritance for shared attributes.

## Harmony Patching
- **Patch Strategy**: Apply Harmony patches to augment or alter vanilla game behaviors—such as pawn interactions with water—while maintaining compatibility with existing systems.
- **Use Cases**: Modify behaviors in `Pawn_HealthTracker_MakeDowned` and `Pawn_PathFollower_SetupMoveIntoNextCell`.

## Suggestions for Copilot
1. **Automate Repetitive Code**: Encourage use of Copilot to suggest auto-generated code for common patterns like floating calculations or animation toggles.
2. **Template XML Entries**: Provide boilerplate XML framework for simpler hedgedif or thing definitions.
3. **Patch Suggestions**: Suggest potential improvements or bug fixes via Harmony method detours or postfixes.
4. **Integration Tests**: Encourage the creation of automated tests to verify item buoyancy behaviors across different conditions and mod setups.
5. **Documentation**: Suggest inline documentation and summaries on key methods and classes for improved codebase clarity.

> This mod was initially crafted for personal use but stands to enrich playthroughs for others. Thank you, notably llunak, for performance enhancements.

## Project Solution Guidelines
- Relevant mod XML files are included as Solution Items under the solution folder named XML, these can be read and modified from within the solution.
- Use these in-solution XML files as the primary files for reference and modification.
- The `.github/copilot-instructions.md` file is included in the solution under the `.github` solution folder, so it should be read/modified from within the solution instead of using paths outside the solution. Update this file once only, as it and the parent-path solution reference point to the same file in this workspace.
- When making functional changes in this mod, ensure the documented features stay in sync with implementation; use the in-solution `.github` copy as the primary file.
- In the solution is also a project called Assembly-CSharp, containing a read-only version of the decompiled game source, for reference and debugging purposes.
- For any new documentation, update this copilot-instructions.md file rather than creating separate documentation files.


## Hard rules (must follow)
- Do NOT run commands that modify the repo (no git commit, git apply, dotnet format) unless explicitly asked.
- Prefer minimal reads: read only the smallest code region needed (around the suspicious lines).

