namespace Lantern.Core;

public static class Cosine
{
    public static float Score(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count != b.Count || a.Count == 0)
        {
            throw new InvalidOperationException("Cosine needs two vectors of the same non-zero length.");
        }

        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (var i = 0; i < a.Count; i++)
        {
            var x = a[i];
            var y = b[i];
            dot += x * y;
            normA += x * x;
            normB += y * y;
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        if (denom == 0)
        {
            throw new InvalidOperationException("Cosine cannot score a zero vector.");
        }

        return (float)(dot / denom);
    }

    public static float Distance(IReadOnlyList<float> a, IReadOnlyList<float> b) => 1f - Score(a, b);
}
