using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;

public partial class Benchmarks
{
    [Benchmark]
    public void AddVector128()
    {
        AddVector128(X, Y, Destination);
    }

    private void AddVector128(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(x.Length, y.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x.Length, destination.Length);

        ThrowIfSpansOverlapAndAreNotSame(x, destination);
        ThrowIfSpansOverlapAndAreNotSame(y, destination);

        ref float xRef = ref MemoryMarshal.GetReference(x);
        ref float yRef = ref MemoryMarshal.GetReference(y);
        ref float dRef = ref MemoryMarshal.GetReference(destination);

        nuint vectorEnd = (uint)(x.Length);

        if (vectorEnd >= (uint)(Vector128<float>.Count))
        {
            vectorEnd -= (vectorEnd % (uint)(Vector128<float>.Count));

            for (nuint i = 0; i < vectorEnd; i += (uint)(Vector128<float>.Count))
            {
                Vector128<float> result = Vector128.LoadUnsafe(ref xRef, i) +
                                          Vector128.LoadUnsafe(ref yRef, i);
                result.StoreUnsafe(ref dRef, i);
            }
        }

        for (int i = (int)(vectorEnd); i < x.Length; i++)
        {
            destination[i] = x[i] + y[i];
        }
    }
}