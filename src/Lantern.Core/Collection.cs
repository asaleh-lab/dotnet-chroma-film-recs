using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lantern.Core;

/// <summary>
/// Chroma has no first-class .NET client we can depend on.
/// Python uses chromadb.PersistentClient. The official JavaScript client talks to a server.
/// This file is an in-memory cosine collection with add, query, update, and delete.
/// We persist JSON under chroma_data/ so CreateCollection can feed QueryUpdateDelete
/// the same way the Python repo does.
/// </summary>
public sealed class Where
{
    private readonly Func<FilmMetadata, bool> _matches;

    private Where(Func<FilmMetadata, bool> matches)
    {
        _matches = matches;
    }

    public bool Matches(FilmMetadata metadata) => _matches(metadata);

    public static Where And(params Where[] clauses) =>
        new(metadata => clauses.All(clause => clause.Matches(metadata)));

    public static Where Eq(string field, object value) =>
        new(metadata => EqualsField(metadata.Field(field), value));

    public static Where Lt(string field, double value) =>
        new(metadata => metadata.Field(field) is IConvertible number && Convert.ToDouble(number) < value);

    public static Where Value(string field, object value) => Eq(field, value);

    private static bool EqualsField(object? left, object right)
    {
        if (left is null)
        {
            return false;
        }

        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool == rightBool;
        }

        if (left is IConvertible && right is IConvertible && left is not string && right is not string)
        {
            return Convert.ToDouble(left).Equals(Convert.ToDouble(right));
        }

        return string.Equals(Convert.ToString(left), Convert.ToString(right), StringComparison.Ordinal);
    }
}

public sealed class QueryResult
{
    public required IReadOnlyList<IReadOnlyList<string>> Ids { get; init; }
    public required IReadOnlyList<IReadOnlyList<string>> Documents { get; init; }
    public required IReadOnlyList<IReadOnlyList<float>> Distances { get; init; }
    public required IReadOnlyList<IReadOnlyList<FilmMetadata>> Metadatas { get; init; }
}

public sealed class GetResult
{
    public required IReadOnlyList<string> Ids { get; init; }
    public required IReadOnlyList<string> Documents { get; init; }
    public required IReadOnlyList<FilmMetadata> Metadatas { get; init; }
}

public sealed class Collection
{
    private readonly Embedder _embedder;
    private StoredCollection _stored;

    internal Collection(Embedder embedder, StoredCollection stored)
    {
        _embedder = embedder;
        _stored = stored;
    }

    public string Name => _stored.Name;

    public int Count() => _stored.Items.Count;

    public async Task AddAsync(
        IReadOnlyList<string> ids,
        IReadOnlyList<string> documents,
        IReadOnlyList<FilmMetadata> metadatas)
    {
        if (ids.Count != documents.Count || ids.Count != metadatas.Count)
        {
            throw new InvalidOperationException("add needs matching ids, documents, and metadatas.");
        }

        var embeddings = await _embedder.EmbedAsync(documents);
        for (var index = 0; index < ids.Count; index++)
        {
            _stored.Items[ids[index]] = new StoredItem
            {
                Document = documents[index],
                Embedding = embeddings[index],
                Metadata = metadatas[index]
            };
        }

        Store.Write(_stored);
    }

    public async Task UpdateAsync(IReadOnlyList<string> ids, IReadOnlyList<string> documents)
    {
        if (ids.Count != documents.Count)
        {
            throw new InvalidOperationException("update needs matching ids and documents.");
        }

        var embeddings = await _embedder.EmbedAsync(documents);
        for (var index = 0; index < ids.Count; index++)
        {
            var id = ids[index];
            if (!_stored.Items.TryGetValue(id, out var existing))
            {
                throw new InvalidOperationException($"update could not find {id}.");
            }

            _stored.Items[id] = new StoredItem
            {
                Document = documents[index],
                Embedding = embeddings[index],
                Metadata = existing.Metadata
            };
        }

        Store.Write(_stored);
    }

    public void Delete(IReadOnlyList<string> ids)
    {
        foreach (var id in ids)
        {
            _stored.Items.Remove(id);
        }

        Store.Write(_stored);
    }

