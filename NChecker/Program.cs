using NChecker.utils;

namespace NChecker
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Logger.Info("Hello, World!");


            await App.Instance.Start();
        }
    }
}