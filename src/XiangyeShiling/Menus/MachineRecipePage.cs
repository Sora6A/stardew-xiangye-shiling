namespace XiangyeShiling.Menus;

/// <summary>Maps the two on-screen recipe rows to their positions in the ordered catalog.</summary>
internal readonly record struct MachineRecipePage(int RecipeCount, int Index = 0)
{
    private const int Rows = 2;

    public int Count => 1 + Math.Max(0, RecipeCount - 1) / Rows;
    public int FirstRecipe => Index * Rows;
    public int VisibleCount => Math.Min(Rows, RecipeCount - this.FirstRecipe);
    public MachineRecipePage Move(int direction) => this with { Index = Math.Clamp(Index + direction, 0, this.Count - 1) };
}
