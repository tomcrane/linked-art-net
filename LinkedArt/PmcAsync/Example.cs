using Microsoft.Extensions.Hosting;

namespace PmcAsync
{
    internal class Example : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("started");
            await StopAsync(cancellationToken);
        }


        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("stopped");
            return Task.CompletedTask;
        }

    }
}
