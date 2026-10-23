using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Data.OnlineReports;
using ClothingStore.Data.Services;

namespace ClothingStore.Tests;

public class OnlineReportsTests
{
    private const string Site = "https://pos-reports.example.workers.dev";

    /// <summary>The keys the website accepts (cloud/reports-worker/src/index.js, DOC_KEY).</summary>
    private static readonly Regex DocKey = new(@"^(store|live|stock|period:(today|yesterday|week|lastweek|month|lastmonth|year)|day:\d{4}-\d{2}-\d{2}|month:\d{4}-\d{2})$");

    /// <summary>Stands in for the website: records each upload and answers as told.</summary>
    private sealed class FakeSite : HttpMessageHandler
    {
        public List<JsonElement> Uploads { get; } = [];
        public List<string> Keys { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Answer { get; set; } = "{\"ok\":true}";
        public bool Offline { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Offline) throw new HttpRequestException("No such host is known.");
            if (request.RequestUri!.AbsolutePath == "/api/health")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true,\"version\":1}") };
            Assert.Equal($"{Site}/api/upload", request.RequestUri.ToString());
            Keys.Add(request.Headers.Authorization!.Parameter!);
            Uploads.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone());
            return new HttpResponseMessage(Status) { Content = new StringContent(Answer) };
        }

        public IEnumerable<string> DocKeys(int upload) => Uploads[upload].GetProperty("docs").EnumerateObject().Select(p => p.Name);
        public IEnumerable<string> AllDocKeys => Enumerable.Range(0, Uploads.Count).SelectMany(DocKeys);
    }

    private static (OnlineReportsService Service, OnlineReportsBuilder Builder, FakeSite Site) Create(TestDatabase db)
    {
        var builder = new OnlineReportsBuilder(db.Factory, db.Reports, db.Shifts);
        var site = new FakeSite();
        return (new OnlineReportsService(db.Factory, builder, new HttpClient(site)), builder, site);
    }

    private static async Task<OnlineReportsService> SetUpAsync(OnlineReportsService service)
    {
        await service.SaveConfigAsync(new OnlineReportsConfig { SiteUrl = Site, UploadKey = "key-123" });
        return service;
    }

    private static async Task SellAsync(TestDatabase db, Shift shift, Product tee, int? customerId = null) =>
        await db.Sales.CompleteSaleAsync(new CheckoutRequest
        {
            UserId = db.Cashier.Id, ShiftId = shift.Id, CustomerId = customerId,
            Lines = [new CheckoutLine(tee.Variants[0].Id, 1)], Payments = [new PaymentInput(PaymentMethod.Cash, 22m)],
        });

    [Fact]
    public async Task The_website_figures_are_the_reports_page_figures()
    {
        await using var db = await TestDatabase.CreateAsync();
        var tee = await db.CreateTeeAsync(price: 20m, cost: 8m);
        var shift = await db.OpenShiftAsync();
        await SellAsync(db, shift, tee);
        await SellAsync(db, shift, tee);
        var (_, builder, _) = Create(db);

        var doc = await builder.PeriodAsync("today", DateTime.Today);
        var report = await db.Reports.GetSalesReportAsync(DateTime.Today, DateTime.Today.AddDays(1));

        Assert.Equal((report.TotalSales, report.NetRevenue, report.GrossProfit, report.Transactions, report.ItemsSold),
            (doc.Totals.TotalSales, doc.Totals.NetRevenue, doc.Totals.GrossProfit, doc.Totals.Transactions, doc.Totals.ItemsSold));
        Assert.Equal(44m, doc.Totals.TotalSales);
        Assert.Equal(report.ByPaymentMethod.Select(p => (p.Name, p.Amount)), doc.Payments.Select(p => (p.Name, p.Amount)));
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), doc.From);
        Assert.Equal(doc.From, doc.To);

        var live = await builder.LiveAsync();
        var drawer = Assert.Single(live.OpenDrawers);
        Assert.Equal((2, 100m + 44m), (drawer.Sales, drawer.ExpectedCash));
        Assert.Equal(2, live.RecentSales.Count);
        Assert.Equal(("completed", 1, 22m), (live.RecentSales[0].Status, live.RecentSales[0].Items, live.RecentSales[0].Total));
    }

    [Theory]
    [InlineData("today", "2026-10-22", "2026-10-23")]
    [InlineData("yesterday", "2026-10-21", "2026-10-22")]
    [InlineData("week", "2026-10-19", "2026-10-23")] // Thursday 22 October: the week started on Monday 19
    [InlineData("lastweek", "2026-10-12", "2026-10-19")]
    [InlineData("month", "2026-10-01", "2026-10-23")]
    [InlineData("lastmonth", "2026-09-01", "2026-10-01")]
    [InlineData("year", "2026-01-01", "2026-10-23")]
    public void Periods_match_the_reports_page_buttons(string period, string from, string to)
    {
        var range = OnlineReportsBuilder.PeriodRange(period, new DateTime(2026, 10, 22, 15, 30, 0));
        Assert.Equal((DateTime.Parse(from), DateTime.Parse(to)), range);
    }

    [Fact]
    public async Task Only_managers_and_admins_can_sign_in_and_their_passwords_are_never_sent()
    {
        await using var db = await TestDatabase.CreateAsync();
        var gone = await db.Users.SaveAsync(new User { Username = "Gone", FullName = "Left", Role = UserRole.Manager }, "secret3", db.Admin.Id);
        gone.IsActive = false;
        await db.Users.SaveAsync(gone, null, db.Admin.Id);
        var (_, builder, _) = Create(db);

        var users = await builder.UsersAsync();

        // The cashier, the deactivated manager and the admin still on the first-run password are left out.
        var manager = Assert.Single(users);
        Assert.Equal(("manager", "Morgan Manager", "manager"), (manager.Username, manager.Name, manager.Role));
        Assert.DoesNotContain("secret2", JsonSerializer.Serialize(users));

        // What the phone works out from the typed password, hashed, is what the website compares with.
        var proof = Rfc2898DeriveBytes.Pbkdf2("secret2", Convert.FromBase64String(manager.Salt), manager.Iterations, HashAlgorithmName.SHA256, 32);
        Assert.Equal(manager.Verifier, Convert.ToBase64String(SHA256.HashData(proof)));
        var wrong = Rfc2898DeriveBytes.Pbkdf2("secret", Convert.FromBase64String(manager.Salt), manager.Iterations, HashAlgorithmName.SHA256, 32);
        Assert.NotEqual(manager.Verifier, Convert.ToBase64String(SHA256.HashData(wrong)));
    }

    [Fact]
    public async Task Nothing_is_sent_until_it_is_set_up_and_switched_on()
    {
        await using var db = await TestDatabase.CreateAsync();
        var (service, _, site) = Create(db);

        Assert.Equal(SyncOutcome.Off, (await service.SyncAsync("till-1", "Till 1")).Outcome);
        await service.SaveConfigAsync(new OnlineReportsConfig { SiteUrl = Site, UploadKey = "key-123", Enabled = false });
        Assert.Equal(SyncOutcome.Off, (await service.SyncAsync("till-1", "Till 1")).Outcome);
        Assert.Empty(site.Uploads);
    }

    [Fact]
    public async Task First_upload_sends_everything_then_only_what_changed()
    {
        await using var db = await TestDatabase.CreateAsync();
        var tee = await db.CreateTeeAsync();
        var shift = await db.OpenShiftAsync();
        var customer = await db.Customers.SaveAsync(new Customer { FirstName = "Rana", LastName = "Haddad", Phone = "03111222" });
        await SellAsync(db, shift, tee, customer.Id);
        var (service, _, site) = Create(db);
        await SetUpAsync(service);

        var first = await service.SyncAsync("till-1", "Till 1");

        Assert.Equal(SyncOutcome.Sent, first.Outcome);
        Assert.All(site.Keys, k => Assert.Equal("key-123", k));
        Assert.All(site.AllDocKeys, k => Assert.Matches(DocKey, k));
        Assert.All(site.Uploads, u => Assert.True(u.GetProperty("docs").EnumerateObject().Count() <= OnlineReportsService.DocsPerUpload));
        Assert.All(site.Uploads, u => Assert.Equal(1, u.GetProperty("v").GetInt32()));
        var keys = site.AllDocKeys.ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains("store", keys);
        Assert.Contains("live", keys);
        Assert.Contains("stock", keys);
        Assert.All(OnlineReportsBuilder.Periods, p => Assert.Contains($"period:{p}", keys));
        Assert.Contains(keys, k => k.StartsWith("day:"));
        // The accounts and the clean-up of old days go with the first batch.
        Assert.True(site.Uploads[0].TryGetProperty("users", out _));
        Assert.True(site.Uploads[0].TryGetProperty("keepDaysFrom", out _));
        // No customer names, phone numbers or passwords.
        var everything = string.Join("\n", site.Uploads.Select(u => u.GetRawText()));
        Assert.DoesNotContain("Rana", everything);
        Assert.DoesNotContain("03111222", everything);
        Assert.DoesNotContain("PBKDF2", everything);

        var state = await service.GetStateAsync();
        Assert.NotNull(state.LastUploadUtc);
        Assert.Null(state.Error);
        Assert.Equal("Till 1", state.Till);

        // Straight after: nothing changed. History is still catching up (25 finished days per run); no accounts again.
        site.Uploads.Clear();
        var second = await service.SyncAsync("till-1", "Till 1");
        Assert.Equal(SyncOutcome.Sent, second.Outcome);
        Assert.All(site.AllDocKeys, k => Assert.Matches(@"^(day|month):", k));
        Assert.False(site.Uploads[0].TryGetProperty("users", out _));

        // Keep going until the history is all there: then a run is only "the shop is online".
        for (var i = 0; i < 6; i++)
        {
            site.Uploads.Clear();
            await service.SyncAsync("till-1", "Till 1");
        }
        Assert.Single(site.Uploads);
        Assert.Empty(site.DocKeys(0));
        state = await service.GetStateAsync();
        Assert.Equal(DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"), state.DaysThrough);
        Assert.Equal(DateTime.Today.AddMonths(-1).ToString("yyyy-MM"), state.MonthsThrough);

        // A new sale: what is happening now and today's report go up.
        await SellAsync(db, shift, tee);
        site.Uploads.Clear();
        await service.SyncAsync("till-1", "Till 1");
        Assert.Equal(["live", "period:today"], site.DocKeys(0).Order());
    }

    [Fact]
    public async Task One_till_sends_at_a_time()
    {
        await using var db = await TestDatabase.CreateAsync();
        var (service, _, site) = Create(db);
        await SetUpAsync(service);

        Assert.Equal(SyncOutcome.Sent, (await service.SyncAsync("till-1", "Till 1")).Outcome);
        Assert.Equal(SyncOutcome.OtherTill, (await service.SyncAsync("till-2", "Till 2")).Outcome);
        // "Upload now" on the second till takes over.
        Assert.Equal(SyncOutcome.Sent, (await service.SyncAsync("till-2", "Till 2", force: true)).Outcome);
        Assert.Equal(SyncOutcome.OtherTill, (await service.SyncAsync("till-1", "Till 1")).Outcome);
        Assert.Equal("Till 2", (await service.GetStateAsync()).Till);
    }

    [Fact]
    public async Task Failures_are_recorded_and_the_next_run_carries_on()
    {
        await using var db = await TestDatabase.CreateAsync();
        var (service, _, site) = Create(db);
        await SetUpAsync(service);

        site.Offline = true;
        var offline = await service.SyncAsync("till-1", "Till 1");
        Assert.Equal((SyncOutcome.Failed, OnlineReportsError.Offline), (offline.Outcome, offline.Error));
        var state = await service.GetStateAsync();
        Assert.Equal(nameof(OnlineReportsError.Offline), state.Error);
        Assert.NotNull(state.FailingSinceUtc);
        Assert.Null(state.LastUploadUtc);

        site.Offline = false;
        site.Status = HttpStatusCode.Unauthorized;
        Assert.Equal(OnlineReportsError.Rejected, (await service.SyncAsync("till-1", "Till 1")).Error);
        site.Status = HttpStatusCode.BadRequest;
        site.Answer = "{\"error\":\"bad_version\",\"version\":2}";
        Assert.Equal(OnlineReportsError.Outdated, (await service.SyncAsync("till-1", "Till 1")).Error);
        Assert.Equal(state.FailingSinceUtc, (await service.GetStateAsync()).FailingSinceUtc); // still failing since the first time

        site.Status = HttpStatusCode.OK;
        site.Answer = "{\"ok\":true}";
        site.Uploads.Clear();
        Assert.Equal(SyncOutcome.Sent, (await service.SyncAsync("till-1", "Till 1")).Outcome);
        Assert.Contains("store", site.AllDocKeys); // nothing was lost
        state = await service.GetStateAsync();
        Assert.Null(state.Error);
        Assert.NotNull(state.LastUploadUtc);
    }

    [Fact]
    public async Task Sign_out_all_devices_is_sent_once()
    {
        await using var db = await TestDatabase.CreateAsync();
        var (service, _, site) = Create(db);
        await SetUpAsync(service);
        await service.SyncAsync("till-1", "Till 1");
        Assert.False(site.Uploads[0].TryGetProperty("signOutAll", out _));

        await service.SignOutAllDevicesAsync();
        site.Uploads.Clear();
        await service.SyncAsync("till-1", "Till 1");
        Assert.Equal(1, site.Uploads[0].GetProperty("signOutAll").GetInt32());

        site.Uploads.Clear();
        await service.SyncAsync("till-1", "Till 1");
        Assert.False(site.Uploads[0].TryGetProperty("signOutAll", out _));
    }

    [Fact]
    public async Task A_password_change_reaches_the_website_on_the_next_run()
    {
        await using var db = await TestDatabase.CreateAsync();
        var (service, _, site) = Create(db);
        await SetUpAsync(service);
        await service.SyncAsync("till-1", "Till 1");
        var before = site.Uploads[0].GetProperty("users")[0].GetProperty("verifier").GetString();

        await db.Users.SaveAsync(db.Manager, "new-secret", db.Admin.Id);
        site.Uploads.Clear();
        await service.SyncAsync("till-1", "Till 1");

        var after = site.Uploads[0].GetProperty("users")[0].GetProperty("verifier").GetString();
        Assert.NotEqual(before, after);
    }

    [Theory]
    [InlineData("pos-reports.rami.workers.dev", "https://pos-reports.rami.workers.dev")]
    [InlineData(" https://pos-reports.rami.workers.dev/ ", "https://pos-reports.rami.workers.dev")]
    [InlineData("https://x.workers.dev/some/page", "https://x.workers.dev")]
    [InlineData("http://localhost:8787", "http://localhost:8787")]
    [InlineData("http://example.com", null)] // only https on the internet
    [InlineData("ftp://x.dev", null)]
    [InlineData("", null)]
    public void Site_addresses_are_tidied(string text, string? expected) =>
        Assert.Equal(expected, OnlineReportsService.NormalizeSiteUrl(text));

    [Fact]
    public async Task Checks_the_website_answers_with_this_format()
    {
        await using var db = await TestDatabase.CreateAsync();
        var (service, _, site) = Create(db);
        Assert.Equal((true, 1), await service.CheckSiteAsync(Site));
        site.Offline = true;
        Assert.Equal((false, null), await service.CheckSiteAsync(Site));
    }

    /// <summary>
    /// Against a real website when one is given (npx wrangler dev in cloud/reports-worker): POS_TEST_REPORTS_SITE and
    /// POS_TEST_REPORTS_KEY. Uploads everything, as a shop's first upload, and checks the website took it.
    /// </summary>
    [Fact]
    public async Task Uploads_to_a_real_website_when_one_is_given()
    {
        var url = Environment.GetEnvironmentVariable("POS_TEST_REPORTS_SITE");
        var key = Environment.GetEnvironmentVariable("POS_TEST_REPORTS_KEY");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) return;

        await using var db = await TestDatabase.CreateAsync();
        var tee = await db.CreateTeeAsync();
        var shift = await db.OpenShiftAsync();
        await SellAsync(db, shift, tee);
        await SellAsync(db, shift, tee);
        var service = new OnlineReportsService(db.Factory, new OnlineReportsBuilder(db.Factory, db.Reports, db.Shifts), new HttpClient());
        await service.SaveConfigAsync(new OnlineReportsConfig { SiteUrl = url, UploadKey = key });

        Assert.Equal((true, 1), await service.CheckSiteAsync(url));
        for (var i = 0; i < 6; i++)
        {
            var result = await service.SyncAsync("till-1", "Till 1");
            Assert.True(result.Outcome == SyncOutcome.Sent, $"{result.Error}: {result.Detail}");
        }
    }
}
