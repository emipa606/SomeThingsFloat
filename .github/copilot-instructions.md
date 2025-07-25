# GitHub Copilot Instructions for RimWorld "Some Things Float" Mod

## Mod Overview and Purpose

The "Some Things Float" mod enhances the game RimWorld by introducing new mechanics related to floating objects and drowning. The mod adds a layer of strategy to water bodies on the map, making them more interactive and significant in gameplay. Players must manage their colonies with the understanding that some objects and pawns can float or drown, which affects resource management and colony safety.

## Key Features and Systems

- **Floating Objects Mechanic**: Certain objects can float on water. This mechanic is controlled by the `FloatingThings_MapComponent` class, which tracks and updates floating objects on the map.

- **Drowning and Sinking**: Pawns can drown or sink if they are not careful near water bodies. Classes like `Hediff_OnlyFloating` help manage the state of pawns related to floating.

- **Alerts System**: The mod includes alerts to warn players about floating objects or pawns. The classes `Alert_ColonistIsFloatingAway` and `Alert_ThingsUnderBridge` handle these alerts.

- **Achievement Tracking**: The mod includes achievement tracking for specific events, such as when enemies drown, managed by classes like `EnemyDrownedTracker`.

- **Rewards System**: Completing certain achievements can trigger specific rewards, such as spawning random items. This is handled by the `Reward_SpawnRandomItem` class.

## Coding Patterns and Conventions

- **Class Organization**: Classes are organized based on functionality, e.g., trackers, alerts, and mod settings.
  
- **Method Naming**: Methods are named using camelCase for private methods and PascalCase for public methods, consistent with typical C# conventions.

- **Static vs Instance**: Static classes are used where shared functionality is intended, whereas individual objects are used for specific behaviors.

## XML Integration

While the C# summary does not explicitly detail XML integration, XML is typically used in RimWorld mods to define game data such as item properties and events. Consider maintaining XML files to define:

- **ThingDefs** for new floating objects or modified properties.
- **HediffDefs** for conditions like floating or drowning.
- Link XML definitions with C# logic using `DefModExtensions`.

## Harmony Patching

**Harmony** is a library used for patching existing methods in RimWorld. Consider the following:

- **Prefix and Postfix Methods**: Use Harmony prefixes and postfixes to execute code before or after game functions are called. This allows you to extend or modify vanilla behavior.

- **Example Usage**: Apply patches in classes like `SomeThingsFloatMod` to integrate floating and drowning mechanics into existing game logic.

## Suggestions for Copilot

To effectively utilize GitHub Copilot in this mod development:

1. **Define Clear Functionality**: Write clear comments about what each method should do. Provide context and expected behavior in comments to guide Copilot suggestions.

2. **Use Descriptive Names**: For variables and methods, use descriptive names that reflect their purpose. This helps Copilot suggest more relevant code snippets.

3. **Decompose Logic**: Break down large methods into smaller, focused functions. Copilot can then generate suggestions per function, making logic easier to manage and test.

4. **Prioritize Common Patterns**: Encourage Copilot to follow the prevalent methods and logic patterns within your code. Consistency aids in generating reliable code.

By adhering to these guidelines, you can leverage GitHub Copilot to streamline your mod development process, generating code that fits seamlessly within the RimWorld modding architecture.
