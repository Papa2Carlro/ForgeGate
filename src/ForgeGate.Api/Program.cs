using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Execution;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;
using ForgeGate.Infrastructure.Providers.OpenAICompatible;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HTTP client for provider
builder.Services.AddHttpClient<OpenAIChatCompletionProvider>();

// Configure routing
builder.Services.Configure<RoutingConfiguration>(builder.Configuration.GetSection("Routing"));

// Add Application services
builder.Services.AddScoped<IChatCompletionProvider, OpenAIChatCompletionProvider>();
builder.Services.AddSingleton<IRouteHealthStateProvider, InMemoryRouteHealthStateProvider>();
builder.Services.AddScoped<IRouteHealthFeedback, RouteHealthFeedback>();
builder.Services.AddScoped<IRouteEligibilityEvaluator, RouteOperationalEligibilityEvaluator>(sp =>
    new RouteOperationalEligibilityEvaluator(
        new HardRouteEligibilityEvaluator(),
        sp.GetRequiredService<IRouteHealthStateProvider>()));
builder.Services.AddScoped<IRouteHealthRanker, RouteHealthRanker>();
builder.Services.AddSingleton<IRouteCapacityCoordinator, InMemoryRouteCapacityCoordinator>();
builder.Services.AddSingleton<IRouteCapacityStateProvider>(sp => (IRouteCapacityStateProvider)sp.GetRequiredService<IRouteCapacityCoordinator>());
builder.Services.AddScoped<IRouteResolver, ConfiguredRouteResolver>(sp =>
    new ConfiguredRouteResolver(
        sp.GetRequiredService<IOptions<RoutingConfiguration>>(),
        sp.GetRequiredService<IRouteEligibilityEvaluator>(),
        sp.GetRequiredService<IRouteHealthRanker>(),
        sp.GetRequiredService<IRouteCapacityStateProvider>()));
builder.Services.AddScoped<ChatExecutionService>();
builder.Services.AddScoped<IChatCompletionOrchestrator, ChatCompletionOrchestrator>();
builder.Services.AddScoped<IStreamingChatCompletionOrchestrator, StreamingChatCompletionOrchestrator>();
builder.Services.AddSingleton<ICapabilityRegistry, InMemoryCapabilityRegistry>();
builder.Services.AddScoped<IActionIntentNormalizer, BasicActionIntentNormalizer>();
builder.Services.AddScoped<ICapabilityTranslator, BasicCapabilityTranslator>();
builder.Services.AddScoped<IPolicyEvaluator, BasicPolicyEvaluator>();
builder.Services.AddScoped<ILayer3Evaluator, BasicLayer3Evaluator>();
builder.Services.AddScoped<IAgentGuard, AgentGuardService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

// Map controllers
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
