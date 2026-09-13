using Lantern.Core;

const string Query = "something quiet after a long day";
const string CollectionName = "lantern_films";

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

var results = await collection.QueryAsync([Query], nResults: 3);
var ids = results.Ids[0];
var documents = results.Documents[0];
var distances = results.Distances[0];

Console.WriteLine($"Query: {Query}");
for (var index = 0; index < ids.Count; index++)
{
    Console.WriteLine($"{distances[index]:F4}  {ids[index]}");
    Console.WriteLine($"  {documents[index]}");
}
