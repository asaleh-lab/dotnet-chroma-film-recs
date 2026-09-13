using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lantern.Core;

public sealed class Film
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

    [JsonPropertyName("minutes")]
    public required int Minutes { get; init; }

    [JsonPropertyName("playing_this_week")]
    public required bool PlayingThisWeek { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }
}

public sealed class FilmMetadata
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

    [JsonPropertyName("minutes")]
    public required int Minutes { get; init; }

    [JsonPropertyName("playing_this_week")]
    public required bool PlayingThisWeek { get; init; }

    public object? Field(string key) => key switch
    {
        "title" => Title,
        "language" => Language,
        "minutes" => Minutes,
        "playing_this_week" => PlayingThisWeek,
        _ => null
    };
}

public static class Films
{
    public static IReadOnlyList<Film> Load()
    {
        var raw = File.ReadAllText(RepoPaths.FilmsJson);
        var films = JsonSerializer.Deserialize<List<Film>>(raw);
        if (films is null || films.Count == 0)
        {
            throw new InvalidOperationException("data/films.json is empty or missing.");
        }

        return films;
    }

    public static string DocumentFor(Film film) => $"{film.Title}. {film.Description}";

    public static FilmMetadata MetadataFor(Film film) => new()
    {
        Title = film.Title,
        Language = film.Language,
        Minutes = film.Minutes,
        PlayingThisWeek = film.PlayingThisWeek
    };
}
