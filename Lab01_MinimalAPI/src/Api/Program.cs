using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();



var app = builder.Build();

app.UseAuthorization();

// Health
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/hello/{name}", (string name) =>
{
    return Results.Ok($"Hello, {name}!");
});

app.Run();
 class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
public partial class Program { } 