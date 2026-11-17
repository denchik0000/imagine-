namespace Imagine.Color;

/// <summary>
/// Линейный конгруэнтный генератор java.util.Random. Нужен, чтобы k-средние
/// стартовали так же, как в эталонной реализации Google, и давали те же цвета.
/// </summary>
internal sealed class JavaRandom(long seed)
{
    private const long Multiplier = 0x5DEECE66DL;
    private const long Mask = (1L << 48) - 1;
    private long _seed = (seed ^ Multiplier) & Mask;

    private int Next(int bits)
    {
        _seed = (_seed * Multiplier + 0xBL) & Mask;
        return (int)((ulong)_seed >> (48 - bits));
    }

    public int NextInt(int bound)
    {
        if (bound <= 0) throw new ArgumentOutOfRangeException(nameof(bound));
        if ((bound & -bound) == bound) return (int)((bound * (long)Next(31)) >> 31);
        int bits, val;
        do
        {
            bits = Next(31);
            val = bits % bound;
        }
        while (bits - val + (bound - 1) < 0);
        return val;
    }
}
