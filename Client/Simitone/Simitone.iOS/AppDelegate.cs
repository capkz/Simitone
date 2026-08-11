using Foundation;
using Simitone.iOS.Input;
using Simitone.iOS.Sync;
using UIKit;
using FSO.Client;
using FSO.Common;
using FSO.LotView;
using Simitone.Client;

namespace Simitone.iOS;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    private UIWindow? _window;
    private SimitoneGame? _game;

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        var gamePath = GameFilesStore.ResolveGamePath();

        if (gamePath != null)
        {
            StartGame(gamePath);
        }
        else
        {
            ShowSyncScreen();
        }

        return true;
    }

    private void ShowSyncScreen()
    {
        _window = new UIWindow(UIScreen.MainScreen.Bounds);

        var sync = new SyncViewController();
        sync.SyncCompleted += gamePath =>
        {
            _window!.RootViewController = null;
            StartGame(gamePath);
        };

        _window.RootViewController = sync;
        _window.MakeKeyAndVisible();
    }

    private void StartGame(string gamePath)
    {
        ConfigureEnvironment(gamePath);

        _game = new SimitoneGame();

        // GameRunBehavior.Asynchronous is required on iOS - MonoGame's iOS backend
        // drives the loop from a CADisplayLink integrated into UIKit's own run
        // loop, it doesn't block Run() the way Desktop/Windows do. See the old
        // (dead since 2019, but still structurally correct) FreeSO FSO.iOS/Main.cs
        // for the reference this is adapted from.
        _game.Run(Microsoft.Xna.Framework.GameRunBehavior.Asynchronous);

        AttachPencilOverlay();
    }

    private static void AttachPencilOverlay()
    {
        // Deliberately not reaching into any MonoGame-internal type name here -
        // this only depends on the game having become the key window's root view
        // after Run(), which is standard MonoGame iOS behavior. See the
        // UNVERIFIED note on ApplePencilOverlay for what to check first if pencil
        // input doesn't show up on a real device.
        var gameView = UIApplication.SharedApplication.KeyWindow?.RootViewController?.View;
        if (gameView != null)
            ApplePencilOverlay.Attach(gameView);
    }

    private static void ConfigureEnvironment(string gamePath)
    {
        var iPad = UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad;
        var scale = (float)UIScreen.MainScreen.Scale;

        FSOEnvironment.ContentDir = "Content/";
        FSOEnvironment.GFXContentDir = "Content/OGL/";
        FSOEnvironment.UserDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/";
        FSOEnvironment.Linux = true; // MonoGame iOS backend is the OpenGL/"Linux-like" code path, not DirectX
        FSOEnvironment.DirectX = false;
        FSOEnvironment.GameThread = Thread.CurrentThread;
        FSOEnvironment.Enable3D = false;

        // Touch-platform tuning carried over from FreeSO's old FSO.iOS project -
        // these flags exist specifically for non-desktop, non-mouse-driven,
        // GLES-constrained platforms.
        FSOEnvironment.SoftwareKeyboard = true;
        FSOEnvironment.SoftwareDepth = true;
        FSOEnvironment.EnableNPOTMip = true;
        FSOEnvironment.GLVer = 2;
        FSOEnvironment.UseMRT = false;
        FSOEnvironment.TexCompress = false;
        FSOEnvironment.TexCompressSupport = false;
        FSOEnvironment.DPIScaleFactor = scale;
        FSOEnvironment.UIZoomFactor = iPad ? 1 : 2;

        FSO.Files.ImageLoaderHelpers.BitmapFunction = ImageDecoding.BitmapReader;

        GlobalSettings.Default.GraphicsWidth = (int)UIScreen.MainScreen.Bounds.Width;
        GlobalSettings.Default.GraphicsHeight = (int)UIScreen.MainScreen.Bounds.Height;
        GlobalSettings.Default.TargetRefreshRate = 60;
        GlobalSettings.Default.CurrentLang = "english";
        if (GlobalSettings.Default.LanguageCode == 0) GlobalSettings.Default.LanguageCode = 1;
        GlobalSettings.Default.Lighting = true;
        GlobalSettings.Default.LightingMode = 3;
        GlobalSettings.Default.AntiAlias = 0;
        GlobalSettings.Default.ComplexShaders = true;
        GlobalSettings.Default.EnableTransitions = true;

        GlobalSettings.Default.StartupPath = gamePath;
        GlobalSettings.Default.TS1HybridEnable = true;
        GlobalSettings.Default.TS1HybridPath = gamePath;
        GlobalSettings.Default.ClientVersion = "0";

        GameFacade.DirectX = false;
        World.DirectX = false;

        // HIGH-RISK / UNVERIFIED: iOS forbids JIT (no executable-memory
        // allocation outside a special, unavailable-to-us entitlement), so
        // SimAntics VM opcode execution must run through the AOT path, not the
        // JIT path Desktop uses by default (Simitone.Desktop only calls
        // InitAOT() when passed -jit; here it's unconditional because there is
        // no other option on this platform). This has NOT been confirmed to
        // produce zero runtime codegen internally - if the game crashes with a
        // codesign/JIT-related trap on device, this is the first place to look.
        var assemblies = new FSO.SimAntics.JIT.Runtime.AssemblyStore();
        assemblies.InitAOT();
        FSO.SimAntics.Engine.VMTranslator.INSTANCE = new FSO.SimAntics.JIT.Runtime.VMAOTTranslator(assemblies);
    }
}
