using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using TwStyling.Maui;

namespace Gallery;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		File.AppendAllText(Path.Combine(Path.GetTempPath(), "tw-gallery-trace.txt"), $"boot {DateTime.Now:HH:mm:ss.fff}\n");
		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			File.WriteAllText(Path.Combine(Path.GetTempPath(), "tw-gallery-crash.txt"), e.ExceptionObject?.ToString());
		TaskScheduler.UnobservedTaskException += (_, e) =>
			File.WriteAllText(Path.Combine(Path.GetTempPath(), "tw-gallery-crash.txt"), e.Exception.ToString());

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseTw(o => o.DiagnosticMode = TwDiagnosticMode.Throw)
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if WINDOWS
		builder.ConfigureLifecycleEvents(events => events.AddWindows(windows => windows.OnWindowCreated(_ =>
		{
			Microsoft.UI.Xaml.Application.Current.UnhandledException += (_, e) =>
			{
				File.WriteAllText(Path.Combine(Path.GetTempPath(), "tw-gallery-crash.txt"),
					$"{e.Message}\n{e.Exception}");
				e.Handled = false;
			};
		})));
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
