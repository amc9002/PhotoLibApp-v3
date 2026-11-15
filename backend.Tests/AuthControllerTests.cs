using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoLibApi.Data;
using PhotoLibApi.Models;

namespace backend.Tests;

/// <summary>
/// Covers sign-in/sign-out, self-service registration, password change,
/// and the admin-key-gated legacy-data-claiming path - the highest-priority
/// area per the testing-foundation proposal, since every other endpoint's
/// security rests on this working correctly.
/// </summary>
public class AuthControllerTests
{
    [Fact]
    public async Task Register_CreatesAccountAndSignsInImmediately()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, userId, email) = await factory.RegisterAndSignInAsync();

        Assert.NotEqual(Guid.Empty, userId);

        var me = await client.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var body = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        using var factory = new TestingWebApplicationFactory();
        var (_, _, email) = await factory.RegisterAndSignInAsync();

        var second = factory.CreateClient();
        var response = await second.PostAsJsonAsync("/api/Auth/register", new RegisterRequest
        {
            Email = email,
            Password = "another-strong-password",
            Name = "Someone Else",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "a-valid-password", "Name")]
    [InlineData("valid@example.test", "short", "Name")]
    [InlineData("valid@example.test", "a-valid-password", "")]
    public async Task Register_InvalidRequest_ReturnsBadRequest(string email, string password, string name)
    {
        using var factory = new TestingWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            Name = name,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_CorrectCredentials_SignsIn()
    {
        using var factory = new TestingWebApplicationFactory();
        var (_, _, email) = await factory.RegisterAndSignInAsync();

        var freshClient = factory.CreateClient();
        var response = await freshClient.PostAsJsonAsync("/api/Auth/login", new LoginRequest
        {
            Email = email,
            Password = "correct-horse-battery-staple",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await freshClient.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorizedWithGenericMessage()
    {
        using var factory = new TestingWebApplicationFactory();
        var (_, _, email) = await factory.RegisterAndSignInAsync();

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/Auth/login", new LoginRequest
        {
            Email = email,
            Password = "totally-wrong-password",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid email or password.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Login_NonexistentEmail_ReturnsSameMessageAsWrongPassword()
    {
        // Regression guard for email enumeration: a nonexistent account and a
        // wrong password must be indistinguishable to the caller.
        using var factory = new TestingWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/login", new LoginRequest
        {
            Email = "nobody-registered@example.test",
            Password = "whatever",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid email or password.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Me_WithoutSession_ReturnsUnauthorized()
    {
        using var factory = new TestingWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();

        var logout = await client.PostAsync("/api/Auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var me = await client.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsForbiddenNotUnauthorized()
    {
        // 403, not 401: the frontend interceptor relies on this distinction
        // to decide whether to sign the user out entirely.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();

        var response = await client.PostAsJsonAsync("/api/Auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "wrong-current-password",
            NewPassword = "a-brand-new-password",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_CorrectCurrentPassword_AllowsLoginWithNewPassword()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, email) = await factory.RegisterAndSignInAsync();

        var change = await client.PostAsJsonAsync("/api/Auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "correct-horse-battery-staple",
            NewPassword = "a-brand-new-password",
        });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var freshClient = factory.CreateClient();
        var login = await freshClient.PostAsJsonAsync("/api/Auth/login", new LoginRequest
        {
            Email = email,
            Password = "a-brand-new-password",
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutSession_ReturnsUnauthorized()
    {
        using var factory = new TestingWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "whatever",
            NewPassword = "a-brand-new-password",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminRegister_WithoutAdminKey_ReturnsForbidden()
    {
        using var factory = new TestingWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/admin/register", new AdminRegisterRequest
        {
            Email = "owner@example.test",
            Password = "a-valid-password",
            Name = "Owner",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminRegister_WithWrongAdminKey_ReturnsForbidden()
    {
        using var factory = new TestingWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", "not-the-real-key");

        var response = await client.PostAsJsonAsync("/api/Auth/admin/register", new AdminRegisterRequest
        {
            Email = "owner@example.test",
            Password = "a-valid-password",
            Name = "Owner",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminRegister_FirstEverAccount_ClaimsPreExistingOrphanedGalleries()
    {
        using var factory = new TestingWebApplicationFactory();

        var orphanedGalleryId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
            db.Galleries.Add(new Gallery
            {
                Id = orphanedGalleryId,
                Title = "Pre-auth legacy gallery",
                OwnerId = null,
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", "test-admin-key");

        var response = await client.PostAsJsonAsync("/api/Auth/admin/register", new AdminRegisterRequest
        {
            Email = "owner@example.test",
            Password = "a-valid-password",
            Name = "Owner",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
            var gallery = await db.Galleries.FindAsync(orphanedGalleryId);
            Assert.NotNull(gallery!.OwnerId);
        }
    }

    [Fact]
    public async Task AdminRegister_SecondAccount_DoesNotClaimAnyonesExistingGalleries()
    {
        using var factory = new TestingWebApplicationFactory();
        var (_, firstUserId, _) = await factory.RegisterAndSignInAsync();

        var ownedByFirstUser = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
            db.Galleries.Add(new Gallery
            {
                Id = ownedByFirstUser,
                Title = "First user's gallery",
                OwnerId = firstUserId,
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", "test-admin-key");
        var response = await client.PostAsJsonAsync("/api/Auth/admin/register", new AdminRegisterRequest
        {
            Email = "second-admin@example.test",
            Password = "a-valid-password",
            Name = "Second Admin",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
            var gallery = await db.Galleries.FindAsync(ownedByFirstUser);
            Assert.Equal(firstUserId, gallery!.OwnerId);
        }
    }

    [Fact]
    public async Task Login_IsRateLimited_AfterRepeatedFailures()
    {
        // AuthLogin policy: 10 requests / 15 min / IP (Program.cs). The
        // TestServer's RemoteIpAddress is constant across requests from the
        // same client, so the 11th attempt in this window must be throttled.
        using var factory = new TestingWebApplicationFactory();
        var client = factory.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
        {
            last = await client.PostAsJsonAsync("/api/Auth/login", new LoginRequest
            {
                Email = "nobody@example.test",
                Password = "wrong",
            });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }
}
