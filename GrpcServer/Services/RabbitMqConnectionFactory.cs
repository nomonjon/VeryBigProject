using RabbitMQ.Client;

namespace GrpcServer.Services;

public static class RabbitMqConnectionFactory
{
    public static IConnection CreateWithRetry(string host, int port, string user, string password,
        int maxRetries = 10, int delaySeconds = 5)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = host,
                    Port = port,
                    UserName = user,
                    Password = password,
                    AutomaticRecoveryEnabled = true
                };

                var conn = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                Console.WriteLine($"[RabbitMQ] GrpcServer connected on attempt {attempt}");
                return conn;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RabbitMQ] Attempt {attempt}/{maxRetries} failed: {ex.Message}");
                if (attempt == maxRetries) throw;
                Task.Delay(TimeSpan.FromSeconds(delaySeconds)).GetAwaiter().GetResult();
            }
        }

        throw new InvalidOperationException("Could not connect to RabbitMQ");
    }
}