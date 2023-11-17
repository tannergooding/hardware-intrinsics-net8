using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;

public partial class Benchmarks
{
    [Benchmark]
    public void AddVector128Unrolled()
    {
        AddVector128Unrolled(X, Y, Destination);
    }

    private void AddVector128Unrolled(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(x.Length, y.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x.Length, destination.Length);

        ThrowIfSpansOverlapAndAreNotSame(x, destination);
        ThrowIfSpansOverlapAndAreNotSame(y, destination);

        ref float xRef = ref MemoryMarshal.GetReference(x);
        ref float yRef = ref MemoryMarshal.GetReference(y);
        ref float dRef = ref MemoryMarshal.GetReference(destination);

        nuint vectorUnrolledEnd = (uint)(x.Length);

        if (vectorUnrolledEnd >= ((uint)(Vector128<float>.Count) * 4))
        {
            vectorUnrolledEnd -= (vectorUnrolledEnd % ((uint)(Vector128<float>.Count) * 4));

            for (nuint i = 0; i < vectorUnrolledEnd; i += ((uint)(Vector128<float>.Count) * 4))
            {
                Vector128<float> result0 = Vector128.LoadUnsafe(ref xRef, i + ((uint)(Vector128<float>.Count) * 0)) +
                                           Vector128.LoadUnsafe(ref yRef, i + ((uint)(Vector128<float>.Count) * 0));
                Vector128<float> result1 = Vector128.LoadUnsafe(ref xRef, i + ((uint)(Vector128<float>.Count) * 1)) +
                                           Vector128.LoadUnsafe(ref yRef, i + ((uint)(Vector128<float>.Count) * 1));
                Vector128<float> result2 = Vector128.LoadUnsafe(ref xRef, i + ((uint)(Vector128<float>.Count) * 2)) +
                                           Vector128.LoadUnsafe(ref yRef, i + ((uint)(Vector128<float>.Count) * 2));
                Vector128<float> result3 = Vector128.LoadUnsafe(ref xRef, i + ((uint)(Vector128<float>.Count) * 3)) +
                                           Vector128.LoadUnsafe(ref yRef, i + ((uint)(Vector128<float>.Count) * 3));

                result0.StoreUnsafe(ref dRef, i + ((uint)(Vector128<float>.Count) * 0));
                result1.StoreUnsafe(ref dRef, i + ((uint)(Vector128<float>.Count) * 1));
                result2.StoreUnsafe(ref dRef, i + ((uint)(Vector128<float>.Count) * 2));
                result3.StoreUnsafe(ref dRef, i + ((uint)(Vector128<float>.Count) * 3));
            }
        }

        for (int i = (int)(vectorUnrolledEnd); i < x.Length; i++)
        {
            destination[i] = x[i] + y[i];
        }
    }
}