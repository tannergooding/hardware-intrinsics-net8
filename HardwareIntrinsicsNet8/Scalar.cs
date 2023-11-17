using BenchmarkDotNet.Attributes;

public partial class Benchmarks
{
    [Benchmark(Baseline = true)]
    public void AddScalar()
    {
        AddScalar(X, Y, Destination);
    }

    private void AddScalar(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(x.Length, y.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x.Length, destination.Length);

        ThrowIfSpansOverlapAndAreNotSame(x, destination);
        ThrowIfSpansOverlapAndAreNotSame(y, destination);

        for (int i = 0; i < x.Length; i++)
        {
            destination[i] = x[i] + y[i];
        }
    }
}