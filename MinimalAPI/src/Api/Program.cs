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
using System.ComponentModel.DataAnnotations;
using Serilog;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Api",
        Version = "v1"
    });
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
// AutoMapper configuration
builder.Services.AddAutoMapper(typeof(Program));

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

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


app.MapGet("/users", async (AppDbContext db, IMapper mapper) =>
{
    var users = await db.Users.ToListAsync();
    var dtos = mapper.Map<List<UserDto>>(users);
    return Results.Ok(dtos);
}).WithOpenApi();

app.MapGet("/users/{id:int}", async (AppDbContext db, int id, IMapper mapper) =>
{
    var u = await db.Users.FindAsync(id);
    if (u is null) return Results.NotFound();
    var dto = mapper.Map<UserDto>(u);
    return Results.Ok(dto);
}).RequireAuthorization().WithOpenApi();
app.MapPost("/users", async (AppDbContext db, UserDB dto) =>
{
    try {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password)) {
            Console.Error.WriteLine($"[POST /users] BadRequest: Username and password required");
            return Results.BadRequest(new { error = "Username and password required" });
        }
        var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        var u = new User { Username = dto.Username, Email = dto.Email, PasswordHash = hash, Created = DateTime.UtcNow };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return Results.NoContent();
    } catch (Exception ex) {
        Console.Error.WriteLine($"[POST /users] Exception: {ex.Message}\n{ex.StackTrace}");
        return Results.Problem("Internal server error");
    }
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
    try {
        if (string.IsNullOrWhiteSpace(dto.Title)) {
            Console.Error.WriteLine($"[POST /tasks] BadRequest: Title required");
            return Results.BadRequest(new { error = "Title required" });
        }
        var user = await db.Users.FindAsync(dto.UserId);
        if (user is null) {
            Console.Error.WriteLine($"[POST /tasks] BadRequest: User not found");
            return Results.BadRequest(new { error = "User not found" });
        }
        var task = new TodoTask { Title = dto.Title, Description = dto.Description ?? string.Empty, UserId = dto.UserId, IsCompleted = false };
        db.TodoTasks.Add(task);
        await db.SaveChangesAsync();
        return Results.Created($"/tasks/{task.Id}", task);
    } catch (Exception ex) {
        Console.Error.WriteLine($"[POST /tasks] Exception: {ex.Message}\n{ex.StackTrace}");
        return Results.Problem("Internal server error");
    }
}).RequireAuthorization().WithOpenApi();
app.MapGet("/users/{id:int}/tasks", async (AppDbContext db, int id) =>
{
    try {
        var userExists = await db.Users.AnyAsync(u => u.Id == id);
        if (!userExists) {
            Console.Error.WriteLine($"[GET /users/{id}/tasks] NotFound: User not found");
            return Results.NotFound();
        }
        var tasks = await db.TodoTasks.Where(t => t.UserId == id).ToListAsync();
        return Results.Ok(tasks);
    } catch (Exception ex) {
        Console.Error.WriteLine($"[GET /users/{id}/tasks] Exception: {ex.Message}\n{ex.StackTrace}");
        return Results.Problem("Internal server error");
    }
}).RequireAuthorization().WithOpenApi();



app.MapPost("/login", async (AppDbContext db, UserLogin dto, IConfiguration config) =>
{
    try {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
        if (user is null) {
            Console.Error.WriteLine($"[POST /login] BadRequest: Invalid username or password");
            return Results.BadRequest(new { error = "Invalid username or password" });
        }
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash)) {
            Console.Error.WriteLine($"[POST /login] BadRequest: Invalid username or password");
            return Results.BadRequest(new { error = "Invalid username or password" });
        }
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
    } catch (Exception ex) {
        Console.Error.WriteLine($"[POST /login] Exception: {ex.Message}\n{ex.StackTrace}");
        return Results.Problem("Internal server error");
    }
}).WithOpenApi();

app.MapGet("/reports/new-users", async (AppDbContext db, DateTime? from, DateTime? to, IMapper mapper) =>
{
    // Default: last 7 days if not provided
    var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
    var toDate = to ?? DateTime.UtcNow;
    var users = await db.Users
        .Where(u => EF.Property<DateTime>(u, "Created") >= fromDate && EF.Property<DateTime>(u, "Created") <= toDate)
        .ToListAsync();
    var dtos = mapper.Map<List<UserDto>>(users);
    return Results.Ok(new { from = fromDate, to = toDate, count = dtos.Count, users = dtos });
}).WithOpenApi();

// Example versioned endpoint
app.MapGet("/v{version:apiVersion}/hello/{name}", (string name) => Results.Ok($"Hello, {name}!")).WithMetadata(new ApiVersionAttribute("1.0"));

app.Run();

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
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
            e.Property(x => x.Created).IsRequired();
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
public record UserDto(int Id, string Username, string Email);

public class MappingProfile : AutoMapper.Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>();
    }
}
