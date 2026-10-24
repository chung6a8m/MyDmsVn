using System;
using System.Globalization;
using MyDmsVn.Bootstrap5WinFormUI.Formatting;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.WinForms;

internal static class ReceiptDecimalInputProfiles
{
    internal static DecimalInputProfile For(ReceiptNumericField field, CultureInfo culture)
    {
        if (culture == null) throw new ArgumentNullException(nameof(culture));
        switch (field)
        {
            case ReceiptNumericField.Quantity:
                return new DecimalInputProfile(culture, false, GoodsReceiptContractLimits.DecimalPlaces,
                    0.0001m, GoodsReceiptContractLimits.MaximumQuantity);
            case ReceiptNumericField.UnitCost:
                return new DecimalInputProfile(culture, false, GoodsReceiptContractLimits.DecimalPlaces,
                    0m, GoodsReceiptContractLimits.MaximumUnitCost);
            case ReceiptNumericField.UnitPrice:
                return new DecimalInputProfile(culture, false, GoodsReceiptContractLimits.DecimalPlaces,
                    0m, SalesContractLimits.MaximumUnitPrice);
            default:
                throw new ArgumentOutOfRangeException(nameof(field));
        }
    }
}
