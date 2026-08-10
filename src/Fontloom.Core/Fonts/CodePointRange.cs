namespace Fontloom.Core.Fonts;

public readonly record struct CodePointRange(uint Start, uint End)
{
    public bool Contains(uint codePoint)
        => codePoint >= Start && codePoint <= End;
}
