using ClothingStore.Core;

namespace ClothingStore.Tests;

public class PhoneLinksTests
{
    [Theory]
    [InlineData("03 123 456", "9613123456")]
    [InlineData("03-123456", "9613123456")]
    [InlineData("71 234 567", "96171234567")]
    [InlineData("+961 71 234 567", "96171234567")]
    [InlineData("00961 3 123456", "9613123456")]
    [InlineData("961 3 123 456", "9613123456")]
    [InlineData("+33 6 12 34 56 78", "33612345678")]
    [InlineData("", null)]
    [InlineData("n/a", null)]
    public void Numbers_become_international_digits(string phone, string? expected) =>
        Assert.Equal(expected, PhoneLinks.WhatsAppNumber(phone));

    [Fact]
    public void Link_carries_the_message()
    {
        Assert.Equal("https://wa.me/9613123456?text=Hi%20Rana%2C%20order%20%2312",
            PhoneLinks.WhatsAppLink("03 123 456", "Hi Rana, order #12"));
        Assert.Null(PhoneLinks.WhatsAppLink(null, "x"));
    }
}
