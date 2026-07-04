using System.Net;
using System.Text;
using Tw.Maui;

namespace Gallery.Pages;

/// <summary>
/// The identity proof: one spec tree, two renderers. The left pane builds native
/// MAUI controls with .Tw(classes); the right pane builds HTML with class="..."
/// rendered by real Tailwind CSS (Play CDN) in a WebView. Same strings, byte for
/// byte — if the engine is faithful, the panes match.
/// </summary>
public class ComparisonPage : ContentPage
{
    private sealed record Node(string Kind, string Classes, string Text = "", Node[]? Children = null);

    // ------------------------------------------------------------ the shared spec

    private static readonly Node Spec = new("col", "gap-4", Children:
    [
        new("text", "text-2xl font-bold text-slate-900", "Same classes, two runtimes"),
        new("text", "text-sm text-slate-500",
            "Native MAUI on the left, real Tailwind CSS in a WebView on the right. Both sides are generated from one spec — every class string is shared."),

        new("card", "bg-white rounded-2xl shadow-md p-6", Children:
        [
            new("col", "gap-3", Children:
            [
                new("text", "text-xl font-bold text-slate-900", "Upgrade to Pro"),
                new("text", "text-sm text-slate-500", "Unlock unlimited projects, priority support, and advanced analytics."),
                new("button", "bg-indigo-600 text-white font-semibold rounded-lg px-4 py-3 mt-2", "Get started"),
            ]),
        ]),

        new("row", "gap-2", Children:
        [
            new("text", "bg-emerald-100 text-emerald-700 text-xs font-medium rounded-full px-3 py-1", "Active"),
            new("text", "bg-amber-100 text-amber-700 text-xs font-medium rounded-full px-3 py-1", "Beta"),
            new("text", "bg-sky-100 text-sky-700 text-xs font-medium rounded-full px-3 py-1", "v0.1"),
        ]),

        new("row", "gap-3", Children:
        [
            new("button", "bg-slate-900 text-white font-medium rounded-lg px-4 py-2", "Primary"),
            new("button", "bg-red-600 text-white font-medium rounded-lg px-4 py-2", "Delete"),
        ]),

        new("text", "text-xs text-slate-400", "gradient · rounded · shadows · opacity · type scale"),

        new("box", "bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500 rounded-xl h-10 w-full"),

        new("row", "gap-3", Children:
        [
            new("box", "bg-blue-500 size-10 rounded-none"),
            new("box", "bg-blue-500 size-10 rounded-md"),
            new("box", "bg-blue-500 size-10 rounded-xl"),
            new("box", "bg-blue-500 size-10 rounded-full"),
        ]),

        new("row", "gap-6 p-2", Children:
        [
            new("card", "bg-white shadow-sm rounded-xl size-14"),
            new("card", "bg-white shadow-md rounded-xl size-14"),
            new("card", "bg-white shadow-xl rounded-xl size-14"),
        ]),

        new("row", "gap-3", Children:
        [
            new("box", "bg-indigo-600 size-10 rounded-lg opacity-100"),
            new("box", "bg-indigo-600 size-10 rounded-lg opacity-75"),
            new("box", "bg-indigo-600 size-10 rounded-lg opacity-50"),
            new("box", "bg-indigo-600 size-10 rounded-lg opacity-25"),
        ]),

        new("col", "gap-1", Children:
        [
            new("text", "text-xs text-slate-900", "text-xs — the quick brown fox"),
            new("text", "text-sm text-slate-900", "text-sm — the quick brown fox"),
            new("text", "text-base text-slate-900", "text-base — the quick brown fox"),
            new("text", "text-lg text-slate-900", "text-lg — the quick brown fox"),
            new("text", "text-2xl font-bold text-slate-900", "text-2xl bold — the fox"),
        ]),
    ]);

    // ------------------------------------------------------------ page assembly

    public ComparisonPage()
    {
        Title = "Web vs Native";
        this.Tw("bg-slate-50");

        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
        }.Tw("gap-x-2");

        var nativeHeader = new Label { Text = "NATIVE — Tw engine on MAUI" }
            .Tw("text-xs font-bold tracking-wider text-indigo-600 p-3");
        var webHeader = new Label { Text = "WEB — Tailwind CSS in a WebView (needs internet)" }
            .Tw("text-xs font-bold tracking-wider text-emerald-600 p-3");
        grid.Add(nativeHeader, 0, 0);
        grid.Add(webHeader, 1, 0);

        var native = new ScrollView { Content = ((View)BuildNative(Spec)).Tw("p-6") };
        grid.Add(native, 0, 1);

        var web = new WebView { Source = new HtmlWebViewSource { Html = BuildHtmlDocument(Spec) } };
        grid.Add(web, 1, 1);

        Content = grid;
    }

    // ------------------------------------------------------------ native renderer

    private static View BuildNative(Node node)
    {
        switch (node.Kind)
        {
            case "text":
                return new Label { Text = node.Text }.Tw(node.Classes);
            case "button":
                return new Button { Text = node.Text }.Tw(node.Classes);
            case "box":
                return new BoxView().Tw(node.Classes);
            case "card":
                var border = new Border().Tw(node.Classes + " border-0");
                if (node.Children is [var only])
                    border.Content = BuildNative(only);
                return border;
            case "row":
            case "col":
                Microsoft.Maui.Controls.StackBase stack = node.Kind == "row"
                    ? new HorizontalStackLayout().Tw(node.Classes)
                    : new VerticalStackLayout().Tw(node.Classes);
                foreach (var child in node.Children ?? [])
                    ((Layout)stack).Children.Add(BuildNative(child));
                return (View)stack;
            default:
                throw new InvalidOperationException($"unknown node kind '{node.Kind}'");
        }
    }

    // ------------------------------------------------------------ web renderer

    private static string BuildHtmlDocument(Node spec)
    {
        var body = new StringBuilder();
        AppendHtml(spec, body);
        return $$"""
            <!doctype html>
            <html>
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <script src="https://cdn.tailwindcss.com"></script>
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link href="https://fonts.googleapis.com/css2?family=Open+Sans:ital,wght@0,300..800;1,300..800&display=swap" rel="stylesheet">
              <script>tailwind.config = { theme: { fontFamily: { sans: ['Open Sans','Segoe UI','sans-serif'] } } }</script>
            </head>
            <body class="bg-slate-50 p-6">
            {{body}}
            </body>
            </html>
            """;
    }

    private static void AppendHtml(Node node, StringBuilder html)
    {
        string classes = node.Classes;
        switch (node.Kind)
        {
            case "text":
                html.Append($"<div class=\"{classes}\">{WebUtility.HtmlEncode(node.Text)}</div>\n");
                return;
            case "button":
                html.Append($"<button class=\"{classes}\">{WebUtility.HtmlEncode(node.Text)}</button>\n");
                return;
            case "box":
                html.Append($"<div class=\"{classes}\"></div>\n");
                return;
            case "card":
                html.Append($"<div class=\"{classes}\">\n");
                break;
            case "row":
                html.Append($"<div class=\"flex flex-row items-start {classes}\">\n");
                break;
            case "col":
                html.Append($"<div class=\"flex flex-col {classes}\">\n");
                break;
        }
        foreach (var child in node.Children ?? [])
            AppendHtml(child, html);
        html.Append("</div>\n");
    }
}
