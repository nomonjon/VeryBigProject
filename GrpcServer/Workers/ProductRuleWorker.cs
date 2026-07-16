using GrpcServer.Interfaces;

namespace GrpcServer.Workers;

// Sweeps active ProductRules against every product every 15s and paints
// Product.StatusColor with the most severe matching rule's color.
public class ProductRuleWorker(IServiceScopeFactory scopeFactory, ILogger<ProductRuleWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var ruleService = scope.ServiceProvider.GetRequiredService<IProductRuleService>();
                var changed = await ruleService.ApplyActiveRulesAsync();

                if (changed > 0)
                    logger.LogInformation("Product rule sweep updated {Count} product(s).", changed);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Product rule sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
