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
                if (SetProperty(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal UnitCost
        {
            get => _unitCost;
            set
            {
                if (SetProperty(ref _unitCost, value))
                {
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal LineTotal => Quantity * UnitCost;

        public void SetProduct(int productId, string? displayName, bool isAvailable)
        {
            ProductId = productId;
            ProductDisplayName = displayName;
            ProductIsAvailable = isAvailable;
        }
    }
}
