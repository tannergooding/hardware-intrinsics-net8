using BenchmarkDotNet.Attributes;

public partial class Benchmarks
{
    [Benchmark]
    public void AddScalarUnrolled()
    {
        AddScalarUnrolled(X, Y, Destination);
    }

    private void AddScalarUnrolled(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(x.Length, y.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x.Length, destination.Length);

        ThrowIfSpansOverlapAndAreNotSame(x, destination);
        ThrowIfSpansOverlapAndAreNotSame(y, destination);

        int unrolledEnd = x.Length;

        if (unrolledEnd >= 4)
        {
            unrolledEnd -= (unrolledEnd % 4);

            for (int i = 0; i < unrolledEnd; i += 4)
            {
                destination[i + 0] = x[i + 0] + y[i + 0];
                destination[i + 1] = x[i + 1] + y[i + 1];
                destination[i + 2] = x[i + 2] + y[i + 2];
                destination[i + 3] = x[i + 3] + y[i + 3];
            }
        }

        for (int i = unrolledEnd; i < x.Length; i++)
        {
            destination[i] = x[i] + y[i];
        }
    }
}