using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application;

public sealed class SalesReceiptLineViewModel : ObservableObject
{
    private readonly Func<bool> _canEdit;
    private readonly Action<string> _changed;
    private readonly ISalesReceiptPreviewCalculator _calculator;
    private int _productId;
    private decimal _quantity = 1m;
    private decimal _unitPrice;
    private bool _isGift;
    internal SalesReceiptLineViewModel(Func<bool> canEdit, Action<string> changed, ISalesReceiptPreviewCalculator calculator)
    { _canEdit = canEdit; _changed = changed; _calculator = calculator; }
    public Guid ClientKey { get; } = Guid.NewGuid();
    public long SaleLineId { get; private set; }
    public int ProductId
    {
        get => _productId;
        set
        {
            if (!_canEdit() || _productId == value) return;
            _productId = value; Unit = ""; ProductDisplayName = null; ProductIsActive = true;
            Notify(); _changed("productId");
        }
    }
    public bool SetProduct(int productId, string? displayName, bool isActive, string unit)
    {
        if (!_canEdit()) return false;
        if (_productId == productId && ProductDisplayName == displayName && ProductIsActive == isActive && Unit == (unit ?? "")) return true;
        var identityChanged = _productId != productId;
        _productId = productId; ProductDisplayName = displayName; ProductIsActive = isActive; Unit = unit ?? "";
        Notify(); _changed(identityChanged ? "productId" : ""); return true;
    }
    public decimal Quantity { get => _quantity; set { if (_canEdit() && SetProperty(ref _quantity, value)) Changed("qty"); } }
    public decimal UnitPrice { get => _unitPrice; set { if (_canEdit() && SetProperty(ref _unitPrice, value)) Changed("unitPrice"); } }
    public bool IsGift { get => _isGift; set { if (_canEdit() && SetProperty(ref _isGift, value)) Changed("isGift"); } }
    public string Unit { get; private set; } = "";
    public string? ProductDisplayName { get; private set; }
    public bool ProductIsActive { get; private set; } = true;
    public decimal? PostedUnitCost { get; private set; }
    public decimal? CostAmount { get; private set; }
    public decimal? LineAmount
    {
        get
        {
            if (CostAmount.HasValue) return _storedAmount;
            try { return _calculator.CalculateRevenue(Quantity, UnitPrice, IsGift); }
            catch (ArgumentException) { return null; }
            catch (OverflowException) { return null; }
        }
    }
    private decimal _storedAmount;
    private void Changed(string field) { OnPropertyChanged(nameof(LineAmount)); _changed(field); }
    internal void Apply(SalesReceiptLineDto line)
    {
        SaleLineId = line.SaleLineId; _productId = line.ProductId; _quantity = line.Qty;
        _unitPrice = line.UnitPrice; _isGift = line.IsGift; Unit = line.Unit;
        ProductDisplayName = line.ProductDisplayName; ProductIsActive = line.ProductIsActive;
        PostedUnitCost = line.PostedUnitCost; CostAmount = line.CostAmount; _storedAmount = line.LineAmount;
    }
    internal void Notify()
    {
        foreach (var name in new[] { nameof(SaleLineId), nameof(ProductId), nameof(Quantity), nameof(UnitPrice), nameof(IsGift), nameof(Unit), nameof(ProductDisplayName), nameof(ProductIsActive), nameof(PostedUnitCost), nameof(CostAmount), nameof(LineAmount) })
            OnPropertyChanged(name);
    }
}
