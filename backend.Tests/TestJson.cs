using System.Text.Json;

namespace backend.Tests;

/// <summary>
/// The backend serializes response bodies with camelCase/lowercase field
/// names (ASP.NET Core's MVC default), while test DTOs use PascalCase C#
/// properties - case-insensitive deserialization bridges the two without
/// needing a <c>[JsonPropertyName]</c> on every test-only DTO.
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions CaseInsensitive =
        new() { PropertyNameCaseInsensitive = true };
}
