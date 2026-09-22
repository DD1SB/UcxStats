using System.Text;

namespace UcxStats.Core.Ucx;

public sealed record UcxParseOptions
{
    public bool StrictStringLengths { get; init; } = true;
    public Encoding TextEncoding { get; init; } = Encoding.Latin1;
}
