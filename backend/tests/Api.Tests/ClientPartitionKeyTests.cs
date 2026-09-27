using System.Net;
using Api.RateLimiting;

namespace Api.Tests;

public class ClientPartitionKeyTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    public void From_ReturnsExpectedKey(string address, string expectedKey)
    {
        var key = ClientPartitionKey.From(IPAddress.Parse(address));

        Assert.Equal(expectedKey, key);
    }

    [Fact]
    public void From_MasksIpv6AddressesInTheSame64PrefixToTheSameKey()
    {
        var keyA = ClientPartitionKey.From(IPAddress.Parse("2001:db8:1:2::1"));
        var keyB = ClientPartitionKey.From(IPAddress.Parse("2001:db8:1:2:ffff::9"));

        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void From_DistinguishesDifferent64Prefixes()
    {
        var sharedPrefixKey = ClientPartitionKey.From(IPAddress.Parse("2001:db8:1:2::1"));
        var differentPrefixKey = ClientPartitionKey.From(IPAddress.Parse("2001:db8:1:3::1"));

        Assert.NotEqual(sharedPrefixKey, differentPrefixKey);
    }

    [Fact]
    public void From_NullAddress_ReturnsUnknown()
    {
        var key = ClientPartitionKey.From(null);

        Assert.Equal("unknown", key);
    }
}
