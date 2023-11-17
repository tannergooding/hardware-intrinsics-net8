using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;

public partial class Benchmarks
{
    [Benchmark]
    public void AddVectorAll()
    {
        AddVectorAll(X, Y, Destination);
    }

    /// <summary>Defines the threshold, in bytes, at which non-temporal stores will be used.</summary>
    /// <remarks>
    ///     A non-temporal store is one that allows the CPU to bypass the cache when writing to memory.
    ///
    ///     This can be beneficial when working with large amounts of memory where the writes would otherwise
    ///     cause large amounts of repeated updates and evictions. The hardware optimization manuals recommend
    ///     the threshold to be roughly half the size of the last level of on-die cache -- that is, if you have approximately
    ///     4MB of L3 cache per core, you'd want this to be approx. 1-2MB, depending on if hyperthreading was enabled.
    ///
    ///     However, actually computing the amount of L3 cache per core can be tricky or error prone. Native memcpy
    ///     algorithms use a constant threshold that is typically around 256KB and we match that here for simplicity. This
    ///     threshold accounts for most processors in the last 10-15 years that had approx. 1MB L3 per core and support
    ///     hyperthreading, giving a per core last level cache of approx. 512KB.
    /// </remarks>
    private const nuint NonTemporalByteThreshold = 256 * 1024;

