using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
public class BasicTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public BasicTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Register_ReturnsNoContent()
    {
        var client = _factory.CreateClient();
        var payload = new StringContent("{\"username\":\"testuser\",\"email\":\"testuser@example.com\",\"password\":\"Test123!\"}", System.Text.Encoding.UTF8, "application/json");
        var res = await client.PostAsync("/users", payload);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Login_And_AccessProtectedResource()
    {
        var client = _factory.CreateClient();
        // Register user
        var payload = new StringContent("{\"username\":\"loginuser\",\"email\":\"loginuser@example.com\",\"password\":\"Test123!\"}", System.Text.Encoding.UTF8, "application/json");
        var regRes = await client.PostAsync("/users", payload);
        Assert.Equal(HttpStatusCode.NoContent, regRes.StatusCode);
        // Login
        var loginPayload = new StringContent("{\"username\":\"loginuser\",\"password\":\"Test123!\"}", System.Text.Encoding.UTF8, "application/json");
        var loginRes = await client.PostAsync("/login", loginPayload);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var json = await loginRes.Content.ReadAsStringAsync();
        var token = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrEmpty(token));
        // Access protected resource
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var protectedRes = await client.GetAsync("/users/1");
        Assert.True(protectedRes.StatusCode == HttpStatusCode.OK || protectedRes.StatusCode == HttpStatusCode.NotFound);
    }

    // Seeding users in DbContext for integration tests
    public class TestDbContext : AppDbContext
    {
        public TestDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().HasData(
                new User { Id = 100, Username = "seededuser", Email = "seeded@example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Seeded123!"), Created = DateTime.UtcNow }
            );
        }
    }
}