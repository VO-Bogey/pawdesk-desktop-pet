namespace PawDesk.App.Models;

public sealed class AppSettings
{
    public string CurrentPetImagePath { get; set; } = string.Empty;
    public double PetX { get; set; } = 300;
    public double PetY { get; set; } = 500;
    public double PetScale { get; set; } = 1.0;
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool AnimationEnabled { get; set; } = true;
    public bool IdleBreathingEnabled { get; set; } = true;
    public bool ClickBounceEnabled { get; set; } = true;
    public bool MouseReactionEnabled { get; set; } = true;
    public bool RandomMoveEnabled { get; set; } = true;
    public string LastOpenDirectory { get; set; } = string.Empty;
}
