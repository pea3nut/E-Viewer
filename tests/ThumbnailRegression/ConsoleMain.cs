using System;
using System.Threading.Tasks;

namespace ThumbnailRegression {
    internal static class ConsoleMain {
        public static async Task<int> Main(string[] args) {
            try {
                var tests = new Regression(Console.WriteLine);
                await tests.RunAsync(Array.IndexOf(args, "--smoke") >= 0);
                Console.WriteLine("PASS: " + tests.Checks + " production-linked checks");
                return 0;
            } catch (Exception error) {
                Console.Error.WriteLine(error);
                return 1;
            }
        }
    }
}
