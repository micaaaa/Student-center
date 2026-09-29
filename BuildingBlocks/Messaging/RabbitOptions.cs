namespace StudentCenter.Messaging;

public sealed class RabbitOptions
{
    public bool Enabled { get; set; }
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName) || Port is < 1 or > 65535
            || string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
            throw new InvalidOperationException("Configure RabbitMQ host, port and credentials before enabling messaging.");
    }
}
