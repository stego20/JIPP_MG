using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Api",
        Version = "v1"
    });

    // 🔒 Dodajemy obsługę autoryzacji JWT
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Wpisz: **Bearer <twój_token>**"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});;
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
var conn = builder.Configuration.GetConnectionString("Sql");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(conn));
// JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev_key_change_me";
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();


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
}).RequireAuthorization().WithOpenApi();
//Rejestracja uzytkownika
app.MapPost("/users", async (AppDbContext db, UserDB dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        return Results.BadRequest(new { error = "Username and password required" });
    // Hash password before storing
    var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
    var u = new User { Username = dto.Username, Email = dto.Email, PasswordHash = hash };
    db.Users.Add(u);
    await db.SaveChangesAsync();
    return Results.NoContent();

}).WithOpenApi();

app.MapPut("/users/{id:int}", async (AppDbContext db, int id, UserDB dto) =>
{
    var u = await db.Users.FindAsync(id);
    if (u is null) return Results.NotFound();
    u.Username = dto.Username; u.Email = dto.Email;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization().WithOpenApi();

app.MapDelete("/users/{id:int}", async (AppDbContext db, int id) =>
{
    var u = await db.Users.FindAsync(id);
    if (u is null) return Results.NotFound();
    db.Users.Remove(u);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization().WithOpenApi();

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
app.MapGet("/users/{id:int}/tasks", async (AppDbContext db, int id) =>
{
    var userExists = await db.Users.AnyAsync(u => u.Id == id);
    if (!userExists) return Results.NotFound();

    var tasks = await db.TodoTasks.Where(t => t.UserId == id).ToListAsync();
    return Results.Ok(tasks);
}).RequireAuthorization().WithOpenApi();



app.MapPost("/login", async (AppDbContext db, UserLogin dto, IConfiguration config) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
    if (user is null) return Results.BadRequest(new { error = "Invalid username or password" });

    // Verify password
    if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        return Results.BadRequest(new { error = "Invalid username or password" });

    // Generate JWT
    var jwtSettings = config.GetSection("Jwt");
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
    };

    var token = new JwtSecurityToken(
        issuer: jwtSettings["Issuer"],
        audience: jwtSettings["Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddHours(2),
        signingCredentials: creds
    );

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(new { token = tokenString });
}).WithOpenApi();

app.Run();

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
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
            e.Property(x => x.PasswordHash).HasMaxLength(1000);
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
public record UserDB(string Username, string Email, string Password);

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
public record UserLogin(string Username, string Password);
