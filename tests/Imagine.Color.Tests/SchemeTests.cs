namespace Imagine.Color.Tests;

public class SchemeTests
{
    private static ColorRole RoleFromName(string name) => Enum.Parse<ColorRole>(name, ignoreCase: true);

    private static Variant VariantFromName(string name) => Enum.Parse<Variant>(name.Replace("_", ""), ignoreCase: true);

    [Fact]
    public void All_roles_of_all_variants_match_reference()
    {
        var mismatches = new List<string>();
        int count = 0;
        foreach (var e in Golden.Root.GetProperty("schemes").EnumerateArray())
        {
            var scheme = new DynamicScheme(
                e.GetProperty("seed").GetUInt32(),
                VariantFromName(e.GetProperty("variant").GetString()!),
                e.GetProperty("dark").GetBoolean(),
                e.GetProperty("contrast").GetDouble());
            foreach (var role in e.GetProperty("roles").EnumerateObject())
            {
                count++;
                var expected = Golden.Hex(role.Value.GetUInt32());
                var actual = Golden.Hex(scheme.Get(RoleFromName(role.Name)));
                if (expected != actual) mismatches.Add($"{scheme} {role.Name}: ожидалось {expected}, получено {actual}");
            }
        }
        Assert.True(count > 5000);
        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches.Take(30)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Text_roles_have_accessible_contrast(bool dark)
    {
        var scheme = new DynamicScheme(0xFFD55E5D, Variant.TonalSpot, dark);
        Assert.True(Contrast.RatioOfArgb(scheme.Get(ColorRole.OnSurface), scheme.Get(ColorRole.Surface)) >= 4.5);
        Assert.True(Contrast.RatioOfArgb(scheme.Get(ColorRole.OnPrimary), scheme.Get(ColorRole.Primary)) >= 4.5);
        Assert.True(Contrast.RatioOfArgb(scheme.Get(ColorRole.OnPrimaryContainer), scheme.Get(ColorRole.PrimaryContainer)) >= 4.5);
    }

    [Fact]
    public void Higher_contrast_level_increases_contrast()
    {
        var normal = new DynamicScheme(0xFFD55E5D, Variant.TonalSpot, false, 0);
        var high = new DynamicScheme(0xFFD55E5D, Variant.TonalSpot, false, 1);
        double c0 = Contrast.RatioOfArgb(normal.Get(ColorRole.Primary), normal.Get(ColorRole.Surface));
        double c1 = Contrast.RatioOfArgb(high.Get(ColorRole.Primary), high.Get(ColorRole.Surface));
        Assert.True(c1 > c0);
    }
}
