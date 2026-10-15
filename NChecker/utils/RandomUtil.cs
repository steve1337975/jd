namespace NChecker.utils
{
    internal static class RandomUtil
    {
        static Random random = new Random();
        internal static long RandomLong(long min, long max) => random.NextInt64(min, max + 1);
        internal static int RandomInt(int min, int max) => random.Next(min, max + 1);
        internal static T Select<T>(T[] a1)
        {
            if (a1.Length == 0) return default;
            int index = random.Next(0, a1.Length);
            return a1[index];
        }
        internal static void Shuffle<T>(T[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                int r = Random.Shared.Next(i, array.Length);
                (array[i], array[r]) = (array[r], array[i]);
            }
        }
        internal static void Shuffle<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int r = Random.Shared.Next(i, list.Count);
                (list[i], list[r]) = (list[r], list[i]);
            }
        }
    }
}
