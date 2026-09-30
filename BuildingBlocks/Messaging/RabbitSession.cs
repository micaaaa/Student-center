using RabbitMQ.Client;

namespace StudentCenter.Messaging;

// Shared transport only. Business entities and databases remain owned by each service.
public sealed class RabbitSession(IConnection connection, IChannel channel) : IAsyncDisposable
{
    public const string Exchange = "student-center.events";
    public const string EligibilityEvent = "AccommodationEligibilityGranted";
    public const string EligibilityQueue = "accommodation.eligibility";
    public const string RejectedQueue = "accommodation.eligibility.rejected";

    public IChannel Channel { get; } = channel;

    public async Task DeclareFoodQueuesAsync(CancellationToken ct)
    {
        foreach (var queue in new[] { "billing.food", "notification.food" })
        {
            await Channel.QueueDeclareAsync(queue, durable: true, exclusive: false,
                autoDelete: false, cancellationToken: ct);
            await Channel.QueueBindAsync(queue, Exchange, "MealPurchased", cancellationToken: ct);
        }
    }

    public async Task DeclareAccommodationQueuesAsync(CancellationToken ct)
    {
        // Durable subscriptions retain events until the downstream services are running.
        foreach (var queue in new[] { "billing.accommodation", "notification.accommodation" })
        {
            await Channel.QueueDeclareAsync(queue, durable: true, exclusive: false,
                autoDelete: false, cancellationToken: ct);
            foreach (var eventType in new[]
            {
                "AccommodationAssigned", "StudentMovedIn", "StudentMovedOut", "AccommodationAssignmentCancelled"
            })
            {
                await Channel.QueueBindAsync(queue, Exchange, eventType, cancellationToken: ct);
            }
        }
    }

    public static async Task<RabbitSession> OpenAsync(RabbitOptions options, CancellationToken ct)
    {
        options.Validate();
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            UserName = options.UserName,
            Password = options.Password,
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
        };
        var connection = await factory.CreateConnectionAsync(ct);
        try
        {
            var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(true, true), ct);
            try
            {
                await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true,
                    autoDelete: false, cancellationToken: ct);
                await channel.QueueDeclareAsync(RejectedQueue, durable: true, exclusive: false,
                    autoDelete: false, cancellationToken: ct);
                await channel.QueueDeclareAsync(EligibilityQueue, durable: true, exclusive: false,
                    autoDelete: false, arguments: new Dictionary<string, object?>
                    {
                        ["x-dead-letter-exchange"] = "",
                        ["x-dead-letter-routing-key"] = RejectedQueue
                    }, cancellationToken: ct);
                await channel.QueueBindAsync(EligibilityQueue, Exchange, EligibilityEvent,
                    cancellationToken: ct);
                return new RabbitSession(connection, channel);
            }
            catch
            {
                await channel.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Channel.DisposeAsync();
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
