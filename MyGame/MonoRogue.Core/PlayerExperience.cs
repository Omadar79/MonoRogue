namespace MonoRogue.Core;

/// <summary>
/// Tracks the player's experience earned during the current run and derives a
/// character level from an adjustable cumulative-XP threshold chart. Applying
/// level-up effects is deferred to a later milestone; this class only accumulates
/// XP and reports the resulting level.
/// </summary>
public sealed class PlayerExperience
{
    private readonly IReadOnlyList<int> _levelChart;

    public PlayerExperience(IReadOnlyList<int>? levelChart = null)
    {
        _levelChart = levelChart ?? GameConstants.DefaultLevelChart;
    }

    private int _current;

    /// <summary>Total experience accumulated this run.</summary>
    public int GetCurrent() => _current;

    /// <summary>Character level derived from <see cref="GetCurrent"/>.</summary>
    public int GetLevel() => CalculateLevel(_current);

    /// <summary>Adds experience. Returns the amount actually added.</summary>
    public int Award(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        _current += amount;
        return amount;
    }

    /// <summary>Sets the accumulated experience directly (used when restoring a save).</summary>
    public void SetExperience(int amount) => _current = Math.Max(0, amount);

    /// <summary>Level for an arbitrary XP total.</summary>
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

    /// <summary>XP required to reach the next level, or 0 when at the highest level.</summary>
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
