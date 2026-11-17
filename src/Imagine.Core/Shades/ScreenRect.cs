namespace Imagine.Core.Shades;

/// <summary>Прямоугольник в пикселях экрана (правая и нижняя границы не входят).</summary>
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}
