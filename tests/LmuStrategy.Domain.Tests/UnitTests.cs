using System.Text.Json;
using LmuStrategy.Domain;

namespace LmuStrategy.Domain.Tests;

public class UnitTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Physical_quantities_reject_negative_and_nonfinite_values(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FuelLitres(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Seconds(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FuelPerLapLitres(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VePerLapPercentagePoints(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TyreWearPerLapPercentagePoints(value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Percent_levels_are_bounded_but_points_are_not_levels(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VePercent(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TyreRemainingPercent(value));
        if (double.IsFinite(value)) Assert.Equal(value, new PercentagePoints(value).Value);
    }

    [Fact]
    public void Counts_and_fractions_have_explicit_boundaries()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Laps(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TyreCount(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fraction(1.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fraction(-0.01));
        Assert.Equal(0, new Laps(0).Value);
        Assert.Equal(100, new VePercent(100).Value);
        Assert.Equal(-12, new Celsius(-12).Value);
        Assert.Equal(-5, new SecondsDelta(-5).Value);
        Assert.Equal(120, new PercentagePoints(120).Value);
    }

    [Fact]
    public void Units_cannot_be_implicitly_substituted_and_roundtrip_without_conversion()
    {
        var units = new[] { typeof(FuelLitres), typeof(Seconds), typeof(VePercent),
            typeof(PercentagePoints), typeof(Laps), typeof(VePerLapPercentagePoints) };
        Assert.All(units, type => Assert.DoesNotContain(type.GetMethods(),
            method => method.Name is "op_Implicit"));
        var value = new VePerLapPercentagePoints(4.25);
        Assert.Equal(value, DomainJson.Deserialize<VePerLapPercentagePoints>(DomainJson.Serialize(value)));
        Assert.Throws<JsonException>(() => DomainJson.Deserialize<VePercent>("{}"));
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainJson.Deserialize<VePercent>("{\"value\":101}"));
    }
}
