using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Tests;

public class ReturnServiceTests
{
    private static async Task<(TestDatabase Db, Sale Sale, Shift Shift, Customer Customer)> SetupAsync()
    {
        var db = await TestDatabase.CreateAsync();
        var tee = await db.CreateTeeAsync(stockEach: 10, price: 10m);
        var customer = await db.Customers.SaveAsync(new Customer { FirstName = "Cleo" });
        var shift = await db.OpenShiftAsync();
        // 3 x 10.00 with 10% cart discount = 27.00 + 10% tax = 29.70
        var sale = await db.Sales.CompleteSaleAsync(new CheckoutRequest
        {
            UserId = db.Cashier.Id,
            ShiftId = shift.Id,
            CustomerId = customer.Id,
            Lines = [new CheckoutLine(tee.Variants[0].Id, 3)],
            CartDiscountType = DiscountType.Percent,
            CartDiscountValue = 10m,
            Payments = [new PaymentInput(PaymentMethod.Cash, 29.70m)],
        });
        return (db, sale, shift, customer);
    }

    [Fact]
    public async Task Partial_returns_reconcile_to_line_total()
    {
        var (db, sale, shift, _) = await SetupAsync();
        await using var _db = db;
        var line = sale.Lines.Single();
        Assert.Equal(29.70m, line.LineTotal);

        var first = await db.Returns.ProcessReturnAsync(new ReturnRequest
        {
            SaleId = sale.Id, UserId = db.Cashier.Id, ShiftId = shift.Id, RefundMethod = RefundMethod.Cash,
            Lines = [new ReturnLineRequest(line.Id, 1)],
        });
        var second = await db.Returns.ProcessReturnAsync(new ReturnRequest
        {
            SaleId = sale.Id, UserId = db.Cashier.Id, ShiftId = shift.Id, RefundMethod = RefundMethod.Cash,
            Lines = [new ReturnLineRequest(line.Id, 2)],
        });

        Assert.Equal(9.90m, first.TotalRefund);
        Assert.Equal(29.70m, first.TotalRefund + second.TotalRefund);
        Assert.Equal(10, (await db.GetVariantAsync(line.ProductVariantId)).StockQuantity);
        Assert.Matches(@"^RT\d{8}-0002$", second.ReturnNumber);

        await Assert.ThrowsAsync<BusinessRuleException>(() => db.Returns.ProcessReturnAsync(new ReturnRequest
        {
            SaleId = sale.Id, UserId = db.Cashier.Id, ShiftId = shift.Id, RefundMethod = RefundMethod.Cash,
            Lines = [new ReturnLineRequest(line.Id, 1)],
        }));

        var summary = await db.Shifts.GetSummaryAsync(shift.Id);
        Assert.Equal(100m, summary.ExpectedCash); // 100 float + 29.70 sale - 29.70 refunds
    }

    [Fact]
    public async Task Store_credit_refund_without_restock()
    {
        var (db, sale, _, customer) = await SetupAsync();
        await using var _db = db;
        var line = sale.Lines.Single();

        var ret = await db.Returns.ProcessReturnAsync(new ReturnRequest
        {
            SaleId = sale.Id, UserId = db.Cashier.Id, RefundMethod = RefundMethod.StoreCredit,
            Lines = [new ReturnLineRequest(line.Id, 1, Restock: false)], Reason = "Damaged seam",
        });

        var c = await db.GetCustomerAsync(customer.Id);
        Assert.Equal(ret.TotalRefund, c.StoreCredit);
        Assert.Equal(29 - 9, c.LoyaltyPoints);
        Assert.Equal(7, (await db.GetVariantAsync(line.ProductVariantId)).StockQuantity); // not restocked
    }

    [Fact]
    public async Task Return_window_enforced_for_cashiers()
    {
        var (db, sale, shift, _) = await SetupAsync();
        await using var _db = db;
        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            await ctx.Sales.Where(s => s.Id == sale.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, DateTime.Now.AddDays(-60)));
        }

        ReturnRequest Request(int user, int? approver = null) => new()
        {
            SaleId = sale.Id, UserId = user, ShiftId = shift.Id, RefundMethod = RefundMethod.Card,
            Lines = [new ReturnLineRequest(sale.Lines[0].Id, 1)], ApprovedByUserId = approver,
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => db.Returns.ProcessReturnAsync(Request(db.Cashier.Id)));
        var ret = await db.Returns.ProcessReturnAsync(Request(db.Cashier.Id, db.Manager.Id));
        Assert.Equal(9.90m, ret.TotalRefund);
    }

    [Fact]
    public async Task Voided_sales_cannot_be_returned_and_returned_sales_cannot_be_voided()
    {
        var (db, sale, shift, _) = await SetupAsync();
        await using var _db = db;

        await db.Returns.ProcessReturnAsync(new ReturnRequest
        {
            SaleId = sale.Id, UserId = db.Cashier.Id, ShiftId = shift.Id, RefundMethod = RefundMethod.Card,
            Lines = [new ReturnLineRequest(sale.Lines[0].Id, 1)],
        });
        await Assert.ThrowsAsync<BusinessRuleException>(() => db.Sales.VoidSaleAsync(sale.Id, db.Manager.Id, "test"));
    }
}
