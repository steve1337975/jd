namespace NChecker.utils
{
    internal static class Logger
    {
        internal static void Info(object message) => Log("[INFO] " + message);
        internal static void Error(object message) => Log("[ERR] " + message);
        private static void Log(object message) => Console.WriteLine(message);
    }
}
