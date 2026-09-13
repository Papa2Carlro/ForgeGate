using ForgeGate.Application.Chat;
using ForgeGate.Infrastructure.Providers.OpenAICompatible;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HTTP client for provider
builder.Services.AddHttpClient<OpenAIChatCompletionProvider>();

// Add Application services
builder.Services.AddScoped<IChatCompletionProvider, OpenAIChatCompletionProvider>();
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
