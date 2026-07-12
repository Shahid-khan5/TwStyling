using System.Runtime.CompilerServices;

// The CSS layer is compiled into this assembly as an internal implementation detail
// (see TwStyling.csproj); its own test project needs to see it.
[assembly: InternalsVisibleTo("TwStyling.Tests")]
[assembly: InternalsVisibleTo("TwStyling.Css.Tests")]
