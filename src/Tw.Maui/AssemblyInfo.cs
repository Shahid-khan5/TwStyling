using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;

// Lets consumers write xmlns:tw="https://tw" instead of the clr-namespace form.
[assembly: XmlnsDefinition("https://tw", "Tw.Maui")]

// Benchmarks measure the internal lowering path (TwMauiPlan.Get).
[assembly: InternalsVisibleTo("Tw.Benchmarks")]
