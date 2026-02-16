using StarRuptureSaveFixer.Fixers;

namespace StarRuptureSaveFixer.AvaloniaApp.ViewModels;

public sealed class FixerOption
{
    public string Name { get; }
    public Func<IFixer> CreateFixer { get; }

    public FixerOption(string name, Func<IFixer> createFixer)
    {
        Name = name;
        CreateFixer = createFixer;
    }

    public override string ToString() => Name;
}
