namespace BunnyLoader;

public readonly unsafe struct ScreenStats
{
    public long WorldFrames { get; init; }

    public long InterfaceFrames { get; init; }

    public long Passes { get; init; }

    public long Frames { get; init; }

    public long MarkersIssued { get; init; }

    public long MarkerHits { get; init; }

    public long ScreenFrames { get; init; }

    public long RenderCpuNs { get; init; }

    public long MarkerCpuNs { get; init; }

    public long GpuNs { get; init; }

    public long GpuSamples { get; init; }

    public long BoxPixels { get; init; }

    public long CopyFallbacks { get; init; }

    public bool HookInstalled { get; init; }

    public bool HasContext { get; init; }

    public bool MarkerReady { get; init; }

    public double GpuAverageMs => GpuSamples > 0 ? GpuNs / (double)GpuSamples / 1e6 : 0;

    internal static ScreenStats Read()
    {
        long* v = stackalloc long[24];
        for (int i = 0; i < 24; i++) v[i] = 0;
        Host.Native()->ScreenStats(v, 24);
        return new ScreenStats
        {
            WorldFrames = v[0],
            InterfaceFrames = v[1],
            Passes = v[3],
            Frames = v[4],
            MarkersIssued = v[5],
            MarkerHits = v[6],
            ScreenFrames = v[7],
            RenderCpuNs = v[8],
            MarkerCpuNs = v[9],
            GpuNs = v[10],
            GpuSamples = v[11],
            BoxPixels = v[14],
            CopyFallbacks = v[15],
            HookInstalled = v[16] != 0,
            HasContext = v[17] != 0,
            MarkerReady = v[18] != 0,
        };
    }

    public override string ToString() =>
        $"world {WorldFrames}, passes {Passes} in {Frames} frames, marker {(MarkerReady ? "ready" : "not ready")} ({MarkerHits}/{MarkersIssued}), gpu {GpuAverageMs:0.00} ms";
}
