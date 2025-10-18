using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// EF Core
var conn = builder.Configuration.GetConnectionString("Sql");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(conn));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();

// Health
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/hello/{name}", (string name) =>
{
    return Results.Ok($"Hello, {name}!");
});


app.MapGet("/api/v1/users", async (AppDbContext db) =>
{
    var users = await db.Users.ToListAsync();
    return Results.Ok(users);
}).WithOpenApi();

app.MapGet("/api/v1/users/{id:int}", async (AppDbContext db, int id) =>
{
    var u = await db.Users.FindAsync(id);
    return u is null ? Results.NotFound() : Results.Ok(u);
}).WithOpenApi();

app.MapPost("/api/v1/users", async (AppDbContext db, UserDB dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Username)) return Results.BadRequest(new { error = "Name required" });
    var u = new User { Username = dto.Username, Email = dto.Email };
    db.Users.Add(u);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/users/{u.Id}", u);
}).WithOpenApi();

app.MapPut("/api/v1/users/{id:int}", async (AppDbContext db, int id, UserDB dto) =>
{
    var p = await db.Users.FindAsync(id);
    if (p is null) return Results.NotFound();
    p.Username = dto.Username; p.Email = dto.Email;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithOpenApi();

app.MapDelete("/api/v1/users/{id:int}", async (AppDbContext db, int id) =>
{
    var p = await db.Users.FindAsync(id);
    if (p is null) return Results.NotFound();
    db.Users.Remove(p);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithOpenApi();
app.Run();

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}



public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(100);
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
        });
    }
}

public partial class Program { }
public record UserDB(string Username, string Email);