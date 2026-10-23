using System.Collections.ObjectModel;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClothingStore.Core.Localization;

namespace ClothingStore.Desktop.ViewModels.Dialogs;

public sealed partial class VariantRowViewModel : ObservableObject
{
    public int Id { get; init; }
    public bool IsNew => Id == 0;

    [ObservableProperty] public partial string Size { get; set; } = "";
    [ObservableProperty] public partial string Color { get; set; } = "";
    [ObservableProperty] public partial string? Sku { get; set; }
    [ObservableProperty] public partial string? Barcode { get; set; }
    [ObservableProperty] public partial decimal? PriceOverride { get; set; }
    [ObservableProperty] public partial decimal? CostOverride { get; set; }
    [ObservableProperty] public partial int StockQuantity { get; set; }
    [ObservableProperty] public partial int ReorderLevel { get; set; } = 2;
    [ObservableProperty] public partial bool IsActive { get; set; } = true;

    public ProductVariant ToEntity() => new()
    {
        Id = Id, Size = Size ?? "", Color = Color ?? "", Sku = Sku ?? "", Barcode = Barcode,
        PriceOverride = PriceOverride, CostOverride = CostOverride, StockQuantity = StockQuantity,
        ReorderLevel = ReorderLevel, IsActive = IsActive,
    };
}

/// <summary>A size button: one of the built-in ones, or one the store added (those can be removed).</summary>
public sealed record SizePreset(string Name, string Sizes, bool IsCustom = false);

/// <summary>Create/edit a style with its size × colour matrix.</summary>
public sealed partial class ProductEditorViewModel : DialogViewModelBase
{
    public override System.Windows.Size? WindowSize => new(width: 1280, height: 720);

    private readonly ProductService _products;
    private readonly SizePresetService _sizePresets;
    private readonly Session _session;
    private readonly int _id;

    private ProductEditorViewModel(IDialogService dialogs, ProductService products, SizePresetService sizePresets, Session session, Product? product) : base(dialogs)
    {
        _products = products;
        _sizePresets = sizePresets;
        _session = session;
        _id = product?.Id ?? 0;
        if (product is null) return;

        Name = product.Name;
        Brand = product.Brand;
        StyleCode = product.StyleCode;
        Description = product.Description;
        Season = product.Season;
        Material = product.Material;
        Gender = product.Gender;
        Price = product.Price;
        Cost = product.Cost;
        IsActive = product.IsActive;
        foreach (var v in product.Variants.OrderBy(v => v.Color).ThenBy(v => v.Id))
        {
            Variants.Add(new VariantRowViewModel
            {
                Id = v.Id, Size = v.Size, Color = v.Color, Sku = v.Sku, Barcode = v.Barcode, PriceOverride = v.PriceOverride,
                CostOverride = v.CostOverride, StockQuantity = v.StockQuantity, ReorderLevel = v.ReorderLevel, IsActive = v.IsActive,
            });
        }
    }

    public static async Task<ProductEditorViewModel> CreateAsync(
        IDialogService dialogs, ProductService products, CategoryService categories, SupplierService suppliers,
        SizePresetService sizePresets, Session session, Product? product)
    {
        var vm = new ProductEditorViewModel(dialogs, products, sizePresets, session, product)
        {
            // Active ones, plus the product's own even if it was deactivated since: otherwise saving would quietly move
            // the product to another category or drop its supplier.
            Categories = (await categories.GetAllAsync(includeInactive: true)).Where(c => c.IsActive || c.Id == product?.CategoryId).ToList(),
            Suppliers = (await suppliers.GetAllAsync(includeInactive: true)).Where(s => s.IsActive || s.Id == product?.SupplierId).ToList(),
        };
        vm.SelectedCategory = vm.Categories.FirstOrDefault(c => c.Id == product?.CategoryId) ?? vm.Categories.FirstOrDefault();
        vm.SelectedSupplier = vm.Suppliers.FirstOrDefault(s => s.Id == product?.SupplierId);
        try { vm.ShowPresets(await sizePresets.GetAllAsync()); }
        catch { /* the built-in buttons are enough to work with */ }
        return vm;
    }

    public override string Title => _id == 0 ? Loc.T("ProductEditor.New") : Loc.T("ProductEditor.Edit", Name);

    public List<Category> Categories { get; private init; } = [];
    public List<Supplier> Suppliers { get; private init; } = [];
    public Gender[] Genders { get; } = Enum.GetValues<Gender>();

    private static readonly SizePreset[] BuiltInPresets =
    [
        new(Loc.T("ProductEditor.Preset.Letters"), "XS, S, M, L, XL, XXL"),
        new(Loc.T("ProductEditor.Preset.LettersShort"), "S, M, L, XL"),
        new(Loc.T("ProductEditor.Preset.Women"), "4, 6, 8, 10, 12, 14, 16"),
        new(Loc.T("ProductEditor.Preset.Waist"), "28, 30, 32, 34, 36, 38"),
        new(Loc.T("ProductEditor.Preset.Shoes"), "36, 37, 38, 39, 40, 41, 42, 43, 44, 45"),
        new(Loc.T("ProductEditor.Preset.Kids"), "2Y, 4Y, 6Y, 8Y, 10Y, 12Y, 14Y"),
        new(Loc.T("ProductEditor.Preset.OneSize"), "One Size"),
    ];

