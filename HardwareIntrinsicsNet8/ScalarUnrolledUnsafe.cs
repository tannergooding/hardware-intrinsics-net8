using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

public partial class Benchmarks
{
    [Benchmark]
    public void AddScalarUnrolledUnsafe()
    {
        AddScalarUnrolledUnsafe(X, Y, Destination);
    }

    private void AddScalarUnrolledUnsafe(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(x.Length, y.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x.Length, destination.Length);

        ThrowIfSpansOverlapAndAreNotSame(x, destination);
        ThrowIfSpansOverlapAndAreNotSame(y, destination);

        ref float xRef = ref MemoryMarshal.GetReference(x);
        ref float yRef = ref MemoryMarshal.GetReference(y);
        ref float dRef = ref MemoryMarshal.GetReference(destination);

        int unrolledEnd = x.Length;

        if (unrolledEnd >= 4)
        {
            unrolledEnd -= (unrolledEnd % 4);

            for (int i = 0; i < unrolledEnd; i += 4)
            {
                Unsafe.Add(ref dRef, i + 0) = Unsafe.Add(ref xRef, i + 0) +
                                              Unsafe.Add(ref yRef, i + 0);
                Unsafe.Add(ref dRef, i + 1) = Unsafe.Add(ref xRef, i + 1) +
                                              Unsafe.Add(ref yRef, i + 1);
                Unsafe.Add(ref dRef, i + 2) = Unsafe.Add(ref xRef, i + 2) +
                                              Unsafe.Add(ref yRef, i + 2);
                Unsafe.Add(ref dRef, i + 3) = Unsafe.Add(ref xRef, i + 3) +
                                              Unsafe.Add(ref yRef, i + 3);
            }
        }

        for (int i = unrolledEnd; i < x.Length; i++)
        {
            destination[i] = x[i] + y[i];
        }
    }
}