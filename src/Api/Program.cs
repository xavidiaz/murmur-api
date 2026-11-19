using Api.Auth;
using Api.Endpoints;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddJwtAuthentication();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("MIGRATE_ON_START"))
    await app.Services.MigrateDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapServerEndpoints();
app.MapInviteEndpoints();

app.Run();

public partial class Program;
