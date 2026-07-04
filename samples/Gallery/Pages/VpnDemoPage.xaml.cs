namespace Gallery.Pages;

public partial class VpnDemoPage : ContentPage
{
	private readonly IDispatcherTimer _autoAdvance;
	private Border[] _dots = [];

	public VpnDemoPage()
	{
		InitializeComponent();
		_dots = [Dot0, Dot1, Dot2, Dot3];

		// Auto-advance: CarouselView animates position changes, so this is the
		// only imperative piece — everything visual lives in the XAML.
		_autoAdvance = Dispatcher.CreateTimer();
		_autoAdvance.Interval = TimeSpan.FromSeconds(3.5);
		_autoAdvance.Tick += (_, _) => Carousel.Position = (Carousel.Position + 1) % _dots.Length;
		Loaded += (_, _) => _autoAdvance.Start();
		Unloaded += (_, _) => _autoAdvance.Stop();
	}

	private void OnDotTapped(object? sender, TappedEventArgs e)
	{
		if (sender is not Border dot || !int.TryParse(dot.ClassId, out int index))
			return;
		Carousel.Position = index;

		// Manual navigation restarts the auto-advance countdown.
		_autoAdvance.Stop();
		_autoAdvance.Start();
	}

	private void OnCarouselPositionChanged(object? sender, PositionChangedEventArgs e)
	{
		// The active dot's ActiveClass (blue pill) animates in via transition-all.
		for (int i = 0; i < _dots.Length; i++)
			Tw.Maui.Tw.SetIsActive(_dots[i], i == e.CurrentPosition);
	}
}

/// <summary>One carousel slide (instantiated from x:Array in the XAML).</summary>
public class VpnSlide
{
	public string Glyph { get; set; } = "";
	public string Title { get; set; } = "";
	public string Subtitle { get; set; } = "";
}