    private unsafe void AddVectorAll(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(x.Length, y.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x.Length, destination.Length);

        ThrowIfSpansOverlapAndAreNotSame(x, destination);
        ThrowIfSpansOverlapAndAreNotSame(y, destination);

        // Since every branch has a cost and since that cost is
        // essentially lost for larger inputs, we do branches
        // in a way that allows us to have the minimum possible
        // for small sizes
 
        ref float xRef = ref MemoryMarshal.GetReference(x);
        ref float yRef = ref MemoryMarshal.GetReference(y);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
 
        nuint remainder = (uint)(x.Length);
 
        if (Vector512.IsHardwareAccelerated)
        {
            if (remainder >= (uint)(Vector512<float>.Count))
            {
                Vectorized512(ref xRef, ref yRef, ref dRef, remainder);
            }
            else
            {
                // We have less than a vector and so we can only handle this as scalar. To do this
                // efficiently, we simply have a small jump table and fallthrough. So we get a simple
                // length check, single jump, and then linear execution.
 
                Vectorized512Small(ref xRef, ref yRef, ref dRef, remainder);
            }
 
            return;
        }
 
        if (Vector256.IsHardwareAccelerated)
        {
            if (remainder >= (uint)(Vector256<float>.Count))
            {
                Vectorized256(ref xRef, ref yRef, ref dRef, remainder);
            }
            else
            {
                // We have less than a vector and so we can only handle this as scalar. To do this
                // efficiently, we simply have a small jump table and fallthrough. So we get a simple
                // length check, single jump, and then linear execution.
 
                Vectorized256Small(ref xRef, ref yRef, ref dRef, remainder);
            }
 
            return;
        }
 
        if (Vector128.IsHardwareAccelerated)
        {
            if (remainder >= (uint)(Vector128<float>.Count))
            {
                Vectorized128(ref xRef, ref yRef, ref dRef, remainder);
            }
            else
            {
                // We have less than a vector and so we can only handle this as scalar. To do this
                // efficiently, we simply have a small jump table and fallthrough. So we get a simple
                // length check, single jump, and then linear execution.
 
                Vectorized128Small(ref xRef, ref yRef, ref dRef, remainder);
            }
 
            return;
        }
 
        // This is the software fallback when no acceleration is available
        // It requires no branches to hit
 
        SoftwareFallback(ref xRef, ref yRef, ref dRef, remainder);
 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SoftwareFallback(ref float xRef, ref float yRef, ref float dRef, nuint length)
        {
            for (nuint i = 0; i < length; i++)
            {
                Unsafe.Add(ref dRef, i) = Unsafe.Add(ref xRef, i) + Unsafe.Add(ref yRef, i);
            }
        }
 
        static void Vectorized128(ref float xRef, ref float yRef, ref float dRef, nuint remainder)
        {
            ref float dRefBeg = ref dRef;
 
            // Preload the beginning and end so that overlapping accesses don't negatively impact the data
 
            Vector128<float> beg = Vector128.LoadUnsafe(ref xRef) +
                                   Vector128.LoadUnsafe(ref yRef);
            Vector128<float> end = Vector128.LoadUnsafe(ref xRef, remainder - (uint)(Vector128<float>.Count)) +
                                   Vector128.LoadUnsafe(ref yRef, remainder - (uint)(Vector128<float>.Count));
 
            if (remainder > (uint)(Vector128<float>.Count * 8))
            {
                // Pinning is cheap and will be short lived for small inputs and unlikely to be impactful
                // for large inputs (> 85KB) which are on the LOH and unlikely to be compacted.
 
                fixed (float* px = &xRef)
                fixed (float* py = &yRef)
                fixed (float* pd = &dRef)
                {
                    float* xPtr = px;
                    float* yPtr = py;
                    float* dPtr = pd;
 
                    // We need to the ensure the underlying data can be aligned and only align
                    // it if it can. It is possible we have an unaligned ref, in which case we
                    // can never achieve the required SIMD alignment.
 
                    bool canAlign = ((nuint)(dPtr) % sizeof(float)) == 0;
 
                    if (canAlign)
                    {
                        // Compute by how many elements we're misaligned and adjust the pointers accordingly
                        //
                        // Noting that we are only actually aligning dPtr. This is because unaligned stores
                        // are more expensive than unaligned loads and aligning both is significantly more
                        // complex.
 
                        nuint misalignment = ((uint)(sizeof(Vector128<float>)) - ((nuint)(dPtr) % (uint)(sizeof(Vector128<float>)))) / sizeof(float);
 
                        xPtr += misalignment;
                        yPtr += misalignment;
                        dPtr += misalignment;
 
                        Debug.Assert(((nuint)(dPtr) % (uint)(sizeof(Vector128<float>))) == 0);
 
                        remainder -= misalignment;
                    }
 
                    Vector128<float> vector1;
                    Vector128<float> vector2;
                    Vector128<float> vector3;
                    Vector128<float> vector4;
 
                    if ((remainder > (NonTemporalByteThreshold / sizeof(float))) && canAlign)
                    {
                        // This loop stores the data non-temporally, which benefits us when there
                        // is a large amount of data involved as it avoids polluting the cache.
 
                        while (remainder >= (uint)(Vector128<float>.Count * 4))
                        {
                            // We load, process, and store the first four vectors
 
                            vector1 = Vector128.Load(xPtr + (uint)(Vector128<float>.Count * 0)) +
                                      Vector128.Load(yPtr + (uint)(Vector128<float>.Count * 0));
                            vector2 = Vector128.Load(xPtr + (uint)(Vector128<float>.Count * 1)) +
                                      Vector128.Load(yPtr + (uint)(Vector128<float>.Count * 1));
                            vector3 = Vector128.Load(xPtr + (uint)(Vector128<float>.Count * 2)) +
                                      Vector128.Load(yPtr + (uint)(Vector128<float>.Count * 2));
                            vector4 = Vector128.Load(xPtr + (uint)(Vector128<float>.Count * 3)) +
                                      Vector128.Load(yPtr + (uint)(Vector128<float>.Count * 3));
 
                            vector1.StoreAlignedNonTemporal(dPtr + (uint)(Vector128<float>.Count * 0));
                            vector2.StoreAlignedNonTemporal(dPtr + (uint)(Vector128<float>.Count * 1));
                            vector3.StoreAlignedNonTemporal(dPtr + (uint)(Vector128<float>.Count * 2));
                            vector4.StoreAlignedNonTemporal(dPtr + (uint)(Vector128<float>.Count * 3));
 
                            // We adjust the source and destination references, then update
                            // the count of remaining elements to process.
 
                            xPtr += (uint)(Vector128<float>.Count * 4);
                            yPtr += (uint)(Vector128<float>.Count * 4);
                            dPtr += (uint)(Vector128<float>.Count * 4);
 
                            remainder -= (uint)(Vector128<float>.Count * 4);
                        }
                    }
                    else
                    {
                        while (remainder >= (uint)(Vector128<float>.Count * 4))
                        {
                            // We load, process, and store the first four vectors
 
                            vector1 = Vector128.Load(xPtr + (uint)(Vector128<float>.Count * 0)) +
                                      Vector128.Load(yPtr + (uint)(Vector128<float>.Count * 0));
                            vector2 = Vector128.Load(xPtr + (uint)(Vector128<float>.Count * 1)) +
                                      Vector128.Load(yPtr + (uint)(Vector128<float>.Count * 1));
                            vector3 = Vector128.Load(xPtr + (uint)(Vector128<float>.Count * 2)) +
                                      Vector128.Load(yPtr + (uint)(Vector128<float>.Count * 2));
                            vector4 = Vector128.Load(xPtr + (uint)(Vector128<float>.Count * 3)) +
                                      Vector128.Load(yPtr + (uint)(Vector128<float>.Count * 3));
 
                            vector1.Store(dPtr + (uint)(Vector128<float>.Count * 0));
                            vector2.Store(dPtr + (uint)(Vector128<float>.Count * 1));
                            vector3.Store(dPtr + (uint)(Vector128<float>.Count * 2));
                            vector4.Store(dPtr + (uint)(Vector128<float>.Count * 3));
 
                            // We adjust the source and destination references, then update
                            // the count of remaining elements to process.
 
                            xPtr += (uint)(Vector128<float>.Count * 4);
                            yPtr += (uint)(Vector128<float>.Count * 4);
                            dPtr += (uint)(Vector128<float>.Count * 4);
 
                            remainder -= (uint)(Vector128<float>.Count * 4);
                        }
                    }
 
                    // Adjusting the refs here allows us to avoid pinning for very small inputs
 
                    xRef = ref *xPtr;
                    yRef = ref *yPtr;
                    dRef = ref *dPtr;
                }
            }
 
            // Process the remaining [Count, Count * 4] elements via a jump table
            //
            // Unless the original length was an exact multiple of Count, then we'll
            // end up reprocessing a couple elements in case 1 for end. We'll also
            // potentially reprocess a few elements in case 0 for beg, to handle any
            // data before the first aligned address.
 
            nuint endIndex = remainder;
            remainder = (remainder + (uint)(Vector128<float>.Count - 1)) & (nuint)(-Vector128<float>.Count);
 
            switch (remainder / (uint)(Vector128<float>.Count))
            {
                case 4:
                {
                    Vector128<float> vector = Vector128.LoadUnsafe(ref xRef, remainder - (uint)(Vector128<float>.Count * 4)) +
                                              Vector128.LoadUnsafe(ref yRef, remainder - (uint)(Vector128<float>.Count * 4));
                    vector.StoreUnsafe(ref dRef, remainder - (uint)(Vector128<float>.Count * 4));
                    goto case 3;
                }
 
                case 3:
                {
                    Vector128<float> vector = Vector128.LoadUnsafe(ref xRef, remainder - (uint)(Vector128<float>.Count * 3)) +
                                              Vector128.LoadUnsafe(ref yRef, remainder - (uint)(Vector128<float>.Count * 3));
                    vector.StoreUnsafe(ref dRef, remainder - (uint)(Vector128<float>.Count * 3));
                    goto case 2;
                }
 
                case 2:
                {
                    Vector128<float> vector = Vector128.LoadUnsafe(ref xRef, remainder - (uint)(Vector128<float>.Count * 2)) +
                                              Vector128.LoadUnsafe(ref yRef, remainder - (uint)(Vector128<float>.Count * 2));
                    vector.StoreUnsafe(ref dRef, remainder - (uint)(Vector128<float>.Count * 2));
                    goto case 1;
                }
 
                case 1:
                {
                    // Store the last block, which includes any elements that wouldn't fill a full vector
                    end.StoreUnsafe(ref dRef, endIndex - (uint)Vector128<float>.Count);
                    goto case 0;
                }
 
                case 0:
                {
                    // Store the first block, which includes any elements preceding the first aligned block
                    beg.StoreUnsafe(ref dRefBeg);
                    break;
                }
            }
        }
 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Vectorized128Small(ref float xRef, ref float yRef, ref float dRef, nuint remainder)
        {
            switch (remainder)
            {
                case 3:
                {
                    Unsafe.Add(ref dRef, 2) = Unsafe.Add(ref xRef, 2) +
                                              Unsafe.Add(ref yRef, 2);
                    goto case 2;
                }
 
                case 2:
                {
                    Unsafe.Add(ref dRef, 1) = Unsafe.Add(ref xRef, 1) +
                                              Unsafe.Add(ref yRef, 1);
                    goto case 1;
                }
 
                case 1:
                {
                    dRef = xRef + yRef;
                    goto case 0;
                }
 
                case 0:
                {
                    break;
                }
            }
        }
 
        static void Vectorized256(ref float xRef, ref float yRef, ref float dRef, nuint remainder)
        {
            ref float dRefBeg = ref dRef;
 
            // Preload the beginning and end so that overlapping accesses don't negatively impact the data
 
            Vector256<float> beg = Vector256.LoadUnsafe(ref xRef) +
                                   Vector256.LoadUnsafe(ref yRef);
            Vector256<float> end = Vector256.LoadUnsafe(ref xRef, remainder - (uint)(Vector256<float>.Count)) +
                                   Vector256.LoadUnsafe(ref yRef, remainder - (uint)(Vector256<float>.Count));
 
            if (remainder > (uint)(Vector256<float>.Count * 4))
            {
                // Pinning is cheap and will be short lived for small inputs and unlikely to be impactful
                // for large inputs (> 85KB) which are on the LOH and unlikely to be compacted.
 
                fixed (float* px = &xRef)
                fixed (float* py = &yRef)
                fixed (float* pd = &dRef)
                {
                    float* xPtr = px;
                    float* yPtr = py;
                    float* dPtr = pd;
 
                    // We need to the ensure the underlying data can be aligned and only align
                    // it if it can. It is possible we have an unaligned ref, in which case we
                    // can never achieve the required SIMD alignment.
 
                    bool canAlign = ((nuint)(dPtr) % sizeof(float)) == 0;
 
                    if (canAlign)
                    {
                        // Compute by how many elements we're misaligned and adjust the pointers accordingly
                        //
                        // Noting that we are only actually aligning dPtr. This is because unaligned stores
                        // are more expensive than unaligned loads and aligning both is significantly more
                        // complex.
 
                        nuint misalignment = ((uint)(sizeof(Vector256<float>)) - ((nuint)(dPtr) % (uint)(sizeof(Vector256<float>)))) / sizeof(float);
 
                        xPtr += misalignment;
                        yPtr += misalignment;
                        dPtr += misalignment;
 
                        Debug.Assert(((nuint)(dPtr) % (uint)(sizeof(Vector256<float>))) == 0);
 
                        remainder -= misalignment;
                    }
 
                    Vector256<float> vector1;
                    Vector256<float> vector2;
                    Vector256<float> vector3;
                    Vector256<float> vector4;
 
                    if ((remainder > (NonTemporalByteThreshold / sizeof(float))) && canAlign)
                    {
                        // This loop stores the data non-temporally, which benefits us when there
                        // is a large amount of data involved as it avoids polluting the cache.
 
                        while (remainder >= (uint)(Vector256<float>.Count * 4))
                        {
                            // We load, process, and store the first four vectors
 
                            vector1 = Vector256.Load(xPtr + (uint)(Vector256<float>.Count * 0)) +
                                      Vector256.Load(yPtr + (uint)(Vector256<float>.Count * 0));
                            vector2 = Vector256.Load(xPtr + (uint)(Vector256<float>.Count * 1)) +
                                      Vector256.Load(yPtr + (uint)(Vector256<float>.Count * 1));
                            vector3 = Vector256.Load(xPtr + (uint)(Vector256<float>.Count * 2)) +
                                      Vector256.Load(yPtr + (uint)(Vector256<float>.Count * 2));
                            vector4 = Vector256.Load(xPtr + (uint)(Vector256<float>.Count * 3)) +
                                      Vector256.Load(yPtr + (uint)(Vector256<float>.Count * 3));
 
                            vector1.StoreAlignedNonTemporal(dPtr + (uint)(Vector256<float>.Count * 0));
                            vector2.StoreAlignedNonTemporal(dPtr + (uint)(Vector256<float>.Count * 1));
                            vector3.StoreAlignedNonTemporal(dPtr + (uint)(Vector256<float>.Count * 2));
                            vector4.StoreAlignedNonTemporal(dPtr + (uint)(Vector256<float>.Count * 3));
 
                            // We adjust the source and destination references, then update
                            // the count of remaining elements to process.
 
                            xPtr += (uint)(Vector256<float>.Count * 4);
                            yPtr += (uint)(Vector256<float>.Count * 4);
                            dPtr += (uint)(Vector256<float>.Count * 4);
 
                            remainder -= (uint)(Vector256<float>.Count * 4);
                        }
                    }
                    else
                    {
                        while (remainder >= (uint)(Vector256<float>.Count * 4))
                        {
                            // We load, process, and store the first four vectors
 
                            vector1 = Vector256.Load(xPtr + (uint)(Vector256<float>.Count * 0)) +
                                      Vector256.Load(yPtr + (uint)(Vector256<float>.Count * 0));
                            vector2 = Vector256.Load(xPtr + (uint)(Vector256<float>.Count * 1)) +
                                      Vector256.Load(yPtr + (uint)(Vector256<float>.Count * 1));
                            vector3 = Vector256.Load(xPtr + (uint)(Vector256<float>.Count * 2)) +
                                      Vector256.Load(yPtr + (uint)(Vector256<float>.Count * 2));
                            vector4 = Vector256.Load(xPtr + (uint)(Vector256<float>.Count * 3)) +
                                      Vector256.Load(yPtr + (uint)(Vector256<float>.Count * 3));
 
                            vector1.Store(dPtr + (uint)(Vector256<float>.Count * 0));
                            vector2.Store(dPtr + (uint)(Vector256<float>.Count * 1));
                            vector3.Store(dPtr + (uint)(Vector256<float>.Count * 2));
                            vector4.Store(dPtr + (uint)(Vector256<float>.Count * 3));
 
                            // We adjust the source and destination references, then update
                            // the count of remaining elements to process.
 
                            xPtr += (uint)(Vector256<float>.Count * 4);
                            yPtr += (uint)(Vector256<float>.Count * 4);
                            dPtr += (uint)(Vector256<float>.Count * 4);
 
                            remainder -= (uint)(Vector256<float>.Count * 4);
                        }
                    }
 
                    // Adjusting the refs here allows us to avoid pinning for very small inputs
 
                    xRef = ref *xPtr;
                    yRef = ref *yPtr;
                    dRef = ref *dPtr;
                }
            }
 
            // Process the remaining [Count, Count * 4] elements via a jump table
            //
            // Unless the original length was an exact multiple of Count, then we'll
            // end up reprocessing a couple elements in case 1 for end. We'll also
            // potentially reprocess a few elements in case 0 for beg, to handle any
            // data before the first aligned address.
 
            nuint endIndex = remainder;
            remainder = (remainder + (uint)(Vector256<float>.Count - 1)) & (nuint)(-Vector256<float>.Count);
 
            switch (remainder / (uint)(Vector256<float>.Count))
            {
                case 4:
                {
                    Vector256<float> vector = Vector256.LoadUnsafe(ref xRef, remainder - (uint)(Vector256<float>.Count * 4)) +
                                              Vector256.LoadUnsafe(ref yRef, remainder - (uint)(Vector256<float>.Count * 4));
                    vector.StoreUnsafe(ref dRef, remainder - (uint)(Vector256<float>.Count * 4));
                    goto case 3;
                }
 
                case 3:
                {
                    Vector256<float> vector = Vector256.LoadUnsafe(ref xRef, remainder - (uint)(Vector256<float>.Count * 3)) +
                                              Vector256.LoadUnsafe(ref yRef, remainder - (uint)(Vector256<float>.Count * 3));
                    vector.StoreUnsafe(ref dRef, remainder - (uint)(Vector256<float>.Count * 3));
                    goto case 2;
                }
 
                case 2:
                {
                    Vector256<float> vector = Vector256.LoadUnsafe(ref xRef, remainder - (uint)(Vector256<float>.Count * 2)) +
                                              Vector256.LoadUnsafe(ref yRef, remainder - (uint)(Vector256<float>.Count * 2));
                    vector.StoreUnsafe(ref dRef, remainder - (uint)(Vector256<float>.Count * 2));
                    goto case 1;
                }
 
                case 1:
                {
                    // Store the last block, which includes any elements that wouldn't fill a full vector
                    end.StoreUnsafe(ref dRef, endIndex - (uint)Vector256<float>.Count);
                    goto case 0;
                }
 
                case 0:
                {
                    // Store the first block, which includes any elements preceding the first aligned block
                    beg.StoreUnsafe(ref dRefBeg);
                    break;
                }
            }
        }
 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Vectorized256Small(ref float xRef, ref float yRef, ref float dRef, nuint remainder)
        {
            switch (remainder)
            {
                case 7:
                case 6:
                case 5:
                {
                    Debug.Assert(Vector128.IsHardwareAccelerated);
 
                    Vector128<float> beg = Vector128.LoadUnsafe(ref xRef) +
                                           Vector128.LoadUnsafe(ref yRef);
                    Vector128<float> end = Vector128.LoadUnsafe(ref xRef, remainder - (uint)(Vector128<float>.Count)) +
                                           Vector128.LoadUnsafe(ref yRef, remainder - (uint)(Vector128<float>.Count));
 
                    beg.StoreUnsafe(ref dRef);
                    end.StoreUnsafe(ref dRef, remainder - (uint)(Vector128<float>.Count));
 
                    break;
                }
 
                case 4:
                {
                    Debug.Assert(Vector128.IsHardwareAccelerated);
 
                    Vector128<float> beg = Vector128.LoadUnsafe(ref xRef) +
                                           Vector128.LoadUnsafe(ref yRef);
                    beg.StoreUnsafe(ref dRef);
 
                    break;
                }
 
                case 3:
                {
                    Unsafe.Add(ref dRef, 2) = Unsafe.Add(ref xRef, 2) +
                                              Unsafe.Add(ref yRef, 2);
                    goto case 2;
                }
 
                case 2:
                {
                    Unsafe.Add(ref dRef, 1) = Unsafe.Add(ref xRef, 1) +
                                              Unsafe.Add(ref yRef, 1);
                    goto case 1;
                }
 
                case 1:
                {
                    dRef = xRef + yRef;
                    goto case 0;
                }
 
                case 0:
                {
                    break;
                }
            }
        }
 
        static void Vectorized512(ref float xRef, ref float yRef, ref float dRef, nuint remainder)
        {
            ref float dRefBeg = ref dRef;
 
            // Preload the beginning and end so that overlapping accesses don't negatively impact the data
 
            Vector512<float> beg = Vector512.LoadUnsafe(ref xRef) +
                                   Vector512.LoadUnsafe(ref yRef);
            Vector512<float> end = Vector512.LoadUnsafe(ref xRef, remainder - (uint)(Vector512<float>.Count)) +
                                   Vector512.LoadUnsafe(ref yRef, remainder - (uint)(Vector512<float>.Count));
 
            if (remainder > (uint)(Vector512<float>.Count * 4))
            {
                // Pinning is cheap and will be short lived for small inputs and unlikely to be impactful
                // for large inputs (> 85KB) which are on the LOH and unlikely to be compacted.
 
                fixed (float* px = &xRef)
                fixed (float* py = &yRef)
                fixed (float* pd = &dRef)
                {
                    float* xPtr = px;
                    float* yPtr = py;
                    float* dPtr = pd;
 
                    // We need to the ensure the underlying data can be aligned and only align
                    // it if it can. It is possible we have an unaligned ref, in which case we
                    // can never achieve the required SIMD alignment.
 
                    bool canAlign = ((nuint)(dPtr) % sizeof(float)) == 0;
 
                    if (canAlign)
                    {
                        // Compute by how many elements we're misaligned and adjust the pointers accordingly
                        //
                        // Noting that we are only actually aligning dPtr. This is because unaligned stores
                        // are more expensive than unaligned loads and aligning both is significantly more
                        // complex.
 
                        nuint misalignment = ((uint)(sizeof(Vector512<float>)) - ((nuint)(dPtr) % (uint)(sizeof(Vector512<float>)))) / sizeof(float);
 
                        xPtr += misalignment;
                        yPtr += misalignment;
                        dPtr += misalignment;
 
                        Debug.Assert(((nuint)(dPtr) % (uint)(sizeof(Vector512<float>))) == 0);
 
                        remainder -= misalignment;
                    }
 
                    Vector512<float> vector1;
                    Vector512<float> vector2;
                    Vector512<float> vector3;
                    Vector512<float> vector4;
 
                    if ((remainder > (NonTemporalByteThreshold / sizeof(float))) && canAlign)
                    {
                        // This loop stores the data non-temporally, which benefits us when there
                        // is a large amount of data involved as it avoids polluting the cache.
 
                        while (remainder >= (uint)(Vector512<float>.Count * 4))
                        {
                            // We load, process, and store the first four vectors
 
                            vector1 = Vector512.Load(xPtr + (uint)(Vector512<float>.Count * 0)) +
                                      Vector512.Load(yPtr + (uint)(Vector512<float>.Count * 0));
                            vector2 = Vector512.Load(xPtr + (uint)(Vector512<float>.Count * 1)) +
                                      Vector512.Load(yPtr + (uint)(Vector512<float>.Count * 1));
                            vector3 = Vector512.Load(xPtr + (uint)(Vector512<float>.Count * 2)) +
                                      Vector512.Load(yPtr + (uint)(Vector512<float>.Count * 2));
                            vector4 = Vector512.Load(xPtr + (uint)(Vector512<float>.Count * 3)) +
                                      Vector512.Load(yPtr + (uint)(Vector512<float>.Count * 3));
 
                            vector1.StoreAlignedNonTemporal(dPtr + (uint)(Vector512<float>.Count * 0));
                            vector2.StoreAlignedNonTemporal(dPtr + (uint)(Vector512<float>.Count * 1));
                            vector3.StoreAlignedNonTemporal(dPtr + (uint)(Vector512<float>.Count * 2));
                            vector4.StoreAlignedNonTemporal(dPtr + (uint)(Vector512<float>.Count * 3));
 
                            // We adjust the source and destination references, then update
                            // the count of remaining elements to process.
 
                            xPtr += (uint)(Vector512<float>.Count * 4);
                            yPtr += (uint)(Vector512<float>.Count * 4);
                            dPtr += (uint)(Vector512<float>.Count * 4);
 
                            remainder -= (uint)(Vector512<float>.Count * 4);
                        }
                    }
                    else
                    {
                        while (remainder >= (uint)(Vector512<float>.Count * 4))
                        {
                            // We load, process, and store the first four vectors
 
                            vector1 = Vector512.Load(xPtr + (uint)(Vector512<float>.Count * 0)) +
                                      Vector512.Load(yPtr + (uint)(Vector512<float>.Count * 0));
                            vector2 = Vector512.Load(xPtr + (uint)(Vector512<float>.Count * 1)) +
                                      Vector512.Load(yPtr + (uint)(Vector512<float>.Count * 1));
                            vector3 = Vector512.Load(xPtr + (uint)(Vector512<float>.Count * 2)) +
                                      Vector512.Load(yPtr + (uint)(Vector512<float>.Count * 2));
                            vector4 = Vector512.Load(xPtr + (uint)(Vector512<float>.Count * 3)) +
                                      Vector512.Load(yPtr + (uint)(Vector512<float>.Count * 3));
 
                            vector1.Store(dPtr + (uint)(Vector512<float>.Count * 0));
                            vector2.Store(dPtr + (uint)(Vector512<float>.Count * 1));
                            vector3.Store(dPtr + (uint)(Vector512<float>.Count * 2));
                            vector4.Store(dPtr + (uint)(Vector512<float>.Count * 3));
 
                            // We adjust the source and destination references, then update
                            // the count of remaining elements to process.
 
                            xPtr += (uint)(Vector512<float>.Count * 4);
                            yPtr += (uint)(Vector512<float>.Count * 4);
                            dPtr += (uint)(Vector512<float>.Count * 4);
 
                            remainder -= (uint)(Vector512<float>.Count * 4);
                        }
                    }
 
                    // Adjusting the refs here allows us to avoid pinning for very small inputs
 
                    xRef = ref *xPtr;
                    yRef = ref *yPtr;
                    dRef = ref *dPtr;
                }
            }
 
            // Process the remaining [Count, Count * 4] elements via a jump table
            //
            // Unless the original length was an exact multiple of Count, then we'll
            // end up reprocessing a couple elements in case 1 for end. We'll also
            // potentially reprocess a few elements in case 0 for beg, to handle any
            // data before the first aligned address.
 
            nuint endIndex = remainder;
            remainder = (remainder + (uint)(Vector512<float>.Count - 1)) & (nuint)(-Vector512<float>.Count);
 
            switch (remainder / (uint)(Vector512<float>.Count))
            {
                case 4:
                {
                    Vector512<float> vector = Vector512.LoadUnsafe(ref xRef, remainder - (uint)(Vector512<float>.Count * 4)) +
                                              Vector512.LoadUnsafe(ref yRef, remainder - (uint)(Vector512<float>.Count * 4));
                    vector.StoreUnsafe(ref dRef, remainder - (uint)(Vector512<float>.Count * 4));
                    goto case 3;
                }
 
                case 3:
                {
                    Vector512<float> vector = Vector512.LoadUnsafe(ref xRef, remainder - (uint)(Vector512<float>.Count * 3)) +
                                              Vector512.LoadUnsafe(ref yRef, remainder - (uint)(Vector512<float>.Count * 3));
                    vector.StoreUnsafe(ref dRef, remainder - (uint)(Vector512<float>.Count * 3));
                    goto case 2;
                }
 
                case 2:
                {
                    Vector512<float> vector = Vector512.LoadUnsafe(ref xRef, remainder - (uint)(Vector512<float>.Count * 2)) +
                                              Vector512.LoadUnsafe(ref yRef, remainder - (uint)(Vector512<float>.Count * 2));
                    vector.StoreUnsafe(ref dRef, remainder - (uint)(Vector512<float>.Count * 2));
                    goto case 1;
                }
 
                case 1:
                {
                    // Store the last block, which includes any elements that wouldn't fill a full vector
                    end.StoreUnsafe(ref dRef, endIndex - (uint)Vector512<float>.Count);
                    goto case 0;
                }
 
                case 0:
                {
                    // Store the first block, which includes any elements preceding the first aligned block
                    beg.StoreUnsafe(ref dRefBeg);
                    break;
                }
            }
        }
 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Vectorized512Small(ref float xRef, ref float yRef, ref float dRef, nuint remainder)
        {
            switch (remainder)
            {
                case 15:
                case 14:
                case 13:
                case 12:
                case 11:
                case 10:
                case 9:
                {
                    Debug.Assert(Vector256.IsHardwareAccelerated);
 
                    Vector256<float> beg = Vector256.LoadUnsafe(ref xRef) +
                                           Vector256.LoadUnsafe(ref yRef);
                    Vector256<float> end = Vector256.LoadUnsafe(ref xRef, remainder - (uint)(Vector256<float>.Count)) +
                                           Vector256.LoadUnsafe(ref yRef, remainder - (uint)(Vector256<float>.Count));
 
                    beg.StoreUnsafe(ref dRef);
                    end.StoreUnsafe(ref dRef, remainder - (uint)(Vector256<float>.Count));
 
                    break;
                }
 
                case 8:
                {
                    Debug.Assert(Vector256.IsHardwareAccelerated);
 
                    Vector256<float> beg = Vector256.LoadUnsafe(ref xRef) +
                                           Vector256.LoadUnsafe(ref yRef);
                    beg.StoreUnsafe(ref dRef);
 
                    break;
                }
 
                case 7:
                case 6:
                case 5:
                {
                    Debug.Assert(Vector128.IsHardwareAccelerated);
 
                    Vector128<float> beg = Vector128.LoadUnsafe(ref xRef) +
                                           Vector128.LoadUnsafe(ref yRef);
                    Vector128<float> end = Vector128.LoadUnsafe(ref xRef, remainder - (uint)(Vector128<float>.Count)) +
                                           Vector128.LoadUnsafe(ref yRef, remainder - (uint)(Vector128<float>.Count));
 
                    beg.StoreUnsafe(ref dRef);
                    end.StoreUnsafe(ref dRef, remainder - (uint)(Vector128<float>.Count));
 
                    break;
                }
 
                case 4:
                {
                    Debug.Assert(Vector128.IsHardwareAccelerated);
 
                    Vector128<float> beg = Vector128.LoadUnsafe(ref xRef) +
                                           Vector128.LoadUnsafe(ref yRef);
                    beg.StoreUnsafe(ref dRef);
 
                    break;
                }
 
                case 3:
                {
                    Unsafe.Add(ref dRef, 2) = Unsafe.Add(ref xRef, 2) +
                                              Unsafe.Add(ref yRef, 2);
                    goto case 2;
                }
 
                case 2:
                {
                    Unsafe.Add(ref dRef, 1) = Unsafe.Add(ref xRef, 1) +
                                              Unsafe.Add(ref yRef, 1);
                    goto case 1;
                }
 
                case 1:
                {
                    dRef = xRef + yRef;
                    goto case 0;
                }
 
                case 0:
                {
                    break;
                }
            }
        }
    }
}