    /// <summary>Built-in size buttons, then the store's own (shared by every till).</summary>
    [ObservableProperty]
    public partial List<SizePreset> SizePresets { get; private set; } = [.. BuiltInPresets];

    private void ShowPresets(IEnumerable<CustomSizePreset> custom) =>
        SizePresets = [.. BuiltInPresets, .. custom.Select(p => new SizePreset(p.Name, p.Sizes, IsCustom: true))];

    /// <summary>Makes a button of the sizes typed in the box, for the next products.</summary>
    [RelayCommand]
    private async Task SavePresetAsync()
    {
        var sizes = SizePresetService.Normalize(SizesText);
        if (sizes.Length == 0)
        {
            Dialogs.Warning(Loc.T("Err.PresetSizesRequired"));
            return;
        }
        var parts = sizes.Split(", ");
        var suggestion = parts.Length > 1 ? $"{parts[0]}–{parts[^1]}" : parts[0];
        var name = Dialogs.Prompt(Loc.T("ProductEditor.SavePreset"), Loc.T("ProductEditor.PresetNamePrompt", sizes), suggestion);
        if (name is null) return;
        List<CustomSizePreset> saved = [];
        if (await RunAsync(async () => saved = await _sizePresets.AddAsync(name, sizes)))
        {
            ShowPresets(saved);
            Dialogs.Toast(Loc.T("ProductEditor.PresetSaved", name.Trim()));
        }
    }

    [RelayCommand]
    private async Task RemovePresetAsync(SizePreset? preset)
    {
        if (preset is not { IsCustom: true } || !Dialogs.Confirm(Loc.T("ProductEditor.PresetRemoveConfirm", preset.Name))) return;
        List<CustomSizePreset> left = [];
        if (await RunAsync(async () => left = await _sizePresets.RemoveAsync(preset.Name))) ShowPresets(left);
    }

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string? Brand { get; set; }
    [ObservableProperty] public partial string? StyleCode { get; set; }
    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial string? Season { get; set; }
    [ObservableProperty] public partial string? Material { get; set; }
    [ObservableProperty] public partial Gender Gender { get; set; }
    [ObservableProperty] public partial decimal Price { get; set; }
    [ObservableProperty] public partial decimal Cost { get; set; }
    [ObservableProperty] public partial bool IsActive { get; set; } = true;
    [ObservableProperty] public partial Category? SelectedCategory { get; set; }
    [ObservableProperty] public partial Supplier? SelectedSupplier { get; set; }

    public string MarginText => Price <= 0 ? "" : Loc.T("ProductEditor.Margin", (Price - Cost) / Price * 100m);

    partial void OnPriceChanged(decimal value) => OnPropertyChanged(nameof(MarginText));
    partial void OnCostChanged(decimal value) => OnPropertyChanged(nameof(MarginText));

    public ObservableCollection<VariantRowViewModel> Variants { get; } = [];

    [ObservableProperty] public partial string SizesText { get; set; } = "S, M, L, XL";
    [ObservableProperty] public partial string ColorsText { get; set; } = "";
    [ObservableProperty] public partial int OpeningStock { get; set; }

    [RelayCommand]
    private void ApplyPreset(SizePreset preset) => SizesText = preset.Sizes;

    [RelayCommand]
    private void Generate()
    {
        static string[] Split(string text) =>
            text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sizes = Split(SizesText);
        var colors = Split(ColorsText);
        if (sizes.Length == 0) sizes = [""];
        if (colors.Length == 0) colors = [""];
        if (sizes is [""] && colors is [""])
        {
            Dialogs.Warning(Loc.T("ProductEditor.NeedSizeOrColour"));
            return;
        }

        var added = 0;
        foreach (var color in colors)
        {
            foreach (var size in sizes)
            {
                var exists = Variants.Any(v =>
                    string.Equals(v.Size, size, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(v.Color, color, StringComparison.OrdinalIgnoreCase));
                if (exists) continue;
                Variants.Add(new VariantRowViewModel { Size = size, Color = color, StockQuantity = Math.Max(0, OpeningStock) });
                added++;
            }
        }
        if (added == 0) Dialogs.Toast(Loc.T("ProductEditor.AllExist"), ToastKind.Info);
        else Dialogs.Toast(Loc.T("ProductEditor.VariantsAdded", added));
    }

    [RelayCommand]
    private void AddVariant() => Variants.Add(new VariantRowViewModel());

    [RelayCommand]
    private void RemoveVariant(VariantRowViewModel row)
    {
        if (!row.IsNew && !Dialogs.Confirm(Loc.T("ProductEditor.RemoveVariantConfirm", ProductVariant.DescribeVariant(row.Size, row.Color))))
            return;
        Variants.Remove(row);
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (SelectedCategory is null) throw new BusinessRuleException(Loc.T("ProductEditor.NeedCategory"));
        var product = new Product
        {
            Id = _id,
            Name = Name ?? "",
            Brand = Brand,
            StyleCode = StyleCode,
            Description = Description,
            Season = Season,
            Material = Material,
            Gender = Gender,
            Price = Price,
            Cost = Cost,
            IsActive = IsActive,
            CategoryId = SelectedCategory.Id,
            SupplierId = SelectedSupplier?.Id,
            Variants = Variants.Select(v => v.ToEntity()).ToList(),
        };
        await _products.SaveAsync(product, _session.User.Id);
        Close(true);
    });

    [RelayCommand]
    private void Cancel() => Close(false);
}
