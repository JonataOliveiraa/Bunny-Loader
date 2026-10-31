namespace Terraria.DataStructures;

public struct ExtraJumpState
{
    internal bool _enabled;
    internal bool _available;
    internal bool _active;
    internal bool _disabled;

    public bool Enabled => _enabled && !_disabled;

    public bool Available
    {
        get => Enabled && _available;
        set => _available = value;
    }

    public bool Active => Enabled && _active;

    public void Enable() => _enabled = true;

    public void Disable() => _disabled = true;

    internal void Start()
    {
        _available = false;
        _active = true;
    }

    internal void Stop()
    {
        _active = false;
    }

    internal void ResetEnabled()
    {
        _enabled = false;
        _disabled = false;
    }

    internal void CommitEnabledState(out bool jumpEnded)
    {
        jumpEnded = false;
        if (Enabled) return;
        jumpEnded = _active;
        _active = false;
        _available = false;
    }
}
