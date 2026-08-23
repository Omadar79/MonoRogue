using MonoRogue.Data;

namespace MonoRogue.Core;

/// <summary>
/// Player inventory: an ordered list of item stacks with stacking, gold totals, and potion-consumption bookkeeping.
/// Pure data — no ECS or UI dependencies, so it can be unit tested in isolation.
/// </summary>
public sealed class Inventory
{
    private readonly List<ItemStack> _stacks = new();

    public IReadOnlyList<ItemStack> GetStacks() => _stacks;

    public void Clear() => _stacks.Clear();


    // Adds one item, merging into an existing stack only when kind, name, and magnitude all match. Items with differing
    // magnitudes (e.g., gold of different values) are kept in separate stacks so <see cref="GetGold"/> stays correct.
    public void Add(ItemKind kind, string name, int magnitude)
    {
        magnitude = Math.Max(1, magnitude);

        for (var i = 0; i < _stacks.Count; i++)
        {
            var stack = _stacks[i];
            if (stack.Kind == kind && stack.Name == name && stack.Magnitude == magnitude)
            {
                _stacks[i] = stack with { Count = stack.Count + 1 };
                return;
            }
        }

        _stacks.Add(new ItemStack(kind, name, 1, magnitude));
    }

    //>Appends a fully-formed stack (used when restoring a saved inventory).
    public void AddStack(ItemStack stack) => _stacks.Add(stack);

    public int GetGold() =>
        _stacks.Where(s => s.Kind == ItemKind.Gold).Sum(s => s.Count * s.Magnitude);

    //Index of the first potion stack that can be consumed, or -1.
    public int FindPotionIndex()
    {
        for (var i = 0; i < _stacks.Count; i++)
        {
            if (_stacks[i].Kind == ItemKind.Potion && _stacks[i].Count > 0)
            {
                return i;
            }
        }

        return -1;
    }

    public ItemStack GetStack(int index) => _stacks[index];

    // Decrements the stack at <paramref name="index"/> by one, removing it when empty.
    public void ConsumeOne(int index)
    {
        var stack = _stacks[index];
        if (stack.Count <= 1)
        {
            _stacks.RemoveAt(index);
        }
        else
        {
            _stacks[index] = stack with { Count = stack.Count - 1 };
        }
    }
}
