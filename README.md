# dotnet-chroma-film-recs

This repo demonstrates how to keep a small catalog as vectors and search it by meaning. The example we will use is this week's films at The Lantern, a one-screen cinema. In our case we need a film for a tired evening, even when the text never says the word we typed.

This article is a practical implementation of the concepts in [Vector Databases for RAG: An Introduction](https://www.coursera.org/learn/vector-databases-for-rag-an-introduction).

## Setup

```powershell
dotnet restore
copy .env.example .env
```

Put your OpenAI API key in `.env`.

## Score two films by hand

```powershell
dotnet run --project src/CosineByHand
```

## Now we put the catalog in Chroma

Chroma has no first-class .NET client we can depend on. We keep an in-memory cosine collection and persist it as JSON under chroma_data/ instead, with the same add, query, update, and delete steps.

```powershell
dotnet run --project src/CreateCollection
```

## Update a card and drop the collection

```powershell
dotnet run --project src/QueryUpdateDelete
```

## Filter the catalog and pick a film

```powershell
dotnet run --project src/FilterRecommend
```

## Now we stuff the hits into a prompt

```powershell
dotnet run --project src/RagFromHits
```

## Serve it with a small page

```powershell
dotnet run --project src/App
```
