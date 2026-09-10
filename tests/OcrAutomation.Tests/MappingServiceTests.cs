using OcrAutomation.Models;
using OcrAutomation.Services;

namespace OcrAutomation.Tests;

public class MappingServiceTests
{
    private static MappingService ServiceWith(params MappingRule[] rules)
    {
        var service = new MappingService();
        service.Rules.Rules.AddRange(rules);
        return service;
    }

    [Fact]
    public void ValidateRules_AcceptsValidRules()
    {
        var service = ServiceWith(new MappingRule
        {
            Name = "Greeting",
            Pattern = "^hello",
            Actions = new List<AutomationAction>()
        });

        Assert.Empty(service.ValidateRules());
    }

    [Fact]
    public void ValidateRules_FlagsInvalidRegex()
    {
        var service = ServiceWith(new MappingRule { Name = "Broken", Pattern = "([a-z" });

        var errors = service.ValidateRules();

        Assert.Single(errors);
        Assert.Contains("Broken", errors[0]);
    }

    [Fact]
    public void ValidateRules_FlagsMissingPatternAndName()
    {
        var service = ServiceWith(
            new MappingRule { Name = "NoPattern", Pattern = "" },
            new MappingRule { Name = "", Pattern = "^x" });

        var errors = service.ValidateRules();

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Contains("NoPattern"));
    }

    [Fact]
    public void MatchText_IgnoresInvalidRulesSafely()
    {
        var service = ServiceWith(
            new MappingRule { Name = "Broken", Pattern = "([a-z" },
            new MappingRule
            {
                Name = "Good",
                Pattern = "^hello",
                Actions = new List<AutomationAction>
                {
                    new() { Type = ActionType.Keystroke, Value = "hi" }
                }
            });

        var actions = service.MatchText("hello world");

        Assert.Single(actions);
    }

    [Fact]
    public void MatchText_ReturnsEmptyWhenNothingMatches()
    {
        var service = ServiceWith(new MappingRule { Name = "Good", Pattern = "^bye" });

        Assert.Empty(service.MatchText("hello world"));
    }
}
