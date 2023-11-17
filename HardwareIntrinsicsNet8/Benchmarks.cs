using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[DisassemblyDiagnoser(maxDepth: 3)]
public partial class Benchmarks
{
    private const int MaxSize = 65536;

    private static readonly Random s_random = new Random(20231116);

    // For testing purposes, get a mix of negative and positive values.
    private static float NextSingle() => (s_random.NextSingle() * 2) - 1;

    private static readonly float[] s_x = new float[MaxSize];
    private static readonly float[] s_y = new float[MaxSize];
    private static readonly float[] s_destination = new float[MaxSize];

    public Span<float> Destination => s_destination.AsSpan(0, Size);

    public Span<float> X => s_x.AsSpan(0, Size);

    public Span<float> Y => s_y.AsSpan(0, Size);

    [Params(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536)]
    public int Size { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        FillArray(s_x);
        FillArray(s_y);
    }

    private void FillArray(float[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = NextSingle();
        }
    }

    private void ThrowIfSpansOverlapAndAreNotSame(ReadOnlySpan<float> span1, ReadOnlySpan<float> span2)
    {
        if (!Unsafe.AreSame(ref MemoryMarshal.GetReference(span1), ref MemoryMarshal.GetReference(span2)) &&
            span1.Overlaps(span2))
        {
            ThrowArgumentExceptionForOverlap();
        }
    }

    private void ThrowArgumentExceptionForOverlap() => throw new ArgumentException();
}
