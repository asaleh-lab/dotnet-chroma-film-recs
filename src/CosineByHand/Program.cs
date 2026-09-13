using Lantern.Core;

const string Query = "something quiet after a long day";

var apiKey = Env.RequireOpenAiKey();
var films = Films.Load().ToDictionary(film => film.Id);
if (!films.TryGetValue("lost-in-translation", out var lost)
    || !films.TryGetValue("independence-day", out var independence))
{
    throw new InvalidOperationException("data/films.json is missing Lost in Translation or Independence Day.");
}

var embedder = new Embedder(apiKey);
var vectors = await embedder.EmbedAsync(
    [Query, Films.DocumentFor(lost), Films.DocumentFor(independence)]);

Console.WriteLine($"Query: {Query}");
Console.WriteLine($"Lost in Translation: {Cosine.Score(vectors[0], vectors[1]):F4}");
Console.WriteLine($"Independence Day: {Cosine.Score(vectors[0], vectors[2]):F4}");
