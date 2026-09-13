using Lantern.Core;

const string CollectionName = "lantern_films";
const string UpdateId = "lost-in-translation";
const string DeleteId = "independence-day";

var apiKey = Env.RequireOpenAiKey();
var client = new PersistentClient(apiKey);

if (!client.ListCollections().Any(collection => collection.Name == CollectionName))
{
    Console.Error.WriteLine("No lantern_films collection. Run: dotnet run --project src/CreateCollection");
    return 1;
}

var collection = client.GetCollection(CollectionName);
Console.WriteLine($"Films in the collection: {collection.Count()}");

var before = collection.Get([UpdateId]);
Console.WriteLine($"\nBefore update: {before.Documents[0]}");

await collection.UpdateAsync(
    [UpdateId],
    ["Lost in Translation. Two jet-lagged strangers. Quiet hotel corridors, a karaoke room, almost no plot."]);

var after = collection.Get([UpdateId]);
Console.WriteLine($"After update: {after.Documents[0]}");

collection.Delete([DeleteId]);
Console.WriteLine($"\nTook down {DeleteId}. Films left: {collection.Count()}");

Console.WriteLine("\nCollections:");
Console.WriteLine(string.Join(", ", client.ListCollections().Select(item => item.Name)));

client.DeleteCollection(CollectionName);
Console.WriteLine("Dropped lantern_films.");
Console.WriteLine(string.Join(", ", client.ListCollections().Select(item => item.Name)));
return 0;
