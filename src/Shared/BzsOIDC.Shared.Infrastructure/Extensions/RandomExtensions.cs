namespace BzsOIDC.Shared.Infrastructure.Extensions;

public static class RandomExtensions
{
    extension(Random random)
    {
        /// <summary>
        /// 在指定范围内生成随机小数。
        /// </summary>
        /// <param name="min">范围最小值。</param>
        /// <param name="max">范围最大值。</param>
        /// <returns>生成的随机小数。</returns>
        public double NextDouble(double min, double max)
        {
            if (min > max)
                return random.NextDouble() * (min - max) + max;
            return random.NextDouble() * (max - min) + min;
        }
    }
}
