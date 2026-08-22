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

    public const int ArgbBlack = unchecked((int)0xFF000000);
    public const int ArgbWhite = unchecked((int)0xFFFFFFFF);
    public const int ArgbRed = unchecked((int)0xFFFF0000);
    public const int ArgbYellow = unchecked((int)0xFFFFFF00);
    public const int ArgbOrangeRed = unchecked((int)0xFFFF4500);
}
