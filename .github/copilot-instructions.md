# GitHub Copilot Instructions for SomeThingsFloat Mod

## Mod Overview and Purpose

**SomeThingsFloat** is a mod for the game RimWorld that allows items and pawns to float in water and move with the current. The primary purpose of this mod is to add a unique gameplay mechanic that allows players to dispose of unwanted items by letting them float away, and to introduce new interactions between pawns, items, and the environment.

## Key Features and Systems

SomeThingsFloat introduces several key features and systems:

- **Floating Items:** Items placed in water will float and move with the current, or randomly in non-streaming water. Players can use this mechanic to dispose of unwanted items like corpses.

- **Spawning Items:** There is a possibility that items can spawn in rivers or space, offering new opportunities for players to use or rescue downed pawns. Failed pawn or corpse generation and placement are reported and do not count as successful spawns.

- **Barriers and Nets:** Players can build barriers and nets in water to catch large objects like corpses, living pawns, weapons, and apparel. This adds an additional layer of strategic gameplay. Hidden despawned pawns are not manually ticked, and hidden objects remain tracked if no suitable water cell is available for restoration.

- **Pawn Mechanics:** Pawns may lose their footing in moving water and can drown if downed. Some apparel and traits can prevent this. Aquatic animals and pawns using the Amphibious movement type are immune to these effects.

- **Floating Values:** The mod calculates the floating value of items based on their materials, with higher values making items move faster in the water. Players can add custom floating values to other items via patches.

- **Performance Options:** Smooth floating animations can be disabled in the mod settings to improve performance.

## Coding Patterns and Conventions

This mod follows standard RimWorld modding conventions and uses the following coding patterns:

- **Harmony Patches:** The mod uses Harmony patches to modify existing RimWorld code. Harmony patches are used to add, remove, or modify code without altering the original RimWorld files.

- **XML Definitions:** The mod defines new game elements using XML files, such as new hediff definitions and thing definitions.

- **C# Code:** The mod's C# code is organized into separate files, with each file containing a single class or a group of related classes.

## XML Integration

SomeThingsFloat uses XML definitions to add new game elements to RimWorld. The XML files are located in the `Defs` directory and include:

- **HediffDefs:** Definitions for new hediffs, such as Drowning and LostFooting.
- **ThingDefs:** Definitions for new things, such as damage definitions and building definitions.
- **UpdateInfo:** A definition for the mod's update information.

## Harmony Patching

This mod uses Harmony patches to modify existing RimWorld code. The following Harmony patches are included in the mod:

- **GenThing_TrueCenter.cs:** A postfix patch for the `GenThing.TrueCenter` method to modify the behavior of items in water.
- **Building_GravEngine_PostSwapMap.cs:** A postfix patch for the `Building_GravEngine.PostSwapMap` method to modify the behavior of gravity engines in water.
- **GenUI_ThingsUnderMouse.cs:** A postfix patch for the `GenUI.ThingsUnderMouse` method to modify the behavior of things under the mouse cursor in water.

## Suggestions for Copilot

To get the most out of Copilot when working on the SomeThingsFloat mod, consider the following suggestions:

- **Use XML Definitions:** When defining new game elements, use XML definitions to add new hediffs, things, and other elements to the game.

- **Harmony Patches:** When modifying existing RimWorld code, use Harmony patches to add, remove, or modify code without altering the original files.

- **C# Code:** Use C# code to implement new functionality, such as calculating floating values and handling pawn mechanics.

- **Performance Optimization:** Consider optimizing performance by disabling smooth floating animations in the mod settings, if necessary.

- **Testing and Debugging:** Test and debug your code thoroughly to ensure that it works as intended and does not cause any issues with the game.

By following these suggestions, you can get the most out of Copilot when working on the SomeThingsFloat mod and contribute to its continued development.

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
- When mentioning SonarQube issues, automatically use the SonarQube MCP service to fetch and address issues instead of making inferred fixes without querying SonarQube first.
- When mentioning the rimworld log, automatically use the Rimworld MCP service to fetch the log.

