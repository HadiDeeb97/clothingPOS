using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using ClothingStore.Core.Barcodes;

namespace ClothingStore.Desktop.Services;

public sealed record LabelData(string Title, string Subtitle, string Price, string Code);

/// <summary>Prints receipts/reports (monospaced text, auto-fitted to the printer width) and barcode price tags.</summary>
public sealed class PrintService
{
    private static readonly FontFamily Mono = new("Consolas, Courier New");

    public bool PrintText(IReadOnlyList<string> lines, string jobName)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return false;

        const double padding = 16;
        var width = dialog.PrintableAreaWidth;
        var longest = Math.Max(20, lines.Count == 0 ? 20 : lines.Max(l => l.Length));
        // Consolas glyphs are ~0.55em wide: shrink the font for narrow receipt printers, cap for A4.
        var fontSize = Math.Clamp((width - padding * 2) / (longest * 0.56), 6, 12);

        var doc = BuildTextDocument(lines, fontSize);
        doc.PageWidth = width;
        doc.PageHeight = dialog.PrintableAreaHeight;
        doc.ColumnWidth = width;
        doc.PagePadding = new Thickness(padding);
        dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, jobName);
        return true;
    }

    public static FlowDocument BuildTextDocument(IEnumerable<string> lines, double fontSize = 12)
    {
        var paragraph = new Paragraph { FontFamily = Mono, FontSize = fontSize, Margin = new Thickness(0) };
        var first = true;
        foreach (var line in lines)
        {
            if (!first) paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Run(line));
            first = false;
        }
        return new FlowDocument(paragraph) { FontFamily = Mono, PagePadding = new Thickness(0) };
    }

    /// <summary>
    /// Prints price tags. Sheet mode lays labels out 3-up on A4/Letter; label-printer mode prints one per page.
    /// </summary>
    public bool PrintLabels(IReadOnlyList<LabelData> labels, bool onePerPage)
    {
        if (labels.Count == 0) return false;
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return false;

        var pageWidth = dialog.PrintableAreaWidth;
        var pageHeight = dialog.PrintableAreaHeight;
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

        if (onePerPage)
        {
            foreach (var label in labels)
            {
                var page = NewPage(pageWidth, pageHeight);
                page.Children.Add(CreateLabel(label, pageWidth, pageHeight));
                AddPage(document, page);
            }
        }
        else
        {
            const int columns = 3;
            const double margin = 12;
            var labelWidth = (pageWidth - margin * 2) / columns;
            const double labelHeight = 125; // ~33mm
            var rows = Math.Max(1, (int)((pageHeight - margin * 2) / labelHeight));
            var perPage = rows * columns;

            for (var start = 0; start < labels.Count; start += perPage)
            {
                var page = NewPage(pageWidth, pageHeight);
                foreach (var (label, index) in labels.Skip(start).Take(perPage).Select((l, i) => (l, i)))
                {
                    var element = CreateLabel(label, labelWidth, labelHeight);
                    FixedPage.SetLeft(element, margin + index % columns * labelWidth);
                    FixedPage.SetTop(element, margin + index / columns * labelHeight);
                    page.Children.Add(element);
                }
                AddPage(document, page);
            }
        }

        dialog.PrintDocument(document.DocumentPaginator, "Price labels");
        return true;
    }

    private static FixedPage NewPage(double width, double height) => new() { Width = width, Height = height, Background = Brushes.White };

    private static void AddPage(FixedDocument document, FixedPage page)
    {
        var size = new Size(page.Width, page.Height);
        page.Measure(size);
        page.Arrange(new Rect(size));
        page.UpdateLayout();
        var content = new PageContent();
        ((IAddChild)content).AddChild(page);
        document.Pages.Add(content);
    }

    private static FrameworkElement CreateLabel(LabelData label, double width, double height)
    {
        const double pad = 8;
        var barcodeHeight = Math.Max(24, height * 0.32);
        var stack = new StackPanel { Margin = new Thickness(pad) };
        stack.Children.Add(new TextBlock
        {
            Text = label.Title, FontWeight = FontWeights.Bold, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis,
        });
        stack.Children.Add(new TextBlock { Text = label.Subtitle, FontSize = 9, TextTrimming = TextTrimming.CharacterEllipsis });
        stack.Children.Add(new TextBlock { Text = label.Price, FontWeight = FontWeights.Bold, FontSize = 15, Margin = new Thickness(0, 1, 0, 2) });

        var barcodeWidth = width - pad * 2;
        stack.Children.Add(new Path
        {
            Data = CreateBarcodeGeometry(label.Code, barcodeWidth, barcodeHeight),
            Fill = Brushes.Black,
            Width = barcodeWidth,
            Height = barcodeHeight,
            SnapsToDevicePixels = true,
        });
        stack.Children.Add(new TextBlock
        {
            Text = label.Code, FontFamily = Mono, FontSize = 8, HorizontalAlignment = HorizontalAlignment.Center,
        });

        return new Border
        {
            Width = width,
            Height = height,
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0.5),
            Child = stack,
        };
    }

    /// <summary>Code 128 bars scaled to fit <paramref name="width"/> with a 10-module quiet zone each side.</summary>
    public static Geometry CreateBarcodeGeometry(string code, double width, double height)
    {
        var widths = Code128.EncodeWidths(code);
        var modules = widths.Sum() + 20;
        var module = width / modules;
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        var x = 10 * module;
        for (var i = 0; i < widths.Count; i++)
        {
            var w = widths[i] * module;
            if (i % 2 == 0) group.Children.Add(new RectangleGeometry(new Rect(x, 0, w, height)));
            x += w;
        }
        group.Freeze();
        return group;
    }
}
