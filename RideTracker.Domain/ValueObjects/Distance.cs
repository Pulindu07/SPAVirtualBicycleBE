namespace RideTracker.Domain.ValueObjects;

public record Distance
{
    public double Kilometers { get; init; }

    public Distance(double kilometers)
    {
        if (kilometers < 0)
            throw new ArgumentException("Distance cannot be negative", nameof(kilometers));
        
        Kilometers = kilometers;
    }

    public double ToMeters() => Kilometers * 1000;
    public double ToMiles() => Kilometers * 0.621371;
}

