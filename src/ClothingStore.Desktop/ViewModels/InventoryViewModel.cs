using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

public sealed partial class InventoryViewModel(
    IDialogService dialogs, InventoryService inventory, CategoryService categories, ReportService reports,
    Session session, PrintService print) : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => "Inventory";

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial List<Category> Categories { get; set; } = [ProductsViewModel.AllCategories];

    [ObservableProperty]
    public partial Category SelectedCategory { get; set; } = ProductsViewModel.AllCategories;

    [ObservableProperty]
    public partial bool LowStockOnly { get; set; }

    [ObservableProperty]
    public partial List<ProductVariant> Items { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AdjustCommand), nameof(CountCommand), nameof(ReorderLevelCommand))]
    public partial ProductVariant? SelectedItem { get; set; }

    [ObservableProperty]
    public partial List<StockMovement> Movements { get; set; } = [];

    [ObservableProperty]
    public partial InventoryValuation? Valuation { get; set; }

    public async Task OnNavigatedToAsync()
    {
        Categories = [ProductsViewModel.AllCategories, .. await categories.GetAllAsync()];
        await RefreshAsync();
    }

    private readonly LatestSearch _search = new();

    partial void OnSearchTextChanged(string value) => _ = LoadItemsAsync(immediately: false);
    partial void OnSelectedCategoryChanged(Category value) => _ = LoadItemsAsync();
    partial void OnLowStockOnlyChanged(bool value) => _ = LoadItemsAsync();

    async partial void OnSelectedItemChanged(ProductVariant? value)
    {
        try
        {
            Movements = value is null ? [] : await inventory.GetMovementsAsync(value.Id, max: 200);
        }
        catch (Exception ex)
        {
            Dialogs.Error("Could not load stock history.", ex);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadItemsAsync();
        try
        {
            Valuation = await reports.GetInventoryValuationAsync();
        }
        catch (Exception ex)
        {
            Dialogs.Error("Could not load stock valuation.", ex);
        }
    }

    private async Task LoadItemsAsync(bool immediately = true)
    {
        var selectedId = SelectedItem?.Id;
        var text = SearchText;
        var categoryId = SelectedCategory is { Id: > 0 } c ? c.Id : (int?)null;
        var lowStockOnly = LowStockOnly;
        try
        {
            Func<CancellationToken, Task<List<ProductVariant>>> load = ct => inventory.GetStockAsync(text, categoryId, lowStockOnly, ct: ct);
            Action<List<ProductVariant>> apply = found =>
            {
                Items = found;
                SelectedItem = Items.FirstOrDefault(i => i.Id == selectedId) ?? Items.FirstOrDefault();
            };
            await (immediately ? _search.RunNowAsync(load, apply) : _search.RunAsync(load, apply));
        }
        catch (Exception ex)
        {
            Dialogs.Error("Could not load stock.", ex);
        }
    }

    private bool HasSelection() => SelectedItem is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task AdjustAsync()
    {
        if (SelectedItem is null) return;
        if (Dialogs.ShowDialog(new StockAdjustViewModel(Dialogs, inventory, session, SelectedItem))) await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task CountAsync()
    {
        if (SelectedItem is not { } item) return;
        var counted = Dialogs.PromptInt("Stock count", $"Counted quantity for {item.DisplayName} ({item.Sku}).\nSystem quantity: {item.StockQuantity}", item.StockQuantity);
        if (counted is null) return;
        if (await RunAsync(() => inventory.SetCountedStockAsync(item.Id, counted.Value, "Stock count", session.User.Id)))
            await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ReorderLevelAsync()
    {
        if (SelectedItem is not { } item) return;
        var level = Dialogs.PromptInt("Reorder level", $"Warn when stock of {item.DisplayName} falls to or below:", item.ReorderLevel);
        if (level is null) return;
        if (await RunAsync(() => inventory.UpdateReorderLevelAsync(item.Id, level.Value))) await LoadItemsAsync();
    }

    [RelayCommand]
    private void PrintLabels()
    {
        List<ProductVariant> targets = SelectedItem is null ? Items : [SelectedItem];
        if (targets.Count == 0) return;
        Dialogs.ShowDialog(new LabelPrintViewModel(Dialogs, print, targets));
    }

    [RelayCommand]
    private void Export()
    {
        var path = Dialogs.SaveFile("Export stock", "CSV files (*.csv)|*.csv", $"stock_{DateTime.Today:yyyyMMdd}.csv");
        if (path is null) return;
        try
        {
            CsvExporter.Write(path,
                ["Product", "Brand", "Category", "Size", "Colour", "SKU", "Barcode", "Stock", "Reorder level", "Unit cost", "Price", "Stock value (cost)"],
                Items.Select(v => new object?[]
                {
                    v.Product?.Name, v.Product?.Brand, v.Product?.Category?.Name, v.Size, v.Color, v.Sku, v.Barcode,
                    v.StockQuantity, v.ReorderLevel, v.EffectiveCost, v.EffectivePrice, Money.Round(Math.Max(0, v.StockQuantity) * v.EffectiveCost),
                }));
            Dialogs.Info($"Exported {Items.Count} items.");
        }
        catch (Exception ex)
        {
            Dialogs.Error("Export failed.", ex);
        }
    }
}

public sealed record AdjustmentReason(string Label, StockMovementType Type, int Sign);

public sealed partial class StockAdjustViewModel(IDialogService dialogs, InventoryService inventory, Session session, ProductVariant item)
    : DialogViewModelBase(dialogs)
{
    public override string Title => "Adjust stock";
    public ProductVariant Item { get; } = item;

    public AdjustmentReason[] Reasons { get; } =
    [
        new("Stock received (no PO)", StockMovementType.PurchaseReceipt, +1),
        new("Damaged / unsellable", StockMovementType.Damaged, -1),
        new("Lost / stolen", StockMovementType.Adjustment, -1),
        new("Returned to supplier", StockMovementType.Adjustment, -1),
        new("Found / correction (+)", StockMovementType.Adjustment, +1),
        new("Correction (−)", StockMovementType.Adjustment, -1),
    ];

    [ObservableProperty]
    public partial AdjustmentReason? SelectedReason { get; set; }

    [ObservableProperty]
    public partial int Quantity { get; set; } = 1;

    [ObservableProperty]
    public partial string? Notes { get; set; }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (SelectedReason is null) throw new BusinessRuleException("Choose a reason.");
        if (Quantity <= 0) throw new BusinessRuleException("Quantity must be at least 1.");
        var notes = string.IsNullOrWhiteSpace(Notes) ? SelectedReason.Label : $"{SelectedReason.Label}: {Notes}";
        await inventory.AdjustStockAsync(Item.Id, SelectedReason.Sign * Quantity, SelectedReason.Type, notes, session.User.Id);
        Close(true);
    });

    [RelayCommand]
    private void Cancel() => Close(false);
}
