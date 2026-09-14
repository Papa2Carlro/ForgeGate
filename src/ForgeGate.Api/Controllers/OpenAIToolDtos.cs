namespace ForgeGate.Api.Controllers;

public sealed class OpenAITool
{
    public string Type { get; init; } = "function";
    public OpenAIToolFunction Function { get; init; } = new();
}

public sealed class OpenAIToolFunction
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class OpenAIToolChoice
{
    public string Type { get; init; } = "function";
    public OpenAIToolChoiceFunction Function { get; init; } = new();
}

public sealed class OpenAIToolChoiceFunction
{
    public string Name { get; init; } = string.Empty;
}
