using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Prevent JSON serializer from failing on circular navigation properties
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
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


app.MapGet("/users", async (AppDbContext db) =>
{
    var users = await db.Users.ToListAsync();
    return Results.Ok(users);
}).WithOpenApi();

app.MapGet("/users/{id:int}", async (AppDbContext db, int id) =>
{
    var u = await db.Users.FindAsync(id);
    return u is null ? Results.NotFound() : Results.Ok(u);
}).WithOpenApi();

app.MapPost("/users", async (AppDbContext db, UserDB dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Username)) return Results.BadRequest(new { error = "Name required" });
    var u = new User { Username = dto.Username, Email = dto.Email };
    db.Users.Add(u);
    await db.SaveChangesAsync();
    return Results.Created($"/users/{u.Id}", u);
}).WithOpenApi();

app.MapPut("/users/{id:int}", async (AppDbContext db, int id, UserDB dto) =>
{
    var u = await db.Users.FindAsync(id);
    if (u is null) return Results.NotFound();
    u.Username = dto.Username; u.Email = dto.Email;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithOpenApi();

app.MapDelete("/users/{id:int}", async (AppDbContext db, int id) =>
{
    var u = await db.Users.FindAsync(id);
    if (u is null) return Results.NotFound();
    db.Users.Remove(u);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithOpenApi();

app.MapPost("/tasks", async (AppDbContext db, TodoTaskCreate dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Title)) return Results.BadRequest(new { error = "Title required" });
    var user = await db.Users.FindAsync(dto.UserId);
    if (user is null) return Results.BadRequest(new { error = "User not found" });
    var task = new TodoTask { Title = dto.Title, Description = dto.Description ?? string.Empty, UserId = dto.UserId, IsCompleted = false };
    db.TodoTasks.Add(task);
    await db.SaveChangesAsync();
    return Results.Created($"/tasks/{task.Id}", task);
}).WithOpenApi();

app.Run();

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<TodoTask> Tasks { get; set; } = new();
}



public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
    public DbSet<TodoTask> TodoTasks => Set<TodoTask>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(100);
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<TodoTask>(e =>
        {
            e.ToTable("Tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.IsCompleted).IsRequired();
            e.HasOne(x => x.User)
             .WithMany(u => u.Tasks)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public partial class Program { }
public record UserDB(string Username, string Email);

public class TodoTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int UserId { get; set; }
    [JsonIgnore]
    public User? User { get; set; }
}

public record TodoTaskCreate(string Title, string? Description, int UserId);