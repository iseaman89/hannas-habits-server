using HannasHabits.Application;
using HannasHabits.Infrastructure;
using HannasHabits.WebApi;

var builder = WebApplication.CreateBuilder(args);

// Layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddWebApi(builder.Configuration);

var app = builder.Build();

// First, so it also catches exceptions thrown by everything below.
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Before authentication, so CORS preflight (OPTIONS) requests are answered without a token.
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Makes the top-level Program visible to WebApplicationFactory<Program> in the integration tests.
public partial class Program;
