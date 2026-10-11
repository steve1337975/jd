namespace NChecker
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            await App.Instance.Start();
        }
    }
}