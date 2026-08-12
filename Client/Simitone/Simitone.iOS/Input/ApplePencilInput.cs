using CoreGraphics;
using Foundation;
using UIKit;

namespace Simitone.iOS.Input;

public readonly record struct ApplePencilSample(
    CGPoint Location,
    float Pressure,       // 0-1 typical range; can exceed 1 under extra-hard presses (see UITouch.Force docs)
    float AltitudeAngle,  // radians: 0 = pencil flat against the glass, pi/2 = perfectly upright
    float AzimuthAngle,   // radians: compass direction the pencil is pointing, relative to the view
    UITouchPhase Phase);

/// <summary>
/// Raises Apple Pencil-specific touch data (pressure, tilt) that MonoGame's own
/// TouchPanel abstraction doesn't expose. Populated by ApplePencilOverlay.
/// Build-mode tools (wall/terrain drawing etc.) can subscribe to Sample to get
/// pencil precision where it matters; everything else can ignore this and keep
/// using normal MonoGame touch input, which keeps working unmodified because the
/// overlay forwards every touch through to the game view underneath.
/// </summary>
public static class ApplePencilInput
{
    public static event Action<ApplePencilSample>? Sample;
    public static bool IsPencilActive { get; private set; }

    internal static void Update(NSSet touches, UIView relativeTo)
    {
        foreach (var obj in touches)
        {
            // UITouchType.Stylus is the actual enum member for Apple Pencil
            // (and other stylus input) - there is no "Pencil" member.
            if (obj is not UITouch touch || touch.Type != UITouchType.Stylus)
                continue;

            IsPencilActive = touch.Phase is not (UITouchPhase.Ended or UITouchPhase.Cancelled);

            var maxForce = (float)touch.MaximumPossibleForce;
            var sample = new ApplePencilSample(
                Location: touch.LocationInView(relativeTo),
                Pressure: maxForce > 0 ? (float)touch.Force / maxForce : 0f,
                AltitudeAngle: (float)touch.AltitudeAngle,
                AzimuthAngle: (float)touch.GetAzimuthAngle(relativeTo),
                Phase: touch.Phase);

            Sample?.Invoke(sample);
        }
    }
}
