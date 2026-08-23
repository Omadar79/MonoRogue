namespace MonoRogue.Core;

/// <summary>
/// Tracks the player's experience earned during the current run and derives a character level from an adjustable 
/// cumulative-XP threshold chart. Applying level-up effects is deferred to a later milestone; this class only 
/// accumulates XP and reports the resulting level.
/// </summary>
public sealed class PlayerExperience
{
    private readonly IReadOnlyList<int> _levelChart;

    public PlayerExperience(IReadOnlyList<int>? levelChart = null)
    {
        _levelChart = levelChart ?? GameConstants.DefaultLevelChart;
    }

    private int _current;

    // Total experience accumulated this run.
    public int GetCurrent() => _current;

    // Character level derived from <see cref="GetCurrent"/>.
    public int GetLevel() => CalculateLevel(_current);

    // Adds experience. Returns the amount actually added.
    public int Award(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        _current += amount;
        return amount;
    }

    // Sets the accumulated experience directly (used when restoring a save).
    public void SetExperience(int amount) => _current = Math.Max(0, amount);

    // Level for an arbitrary XP total.
    public int CalculateLevel(int xp)
    {
        var level = 1;
        for (var i = 1; i < _levelChart.Count; i++)
        {
            if (xp >= _levelChart[i])
            {
                level = i + 1;
            }
            else
            {
                break;
            }
        }

        return level;
    }

    // XP required to reach the next level, or 0 when at the highest level.
    public int XpForNextLevel()
    {
        var level = GetLevel();
        if (level >= _levelChart.Count)
        {
            return 0;
        }

        return _levelChart[level];
    }
}
