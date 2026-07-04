namespace Gallery;

public partial class AppShell : Shell
{
	private static void Trace(string msg) =>
		File.AppendAllText(Path.Combine(Path.GetTempPath(), "tw-gallery-trace.txt"), $"{msg} {DateTime.Now:HH:mm:ss.fff}\n");

	public AppShell()
	{
		InitializeComponent();
		Trace("shell");

		// Test hooks used by screenshot-based verification:
		//   TW_PAGE=Colors|Typography|Spacing|Utilities|Comparison — open on that page
		//   TW_SHOT=<path.png> — self-capture after load, save, and quit
		var page = Environment.GetEnvironmentVariable("TW_PAGE");
		var shot = Environment.GetEnvironmentVariable("TW_SHOT");
		Trace($"env page='{page}' shot='{shot}'");

		Loaded += async (_, _) =>
		{
			try
			{
				Trace("loaded");
				if (!string.IsNullOrEmpty(page) && page != "Components")
					await GoToAsync($"//{page}"); // navigating to the already-current route closes the window (Shell quirk)
				Trace("navigated");

				if (!string.IsNullOrEmpty(shot))
				{
					await Task.Delay(page == "Comparison" ? 15000 : 4000); // let WebView/CDN settle
					Trace("capturing");
					var capture = await Screenshot.CaptureAsync();
					await using var source = await capture.OpenReadAsync(ScreenshotFormat.Png);
					await using var file = File.Create(shot);
					await source.CopyToAsync(file);
					Trace("captured");
					Application.Current?.Quit();
				}
			}
			catch (Exception ex)
			{
				Trace($"HOOK FAILED: {ex}");
			}
		};
	}
}
