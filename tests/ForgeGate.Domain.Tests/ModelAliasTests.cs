using ForgeGate.Domain;

namespace ForgeGate.Domain.Tests;

public class ModelAliasTests
{
    [Fact]
    public void ModelAlias_CanBeCreatedWithValidValues()
    {
        var alias = new ModelAlias
        {
            Alias = "fast",
            Models = ["gpt-4o-mini", "claude-haiku"]
        };

        Assert.Equal("fast", alias.Alias);
        Assert.Equal(2, alias.Models.Count);
    }
}
