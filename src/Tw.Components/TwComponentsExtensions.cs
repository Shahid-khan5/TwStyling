using Microsoft.Maui.Handlers;

namespace Tw.Components;

public static class TwComponentsExtensions
{
    /// <summary>
    /// Registers handler-mapper customizations for the Tw components. Mappers run at
    /// the handler layer — the MAUI-blessed way to adjust platform behavior without
    /// subclassing renderers.
    /// </summary>
    public static MauiAppBuilder UseTwComponents(this MauiAppBuilder builder)
    {
        // The platform's built-in hover/pressed chrome fights the pressed:/hover:
        // classes the components own. Neutralize it at the platform view so the
        // Tw visual states are the single source of interaction feedback.
        ButtonHandler.Mapper.AppendToMapping("TwButtonInteractionChrome", static (handler, view) =>
        {
            if (view is not TwButton)
                return;

#if WINDOWS
            // Writing theme resources during SetVirtualView throws COMExceptions in
            // WinUI 3 — defer to Loaded, when the control template exists, and treat
            // failure as cosmetic (the Tw visual states still win on press).
            var native = handler.PlatformView; // Microsoft.UI.Xaml.Controls.Button
            native.Loaded += static (sender, _) =>
            {
                try
                {
                    var button = (Microsoft.UI.Xaml.Controls.Button)sender;
                    if (button.Background is { } bg)
                    {
                        button.Resources["ButtonBackgroundPointerOver"] = bg;
                        button.Resources["ButtonBackgroundPressed"] = bg;
                    }
                    if (button.Foreground is { } fg)
                    {
                        button.Resources["ButtonForegroundPointerOver"] = fg;
                        button.Resources["ButtonForegroundPressed"] = fg;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Tw] button chrome tweak skipped: {ex.Message}");
                }
            };
#endif
        });

        return builder;
    }
}
