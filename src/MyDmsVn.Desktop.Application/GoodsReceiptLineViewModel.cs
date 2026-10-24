using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyDmsVn.Desktop.Application
{
    public sealed class GoodsReceiptLineViewModel : ObservableObject
    {
        private long _id;
        private int _productId;
        private string? _productDisplayName;
        private bool _productIsAvailable = true;
        private decimal _quantity = 1m;
        private decimal _unitCost;
        private Func<bool>? _canMutate;
        private Action? _beforeMutation;

        internal void AttachOwner(Func<bool> canMutate, Action beforeMutation)
        { _canMutate = canMutate; _beforeMutation = beforeMutation; }
        internal void DetachOwner() { _canMutate = null; _beforeMutation = null; }
        private bool CanMutate => _canMutate?.Invoke() ?? true;

        public long Id
        {
            get => _id;
            internal set => SetProperty(ref _id, value);
        }

        public int ProductId
        {
            get => _productId;
            private set => SetProperty(ref _productId, value);
        }

        public string? ProductDisplayName
        {
            get => _productDisplayName;
            private set => SetProperty(ref _productDisplayName, value);
        }

        public bool ProductIsAvailable
        {
            get => _productIsAvailable;
            private set => SetProperty(ref _productIsAvailable, value);
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (CanMutate && _quantity != value)
                {
                    _beforeMutation?.Invoke(); _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal UnitCost
        {
            get => _unitCost;
            set
            {
                if (CanMutate && _unitCost != value)
                {
                    _beforeMutation?.Invoke(); _unitCost = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal? LineTotal
        {
            get
            {
                try
                {
                    return checked(Quantity * UnitCost);
                }
                catch (OverflowException)
                {
                    return null;
                }
            }
        }

        public void SetProduct(int productId, string? displayName, bool isAvailable)
        {
            if (_productId != productId)
            {
                if (!CanMutate) return;
                _beforeMutation?.Invoke();
                _productId = productId;
                OnPropertyChanged(nameof(ProductId));
            }
            ProductDisplayName = displayName;
            ProductIsAvailable = isAvailable;
        }

        internal void ApplyNumericSilently(decimal quantity, decimal unitCost)
        { _quantity = quantity; _unitCost = unitCost; }
        internal void NotifyNumeric(bool quantityChanged, bool costChanged)
        {
            if (quantityChanged) OnPropertyChanged(nameof(Quantity));
            if (costChanged) OnPropertyChanged(nameof(UnitCost));
            if (quantityChanged || costChanged) OnPropertyChanged(nameof(LineTotal));
        }
    }
}
