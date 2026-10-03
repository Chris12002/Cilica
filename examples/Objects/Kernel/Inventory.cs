namespace Cilica.Examples.Objects
{

// Independent application logic: no compiler/runtime references or host APIs.
public static class Inventory
{
    public static int Run(int turns, int selected, int mode)
    {
        // The sample deliberately caps its workload; this is not a compiler limit.
        if (turns < 0 || turns > 12) return -1;
        var state = new InventoryState();
        state.Items[0] = new Item("ore", 2);
        state.Items[1] = new BonusItem("gem", 5, 3);
        state.Items[2] = new Item("wood", 1);
        if (mode == 1) state.Items[1] = null!;
        if (mode == 2) state.Totals[0] = int.MaxValue;
        for (int turn = 0; turn < turns; turn++)
        {
            int slot = turn % state.Items.Length;
            Item item = At(state.Items, slot);
            IItem action = item;
            string label = Name(action);
            state.Labels[slot] = label;
            state.Transcript = string.Concat(state.Transcript, label);
            state.Totals[slot] = Add(state.Totals[slot], action.Apply(turn));
            // The same receiver also uses class virtual dispatch.
            if (item.Name() != label) return -2;
        }
        Item chosen = At(state.Items, selected);
        object candidate = chosen;
        BonusItem? bonus = candidate as BonusItem;
        int result = bonus == null ? 0 : bonus.Bonus;
        object[] labels = state.Labels; // Covariant view preserves actual String[] storage.
        labels[selected] = chosen.Name();
        string selectedLabel = (string)labels[selected];
        if (selectedLabel == "gem!") result = Add(result, 100);
        for (int i = 0; i < state.Totals.Length; i++) result = Add(result, state.Totals[i]);
        // Include every UTF-16 unit in the result, making transcript order observable.
        for (int i = 0; i < state.Transcript.Length; i++) result = unchecked(result * 31 + state.Transcript[i]);
        return Add(result, selectedLabel.Length);
    }

    // Small expression bodies give independently verifiable fault origins.
    public static Item At(Item[] values, int index) => values[index];
    public static int Add(int left, int right) => checked(left + right);
    public static string Name(IItem item) => item.Name();
}

public sealed class InventoryState
{
    public Item[] Items = new Item[3];
    public int[] Totals = new int[3];
    public string[] Labels = new string[3];
    public string Transcript = "";
}

public interface IItem
{
    string Name();
    int Apply(int turn);
}

public class Item : IItem
{
    public string Title;
    public int Score;
    public Item(string title, int score) { Title = title; Score = score; }
    public virtual string Name() => Title;
    public virtual int Apply(int turn) => checked(Score + turn);
}

public sealed class BonusItem : Item
{
    public int Bonus;
    public BonusItem(string title, int score, int bonus) : base(title, score) { Bonus = bonus; }
    public override string Name() => string.Concat(Title, "!");
    public override int Apply(int turn) => checked(base.Apply(turn) + Bonus);
}
}
