using Lantern.Core;

const string Query = "something quiet after a long day";
const string CollectionName = "lantern_films";

var apiKey = Env.RequireOpenAiKey();
var model = Env.OpenAiModel();
var films = Films.Load();
var client = new PersistentClient(apiKey);

if (client.ListCollections().Any(collection => collection.Name == CollectionName))
{
    client.DeleteCollection(CollectionName);
}

var collection = client.CreateCollection(
    CollectionName,
    new Dictionary<string, string> { ["hnsw:space"] = "cosine" });

await collection.AddAsync(
    films.Select(film => film.Id).ToList(),
    films.Select(Films.DocumentFor).ToList(),
    films.Select(Films.MetadataFor).ToList());

var where = Where.And(
    Where.Eq("playing_this_week", true),
    Where.Lt("minutes", 110),
    Where.Value("language", "en"));
var hits = await collection.QueryAsync([Query], nResults: 2, where);
var documents = hits.Documents[0];
var metadatas = hits.Metadatas[0];

Console.WriteLine($"Query: {Query}");
Console.WriteLine("Retrieved:");
for (var index = 0; index < documents.Count; index++)
{
    var meta = metadatas[index];
    Console.WriteLine($"- {meta.Title} ({meta.Minutes} min)");
    Console.WriteLine($"  {documents[index]}");
}

var context = string.Join("\n\n", documents);
const string SystemPrompt =
    "Answer from these films only. If none of them fit the need, say so. " +
    "Name the film you would put on tonight.";
var user = $"Films:\n{context}\n\nNeed: {Query}";

Console.WriteLine("\nFilled prompt:");
Console.WriteLine($"system: {SystemPrompt}");
Console.WriteLine($"user: {user}");
Console.WriteLine("---");

var answer = await LanternChat.CompleteAsync(apiKey, model, SystemPrompt, user);
Console.WriteLine(answer);
