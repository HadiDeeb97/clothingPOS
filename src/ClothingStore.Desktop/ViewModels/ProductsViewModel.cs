using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

public sealed record ProductRow(Product Product)
{
    public int TotalStock => Product.Variants.Where(v => v.IsActive).Sum(v => v.StockQuantity);
    public int VariantCount => Product.Variants.Count(v => v.IsActive);
    public string Sizes => string.Join(", ", Product.Variants.Where(v => v.IsActive).Select(v => v.Size).Where(s => s.Length > 0).Distinct());
    public string Colors => string.Join(", ", Product.Variants.Where(v => v.IsActive).Select(v => v.Color).Where(s => s.Length > 0).Distinct());
    public bool HasLowStock => Product.Variants.Any(v => v.IsActive && v.IsLowStock);
}

public sealed partial class ProductsViewModel(
    IDialogService dialogs, ProductService products, CategoryService categories, SupplierService suppliers,
    Session session, PrintService print) : ViewModelBase(dialogs), IPageViewModel
{
    public static readonly Category AllCategories = new() { Id = 0, Name = "All categories" };

    public string Title => "Products";
    public bool CanEdit => session.Can(Permission.ManageProducts);

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial List<Category> Categories { get; set; } = [AllCategories];

    [ObservableProperty]
    public partial Category SelectedCategory { get; set; } = AllCategories;

    [ObservableProperty]
    public partial bool IncludeInactive { get; set; }

    [ObservableProperty]
    public partial List<ProductRow> Products { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand), nameof(PrintLabelsCommand))]
    public partial ProductRow? SelectedProduct { get; set; }

    public async Task OnNavigatedToAsync()
    {
        Categories = [AllCategories, .. await categories.GetAllAsync()];
        await SearchAsync();
    }

    private readonly LatestSearch _search = new();

    partial void OnSearchTextChanged(string value) => _ = LoadAsync(immediately: false);
    partial void OnSelectedCategoryChanged(Category value) => _ = SearchAsync();
    partial void OnIncludeInactiveChanged(bool value) => _ = SearchAsync();

    [RelayCommand]
    private Task SearchAsync() => LoadAsync(immediately: true);

    private async Task LoadAsync(bool immediately)
    {
        var selectedId = SelectedProduct?.Product.Id;
        var text = SearchText;
        var categoryId = SelectedCategory is { Id: > 0 } c ? c.Id : (int?)null;
        var includeInactive = IncludeInactive;
        try
        {
            Func<CancellationToken, Task<List<Product>>> load = ct => products.SearchAsync(text, categoryId, includeInactive, ct);
            Action<List<Product>> apply = found =>
            {
                Products = found.Select(p => new ProductRow(p)).ToList();
                SelectedProduct = Products.FirstOrDefault(p => p.Product.Id == selectedId) ?? Products.FirstOrDefault();
            };
            await (immediately ? _search.RunNowAsync(load, apply) : _search.RunAsync(load, apply));
        }
        catch (Exception ex)
        {
            Dialogs.Error("Product search failed.", ex);
        }
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!CanEdit) return;
        var editor = await ProductEditorViewModel.CreateAsync(Dialogs, products, categories, suppliers, session, null);
        if (Dialogs.ShowDialog(editor)) await SearchAsync();
    }

    private bool HasSelection() => SelectedProduct is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedProduct is null || !CanEdit) return;
        var product = await products.GetAsync(SelectedProduct.Product.Id);
        if (product is null) return;
        var editor = await ProductEditorViewModel.CreateAsync(Dialogs, products, categories, suppliers, session, product);
        if (Dialogs.ShowDialog(editor)) await SearchAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedProduct is not { } row || !CanEdit) return;
        if (!Dialogs.Confirm($"Delete '{row.Product.Name}'?\n\nProducts that have been sold or ordered are deactivated instead, so history is kept."))
            return;
        var deleted = false;
        if (await RunAsync(async () => deleted = await products.DeleteAsync(row.Product.Id)))
        {
            if (!deleted) Dialogs.Info("The product has sales or purchase history, so it was deactivated instead.");
            await SearchAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void PrintLabels()
    {
        if (SelectedProduct is null) return;
        Dialogs.ShowDialog(new LabelPrintViewModel(Dialogs, print, SelectedProduct.Product.Variants.Where(v => v.IsActive)
            .Select(v => { v.Product = SelectedProduct.Product; return v; }).ToList()));
    }
}

public sealed partial class LabelRowViewModel(ProductVariant variant, int copies) : ObservableObject
{
    public ProductVariant Variant { get; } = variant;

    [ObservableProperty]
    public partial int Copies { get; set; } = copies;
}

/// <summary>Choose how many price tags to print for each variant.</summary>
public sealed partial class LabelPrintViewModel(IDialogService dialogs, PrintService print, IReadOnlyList<ProductVariant> variants)
    : DialogViewModelBase(dialogs)
{
    public override string Title => "Print price labels";
    public List<LabelRowViewModel> Rows { get; } = variants.Select(v => new LabelRowViewModel(v, 1)).ToList();

    [ObservableProperty]
    public partial bool OnePerPage { get; set; }

    [RelayCommand]
    private void CopiesFromStock()
    {
        foreach (var row in Rows) row.Copies = Math.Max(0, row.Variant.StockQuantity);
    }

    [RelayCommand]
    private void Print()
    {
        var labels = Rows.SelectMany(r => Enumerable.Repeat(new LabelData(
            r.Variant.Product?.Name ?? "",
            $"{r.Variant.Description}  {r.Variant.Sku}",
            CurrencyFormat.Format(r.Variant.EffectivePrice),
            r.Variant.Barcode ?? r.Variant.Sku), Math.Max(0, r.Copies))).ToList();
        if (labels.Count == 0)
        {
            Dialogs.Warning("Set at least one copy.");
            return;
        }
        try
        {
            if (print.PrintLabels(labels, OnePerPage)) Close(true);
        }
        catch (Exception ex)
        {
            Dialogs.Error("Printing failed.", ex);
        }
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}
