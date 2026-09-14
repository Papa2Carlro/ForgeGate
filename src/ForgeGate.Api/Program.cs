using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing;
using ForgeGate.Infrastructure.Providers.OpenAICompatible;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Configuration;

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
builder.Services.AddScoped<IRouteEligibilityEvaluator, HardRouteEligibilityEvaluator>();
builder.Services.AddScoped<IRouteResolver, ConfiguredRouteResolver>();
builder.Services.AddScoped<ChatExecutionService>();

var app = builder.Build();

// Configure Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map controllers
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
