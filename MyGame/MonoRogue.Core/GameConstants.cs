namespace MonoRogue.Core;

/// <summary>
/// Centralized gameplay tuning values and shared ARGB colors.
/// </summary>
public static class GameConstants
{
    public const int DefaultEnergyPerTurn = 100;
    public const int DefaultActionCost = 100;
    public const int MaxMonsterActionsPerTurn = 4;
    public const int TurnTimeQuantum = 100;

    public const int DefaultPlayerHealth = 20;
    public const int DefaultMonsterHealth = 8;
    public const int DefaultPlayerAttack = 5;
    public const int DefaultMonsterAttack = 3;
    public const int DragonAttack = 5;

    // Experience awarded when a monster is slain. The fallback path (no JSON content)
    // uses these values; content-driven monsters read their value from monsters.json.
    public const int DefaultMonsterExperience = 10;
    public const int DragonExperience = 50;

    // Cumulative XP thresholds for player levels (index 0 => level 1, index 1 => level 2, ...).
    // Adjustable so progression can be rebalanced without touching game logic.
    public static readonly int[] DefaultLevelChart = [0, 20, 50, 100, 180, 300];

    public const int ArgbBlack = unchecked((int)0xFF000000);
    public const int ArgbWhite = unchecked((int)0xFFFFFFFF);
    public const int ArgbRed = unchecked((int)0xFFFF0000);
    public const int ArgbYellow = unchecked((int)0xFFFFFF00);
    public const int ArgbOrangeRed = unchecked((int)0xFFFF4500);
}
