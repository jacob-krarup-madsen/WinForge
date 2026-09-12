namespace WingetStore.Services;

public readonly record struct GridDimensions(
    int Columns,
    double SlotWidth,
    double EffectiveGap)
{
    public double CardWidth => Math.Max(0, SlotWidth - EffectiveGap);
}

public static class GridCalculator
{
    public static GridDimensions CalculateGridDimensions(
        double usableWidth, 
        double minCardWidth = 300, 
        double gap = 16, 
        int maxColumns = 5)
    {
        if (!double.IsFinite(minCardWidth) || minCardWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(minCardWidth), "Card width must be > 0.");
        if (!double.IsFinite(gap) || gap < 0)
            throw new ArgumentOutOfRangeException(nameof(gap), "Gap cannot be negative.");
        if (maxColumns <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxColumns), "Max columns must be > 0.");

        if (!double.IsFinite(usableWidth) || usableWidth <= 0)
            return new GridDimensions(1, 0, 0);

        double minSlotWidth = minCardWidth + gap; // 316 DIPs
        int columns = Math.Clamp((int)Math.Floor(usableWidth / minSlotWidth), 1, maxColumns);
        double slotWidth = usableWidth / columns;
        double effectiveGap = columns == 1 ? 0 : gap;

        return new GridDimensions(columns, slotWidth, effectiveGap);
    }
}
