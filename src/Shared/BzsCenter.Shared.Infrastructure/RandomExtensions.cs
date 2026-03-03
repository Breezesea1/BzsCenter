namespace BzsCenter.Shared.Infrastructure;

public static class RandomExtensions
{
    extension(Random random)
    {
        public double NextDouble(double min, double max)
        {
            if (min > max)
                return random.NextDouble() * (min - max) + max;
            return random.NextDouble() * (max - min) + min;
        }
    }
}