    public GetResult Get(IReadOnlyList<string> ids)
    {
        var foundIds = new List<string>();
        var documents = new List<string>();
        var metadatas = new List<FilmMetadata>();
        foreach (var id in ids)
        {
            if (!_stored.Items.TryGetValue(id, out var item))
            {
                continue;
            }

            foundIds.Add(id);
            documents.Add(item.Document);
            metadatas.Add(item.Metadata);
        }

        return new GetResult
        {
            Ids = foundIds,
            Documents = documents,
            Metadatas = metadatas
        };
    }

    public async Task<QueryResult> QueryAsync(IReadOnlyList<string> queryTexts, int nResults, Where? where = null)
    {
        var queryEmbeddings = await _embedder.EmbedAsync(queryTexts);
        var ids = new List<IReadOnlyList<string>>();
        var documents = new List<IReadOnlyList<string>>();
        var distances = new List<IReadOnlyList<float>>();
        var metadatas = new List<IReadOnlyList<FilmMetadata>>();

        foreach (var queryEmbedding in queryEmbeddings)
        {
            var ranked = _stored.Items
                .Where(pair => where is null || where.Matches(pair.Value.Metadata))
                .Select(pair => new
                {
                    pair.Key,
                    pair.Value,
                    Distance = Cosine.Distance(queryEmbedding, pair.Value.Embedding)
                })
                .OrderBy(row => row.Distance)
                .Take(nResults)
                .ToList();

            ids.Add(ranked.Select(row => row.Key).ToList());
            documents.Add(ranked.Select(row => row.Value.Document).ToList());
            distances.Add(ranked.Select(row => row.Distance).ToList());
            metadatas.Add(ranked.Select(row => row.Value.Metadata).ToList());
        }

        return new QueryResult
        {
            Ids = ids,
            Documents = documents,
            Distances = distances,
            Metadatas = metadatas
        };
    }
}

public sealed class PersistentClient
{
    private readonly Embedder _embedder;

    public PersistentClient(string apiKey)
    {
        _embedder = new Embedder(apiKey);
    }

    public IReadOnlyList<NamedCollection> ListCollections()
    {
        var dir = RepoPaths.ChromaData;
        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.GetFiles(dir, "*.json")
            .Select(path => new NamedCollection(Path.GetFileNameWithoutExtension(path)))
            .ToList();
    }

    public void DeleteCollection(string name)
    {
        var path = Store.CollectionPath(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public Collection CreateCollection(string name, IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (ListCollections().Any(collection => collection.Name == name))
        {
            throw new InvalidOperationException($"Collection {name} already exists.");
        }

        var stored = new StoredCollection
        {
            Name = name,
            Metadata = metadata is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(metadata),
            Items = new Dictionary<string, StoredItem>()
        };
        Store.Write(stored);
        return new Collection(_embedder, stored);
    }

    public Collection GetCollection(string name)
    {
        var stored = Store.Read(name);
        if (stored is null)
        {
            throw new InvalidOperationException($"Collection {name} is missing.");
        }

        return new Collection(_embedder, stored);
    }
}

public readonly record struct NamedCollection(string Name);

internal sealed class StoredItem
{
    [JsonPropertyName("document")]
    public required string Document { get; init; }

    [JsonPropertyName("embedding")]
    public required float[] Embedding { get; init; }

    [JsonPropertyName("metadata")]
    public required FilmMetadata Metadata { get; init; }
}

internal sealed class StoredCollection
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("metadata")]
    public required Dictionary<string, string> Metadata { get; init; }

    [JsonPropertyName("items")]
    public required Dictionary<string, StoredItem> Items { get; init; }
}

internal static class Store
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string CollectionPath(string name) =>
        Path.Combine(RepoPaths.ChromaData, $"{name}.json");

    public static StoredCollection? Read(string name)
    {
        var path = CollectionPath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<StoredCollection>(File.ReadAllText(path), JsonOptions);
    }

    public static void Write(StoredCollection stored)
    {
        Directory.CreateDirectory(RepoPaths.ChromaData);
        File.WriteAllText(CollectionPath(stored.Name), JsonSerializer.Serialize(stored, JsonOptions));
    }
}
