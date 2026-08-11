using Foundation;
using UIKit;

namespace Simitone.iOS.Input;

/// <summary>
/// A transparent view placed on top of MonoGame's game view purely to read Apple
/// Pencil touch metadata (pressure/tilt) that UIKit exposes but MonoGame's
/// touch-panel abstraction doesn't. Every touch it receives - pencil or finger -
/// is forwarded on to the underlying game view immediately after being read, so
/// normal touch-driven gameplay/UI is unaffected; this only ever adds data, it
/// never consumes or blocks a touch.
///
/// UNVERIFIED: written against documented UIView/UITouch APIs but never
/// compiled (no Xcode available in the environment this was written in). The
/// specific assumption that needs confirming on a real device/simulator: that
/// UIApplication.SharedApplication.KeyWindow.RootViewController.View, read
/// right after Game.Run() returns, actually is MonoGame's real touch-handling
/// view and not an intermediate container - if MonoGame wraps it in an extra
/// container view, TouchesBegan/Moved/Ended forwarding here would need to walk
/// down to the real subview instead of calling straight through.
/// </summary>
public sealed class ApplePencilOverlay : UIView
{
    private readonly UIView _target;

    private ApplePencilOverlay(UIView target)
    {
        _target = target;
    }

    /// <summary>Attaches a pencil-capturing overlay on top of <paramref name="target"/>.</summary>
    public static ApplePencilOverlay Attach(UIView target)
    {
        var overlay = new ApplePencilOverlay(target)
        {
            Frame = target.Bounds,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
            BackgroundColor = UIColor.Clear,
            UserInteractionEnabled = true,
            MultipleTouchEnabled = true,
        };

        target.AddSubview(overlay);
        return overlay;
    }

    public override void TouchesBegan(NSSet touches, UIEvent? evt)
    {
        ApplePencilInput.Update(touches, _target);
        _target.TouchesBegan(touches, evt);
    }

    public override void TouchesMoved(NSSet touches, UIEvent? evt)
    {
        ApplePencilInput.Update(touches, _target);
        _target.TouchesMoved(touches, evt);
    }

    public override void TouchesEnded(NSSet touches, UIEvent? evt)
    {
        ApplePencilInput.Update(touches, _target);
        _target.TouchesEnded(touches, evt);
    }

    public override void TouchesCancelled(NSSet touches, UIEvent? evt)
    {
        ApplePencilInput.Update(touches, _target);
        _target.TouchesCancelled(touches, evt);
    }
}
