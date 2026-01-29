namespace K1.VectorDB.Engine.Helpers;

internal static class SimilarityMath
{
    private const double Epsilon = 1e-12;

    public static double CosineSimilarity(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        
        if (x.Length != y.Length) throw new ArgumentException("Vectors must have same length.");

        var dot = 0.0;
        var nx = 0.0;
        var ny = 0.0;
        for (var i = 0; i < x.Length; i++)
        {
            var a = x[i];
            var b = y[i];
            dot += a * b;
            nx += a * a;
            ny += b * b;
        }

        var sqrt = Math.Sqrt(nx) * Math.Sqrt(ny);
        if (sqrt < Epsilon) return 0.0;
        return dot / sqrt;
    }

    public static double EuclideanDistance(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        
        if (x.Length != y.Length) throw new ArgumentException("Vectors must have same length.");

        var sum = x.Select((t, i) => t - y[i]).Sum(d => d * d);

        return Math.Sqrt(sum);
    }
}