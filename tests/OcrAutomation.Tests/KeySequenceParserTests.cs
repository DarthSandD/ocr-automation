using OcrAutomation.Utilities;

namespace OcrAutomation.Tests;

public class KeySequenceParserTests
{
    [Fact]
    public void ParsesModifierCombination()
    {
        var keys = KeySequenceParser.Parse("{CTRL+Z}");
        Assert.Equal(2, keys.Count);
        Assert.Equal((ushort)0x11, keys[0].VirtualKey);
        Assert.Equal((ushort)0x5A, keys[1].VirtualKey);
    }

    [Fact]
    public void RejectsUnknownToken()
    {
        Assert.Throws<FormatException>(() => KeySequenceParser.Parse("{NOT_A_KEY}"));
    }

    [Fact]
    public void RejectsUnclosedToken()
    {
        Assert.Throws<FormatException>(() => KeySequenceParser.Parse("{ENTER"));
    }
}
