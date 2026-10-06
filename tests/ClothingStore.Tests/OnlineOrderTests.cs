using ClothingStore.Core;
using ClothingStore.Data.Services;

namespace ClothingStore.Tests;

/// <summary>The tee sells for 22.00 (20 + 10% tax).</summary>
public class OnlineOrderTests
{
    private static OnlineOrderInput Input(int variantId, int qty = 2, decimal delivery = 3m) => new()
    {
        Channel = SalesChannel.Instagram,
        CustomerName = "Rana Haddad",
        Phone = "03 123 456",
        Handle = "@rana",
        Address = "Hamra, Beirut",
        DeliveryFee = delivery,
        Lines = [new OnlineOrderLineInput(variantId, qty)],
    };

    [Fact]
    public async Task Order_holds_stock_when_confirmed_and_completes_into_a_sale_with_the_delivery_fee()
    {
        await using var db = await TestDatabase.CreateAsync();
        var tee = await db.CreateTeeAsync(stockEach: 5);
        var variant = tee.Variants[0];
        var shift = await db.OpenShiftAsync();

        var order = await db.Orders.CreateAsync(Input(variant.Id) with { SaveCustomer = true }, db.Cashier.Id);
        Assert.StartsWith("OL", order.OrderNumber);
        Assert.Equal((44m, 3m, 47m), (order.ItemsTotal, order.DeliveryFee, order.Total));
        Assert.NotNull(order.CustomerId); // saved as a customer
        Assert.Equal(5, (await db.GetVariantAsync(variant.Id)).StockQuantity); // not held yet
        Assert.Equal(1, (await db.Orders.GetCountsAsync()).New);

        await db.Orders.ConfirmAsync(order.Id, db.Cashier.Id);
        Assert.Equal(3, (await db.GetVariantAsync(variant.Id)).StockQuantity);
        await Assert.ThrowsAsync<BusinessRuleException>(() => db.Orders.UpdateAsync(order.Id, Input(variant.Id, 1)));

        await db.Orders.MarkOutForDeliveryAsync(order.Id, "Abu Ali", db.Cashier.Id);
        var sale = await db.Orders.CompleteAsync(order.Id, new CompleteOnlineOrderRequest
        {
            UserId = db.Cashier.Id, ShiftId = shift.Id,
            Payments = [new PaymentInput(PaymentMethod.Cash, 50m)],
        });

        Assert.Equal((SalesChannel.Instagram, 3m, 47m, 3m), (sale.Channel, sale.DeliveryFee, sale.Total, sale.ChangeGiven));
        Assert.Equal(2, sale.Lines.Single().Quantity);
        Assert.Equal(3, (await db.GetVariantAsync(variant.Id)).StockQuantity); // not taken twice

        var done = await db.Orders.GetAsync(order.Id);
        Assert.Equal((OnlineOrderStatus.Completed, sale.Id, false), (done!.Status, done.SaleId, done.StockHeld));
        Assert.Equal(100m + 47m, (await db.Shifts.GetSummaryAsync(shift.Id)).ExpectedCash);

        var report = await db.Reports.GetSalesReportAsync(DateTime.Today, DateTime.Today.AddDays(1));
        Assert.Equal(3m, report.DeliveryFees);
        Assert.Equal(47m, Assert.Single(report.ByChannel).Amount);
    }

    [Fact]
    public async Task Cancelling_puts_held_stock_back_and_new_orders_can_be_edited()
    {
        await using var db = await TestDatabase.CreateAsync();
        var tee = await db.CreateTeeAsync(stockEach: 5);
        var variant = tee.Variants[1];

        var order = await db.Orders.CreateAsync(Input(variant.Id, 1, 0m), db.Cashier.Id);
        order = await db.Orders.UpdateAsync(order.Id, Input(variant.Id, 3, 2m));
        Assert.Equal((3, 68m), (order.Lines.Single().Quantity, order.Total));

        await db.Orders.ConfirmAsync(order.Id, db.Cashier.Id);
        Assert.Equal(2, (await db.GetVariantAsync(variant.Id)).StockQuantity);

        await Assert.ThrowsAsync<BusinessRuleException>(() => db.Orders.CancelAsync(order.Id, " ", db.Cashier.Id));
        await db.Orders.CancelAsync(order.Id, "Customer changed their mind", db.Cashier.Id);
        Assert.Equal(5, (await db.GetVariantAsync(variant.Id)).StockQuantity);
        Assert.Equal(OnlineOrderStatus.Cancelled, (await db.Orders.GetAsync(order.Id))!.Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() => db.Orders.ConfirmAsync(order.Id, db.Cashier.Id));
    }

    [Fact]
    public async Task Cash_needs_an_open_shift_but_a_wallet_payment_does_not()
    {
        await using var db = await TestDatabase.CreateAsync();
        var tee = await db.CreateTeeAsync();
        var order = await db.Orders.CreateAsync(Input(tee.Variants[0].Id, 1, 0m), db.Cashier.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => db.Orders.CompleteAsync(order.Id, new CompleteOnlineOrderRequest
        {
            UserId = db.Cashier.Id, Payments = [new PaymentInput(PaymentMethod.Cash, 22m)],
        }));

        // Completing straight from "new" takes the stock then.
        var sale = await db.Orders.CompleteAsync(order.Id, new CompleteOnlineOrderRequest
        {
            UserId = db.Cashier.Id, Payments = [new PaymentInput(PaymentMethod.MobileWallet, 22m, "Whish 123")],
        });
        Assert.Equal(PaymentMethod.MobileWallet, sale.Payments.Single().Method);
        Assert.Equal(9, (await db.GetVariantAsync(tee.Variants[0].Id)).StockQuantity);
    }
}
