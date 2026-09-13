using Lantern.Core;

const string Query = "something quiet after a long day";
const string CollectionName = "lantern_films";

static void Show(string heading, QueryResult results)
{
    Console.WriteLine($"\n{heading}");
    var ids = results.Ids[0];
    var documents = results.Documents[0];
    var distances = results.Distances[0];
    var metadatas = results.Metadatas[0];
    for (var index = 0; index < ids.Count; index++)
    {
        var meta = metadatas[index];
        Console.WriteLine(
            $"{distances[index]:F4}  {ids[index]}  {meta.Language}  " +
            $"{meta.Minutes} min  this week={meta.PlayingThisWeek}");
        Console.WriteLine($"  {documents[index]}");
    }
}

var apiKey = Env.RequireOpenAiKey();
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

Console.WriteLine($"Stored {collection.Count()} films");
Console.WriteLine($"Query: {Query}");

Show("Meaning only", await collection.QueryAsync([Query], nResults: 3));

var where = Where.And(
    Where.Eq("playing_this_week", true),
    Where.Lt("minutes", 110),
    Where.Value("language", "en"));
var filtered = await collection.QueryAsync([Query], nResults: 2, where);
Show("This week, under 110 minutes, English", filtered);

var top = filtered.Metadatas[0].Count > 0 ? filtered.Metadatas[0][0] : null;
if (top is null)
{
    Console.WriteLine("\nTonight we put on: nothing that fits.");
    return;
}

Console.WriteLine($"\nTonight we put on: {top.Title}");
