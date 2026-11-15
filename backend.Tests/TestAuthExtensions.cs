using System.Net.Http.Json;
using PhotoLibApi.Models;

namespace backend.Tests;

/// <summary>
/// Signs a test <see cref="HttpClient"/> in through the real
/// <c>/api/Auth/register</c> endpoint (not a shortcut/mock), so every test
/// exercises the actual password-hashing and cookie-issuing path. The
/// returned client carries the session cookie for all subsequent requests.
/// </summary>
public static class TestAuthExtensions
{
    private static int _counter;

    /// <summary>
    /// Registers a brand-new, uniquely-named account and returns the now
    /// signed-in client along with the created user's id and email.
    /// </summary>
    public static async Task<(HttpClient Client, Guid UserId, string Email)> RegisterAndSignInAsync(
        this TestingWebApplicationFactory factory, string? namePrefix = null)
    {
        var client = factory.CreateClient();
        var unique = Interlocked.Increment(ref _counter);
        var email = $"{namePrefix ?? "user"}{unique}@example.test";

        var response = await client.PostAsJsonAsync("/api/Auth/register", new RegisterRequest
        {
            Email = email,
            Password = "correct-horse-battery-staple",
            Name = namePrefix ?? $"Test User {unique}",
        });
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<UserResponse>(TestJson.CaseInsensitive);
        return (client, user!.Id, email);
    }

    private class UserResponse
    {
        public Guid Id { get; set; }
    }
}
