using Gallery.Pages;

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
		var selfTest = Environment.GetEnvironmentVariable("TW_SELFTEST");
		Trace($"env page='{page}' shot='{shot}' selftest='{selfTest}'");

		Loaded += async (_, _) =>
		{
			try
			{
				Trace("loaded");

				// Headless verification: run the micro-bench + leak probe, write numbers, quit.
				if (!string.IsNullOrEmpty(selfTest))
				{
					await RunSelfTest(selfTest, shot);
					return;
				}

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

	/// <summary>Drives the Stress micro-bench and the Diagnostics leak probe on the real
	/// platform, writes their numbers to <paramref name="resultPath"/>, optionally screenshots
	/// the stress page, then quits.</summary>
	private async Task RunSelfTest(string resultPath, string? shot)
	{
		var report = new System.Text.StringBuilder();
		report.AppendLine($"Tw self-test {DateTime.Now:u}");

		await GoToAsync("//Stress");
		await Task.Delay(2500); // let 1000 cells realize + Loaded fire
		Trace("selftest: stress loaded");
		if (CurrentPage is StressPage stress)
			report.AppendLine("PERF  " + stress.RunMicroBench().Replace("\n", "\n      "));

		if (!string.IsNullOrEmpty(shot))
		{
			var capture = await Screenshot.CaptureAsync();
			await using var source = await capture.OpenReadAsync(ScreenshotFormat.Png);
			await using var file = File.Create(shot);
			await source.CopyToAsync(file);
			Trace("selftest: screenshot saved");
		}

		await GoToAsync("//Diagnostics");
		await Task.Delay(1500);
		Trace("selftest: diagnostics loaded");
		if (CurrentPage is DiagnosticsPage diagnostics)
			report.AppendLine("LEAK\n" + await diagnostics.RunProbeAsync());

		File.WriteAllText(resultPath, report.ToString());
		Trace("selftest: wrote report");
		Application.Current?.Quit();
	}
}
