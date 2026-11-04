namespace Terraria.GameContent;

public partial struct PlayerEyeHelper
{
    public EyeState CurrentEyeState { get => _state; set => _state = value; }
    public EyeFrame CurrentEyeFrame { get => (EyeFrame)EyeFrameToShow; set => EyeFrameToShow = (int)value; }
    public int TimeInState { get => _timeInState; set => _timeInState = value; }
}
