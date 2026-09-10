using System.Text;

namespace OcrAutomation.Utilities;

public readonly record struct KeyStroke(ushort VirtualKey, bool Extended = false);

public static class KeySequenceParser
{
    private static readonly IReadOnlyDictionary<string, KeyStroke> Keys = new Dictionary<string, KeyStroke>(StringComparer.OrdinalIgnoreCase)
    {
        ["ENTER"] = new(0x0D), ["RETURN"] = new(0x0D), ["TAB"] = new(0x09), ["ESC"] = new(0x1B), ["ESCAPE"] = new(0x1B),
        ["BACKSPACE"] = new(0x08), ["BS"] = new(0x08), ["SPACE"] = new(0x20), ["DELETE"] = new(0x2E), ["DEL"] = new(0x2E),
        ["INSERT"] = new(0x2D), ["HOME"] = new(0x24), ["END"] = new(0x23), ["PAGEUP"] = new(0x21), ["PAGEDOWN"] = new(0x22),
        ["LEFT"] = new(0x25, true), ["RIGHT"] = new(0x27, true), ["UP"] = new(0x26, true), ["DOWN"] = new(0x28, true),
        ["CTRL"] = new(0x11), ["CONTROL"] = new(0x11), ["ALT"] = new(0x12), ["SHIFT"] = new(0x10), ["WIN"] = new(0x5B, true),
        ["F1"] = new(0x70), ["F2"] = new(0x71), ["F3"] = new(0x72), ["F4"] = new(0x73), ["F5"] = new(0x74), ["F6"] = new(0x75),
        ["F7"] = new(0x76), ["F8"] = new(0x77), ["F9"] = new(0x78), ["F10"] = new(0x79), ["F11"] = new(0x7A), ["F12"] = new(0x7B)
    };

    public static IReadOnlyList<KeyStroke> Parse(string value)
    {
        var result = new List<KeyStroke>();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '{') { result.Add(new KeyStroke(0, false)); continue; }
            var end = value.IndexOf('}', i + 1);
            if (end < 0) throw new FormatException("Unclosed key token.");
            var token = value[(i + 1)..end].Trim();
            if (token.Length == 0) throw new FormatException("Empty key token.");
            foreach (var part in token.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!TryResolve(part, out var stroke))
                    throw new FormatException($"Unsupported key token: {{{part}}}.");

                result.Add(stroke);
            }
            i = end;
        }
        return result;
    }

    public static bool IsTokenized(string value) => value.Contains('{');

    public static bool TryResolve(string token, out KeyStroke stroke)
    {
        var normalized = token.Trim();
        if (Keys.TryGetValue(normalized, out stroke))
            return true;

        if (normalized.Length == 1)
        {
            var character = char.ToUpperInvariant(normalized[0]);
            if (character is >= 'A' and <= 'Z')
            {
                stroke = new KeyStroke(character);
                return true;
            }

            if (character is >= '0' and <= '9')
            {
                stroke = new KeyStroke(character);
                return true;
            }
        }

        stroke = default;
        return false;
    }
}
