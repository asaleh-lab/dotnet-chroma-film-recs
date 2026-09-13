using Lantern.Core;

const string CollectionName = "lantern_films";
const string SystemPrompt =
    "Answer from these films only. If none of them fit the need, say so. " +
    "Name the film you would put on tonight.";
const string Page = """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>The Lantern</title>
  <style>
    :root { color-scheme: light; font-family: Georgia, "Times New Roman", serif; }
    body { margin: 0; background: #f6f1e7; color: #1b1b1b; }
    main { max-width: 40rem; margin: 0 auto; padding: 2rem 1.25rem 4rem; }
    h1 { font-size: 1.75rem; font-weight: 400; margin: 0 0 0.35rem; }
    p.lead { margin: 0 0 1.5rem; color: #444; }
    #log { display: flex; flex-direction: column; gap: 0.75rem; margin-bottom: 1.25rem; }
    .bubble { padding: 0.75rem 0.9rem; border-radius: 0.4rem; white-space: pre-wrap; }
    .need { background: #1b1b1b; color: #f6f1e7; }
    .reply { background: #fff; border: 1px solid #d8cfc0; }
    form { display: flex; gap: 0.5rem; }
    input { flex: 1; padding: 0.7rem 0.8rem; border: 1px solid #c9bfb0; border-radius: 0.35rem; font: inherit; }
    button { padding: 0.7rem 1rem; border: 0; border-radius: 0.35rem; background: #1b1b1b; color: #f6f1e7; font: inherit; cursor: pointer; }
    .examples { display: flex; flex-wrap: wrap; gap: 0.5rem; margin: 0 0 1rem; }
    .examples button { background: #fff; color: #1b1b1b; border: 1px solid #c9bfb0; }
  </style>
</head>
<body>
  <main>
    <h1>The Lantern</h1>
    <p class="lead">Tell us the kind of evening you want. We pick from this week's short English films.</p>
    <div class="examples">
      <button type="button" data-need="something quiet after a long day">something quiet after a long day</button>
      <button type="button" data-need="something loud">something loud</button>
    </div>
    <div id="log"></div>
    <form id="ask">
      <input name="need" autocomplete="off" placeholder="something quiet after a long day" />
      <button type="submit">Ask</button>
    </form>
  </main>
  <script>
    const log = document.getElementById("log");
    const form = document.getElementById("ask");
    const input = form.querySelector("input");

    function addBubble(text, className) {
      const div = document.createElement("div");
      div.className = "bubble " + className;
      div.textContent = text;
      log.appendChild(div);
    }

    async function ask(need) {
      if (!need) return;
      addBubble(need, "need");
      const response = await fetch("/chat", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ message: need }),
      });
      const data = await response.json();
      addBubble(data.reply || data.error || "No reply.", "reply");
    }

    form.addEventListener("submit", (event) => {
      event.preventDefault();
      const need = input.value.trim();
      input.value = "";
      ask(need);
    });

    document.querySelectorAll("[data-need]").forEach((button) => {
      button.addEventListener("click", () => ask(button.getAttribute("data-need")));
    });
  </script>
</body>
</html>
""";

var where = Where.And(
    Where.Eq("playing_this_week", true),
    Where.Lt("minutes", 110),
    Where.Value("language", "en"));

var apiKey = Env.RequireOpenAiKey();
var model = Env.OpenAiModel();
var films = Films.Load();
var store = new PersistentClient(apiKey);

if (store.ListCollections().Any(collection => collection.Name == CollectionName))
{
    store.DeleteCollection(CollectionName);
}

var collection = store.CreateCollection(
    CollectionName,
    new Dictionary<string, string> { ["hnsw:space"] = "cosine" });
await collection.AddAsync(
    films.Select(film => film.Id).ToList(),
    films.Select(Films.DocumentFor).ToList(),
    films.Select(Films.MetadataFor).ToList());

var port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var parsed) ? parsed : 7860;
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
var app = builder.Build();

app.MapGet("/", () => Results.Content(Page, "text/html; charset=utf-8"));
app.MapGet("/index.html", () => Results.Content(Page, "text/html; charset=utf-8"));
app.MapPost("/chat", async (ChatRequest request) =>
{
    var message = request.Message?.Trim() ?? "";
    if (message.Length == 0)
    {
        return Results.Json(new { error = "Send a message." }, statusCode: 400);
    }

    try
    {
        var reply = await RecommendAsync(collection, apiKey, model, message, where);
        return Results.Json(new { reply });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
});

Console.WriteLine($"The Lantern is on http://127.0.0.1:{port}");
app.Run();

static async Task<string> RecommendAsync(
    Collection collection,
    string apiKey,
    string model,
    string message,
    Where filter)
{
    var hits = await collection.QueryAsync([message], nResults: 2, filter);
    var documents = hits.Documents[0];
    var metadatas = hits.Metadatas[0];
    if (documents.Count == 0)
    {
        return "None of the films we can show tonight fit that.";
    }

    var context = string.Join("\n\n", documents);
    var answer = await LanternChat.CompleteAsync(
        apiKey,
        model,
        SystemPrompt,
        $"Films:\n{context}\n\nNeed: {message}");
    var titles = string.Join(", ", metadatas.Select(meta => meta.Title));
    return $"{answer}\n\nHits: {titles}";
}

sealed record ChatRequest(string? Message);
