using NChecker.utils;

namespace NChecker
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Logger.Info("Hello, World!");
            await App.Instance.Start();
        }


        internal class test2
        {
            internal int id;
        }

        class test1
        {
            internal test2[] klass;
            internal int id;
        }

        internal static void test()
        {
            test1[] arr = new test1[] { new test1() };
        }
    }
}