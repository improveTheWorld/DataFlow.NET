using Confluent.Kafka;

namespace iCode.Log
{
    class EventHubKafkaLogger : ILoggerTarget, IDisposable
    {
        IProducer<Null, string> KafkaProducer;
        string Topic;
        public bool AutoFlush = true;
        public TimeSpan AutoFlushTimeSpan = TimeSpan.FromSeconds(10);
       
        public EventHubKafkaLogger(string eventHubNamespace, string connectionString, string topic)
        {
            Topic = topic;
            var config = new ProducerConfig
            {
                BootstrapServers = $"{eventHubNamespace}.servicebus.windows.net:9093",
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = "$ConnectionString",
                SaslPassword = connectionString,
                SocketKeepaliveEnable = true
            };
            KafkaProducer = new ProducerBuilder<Null, string>(config).Build();
        }

        public void Dispose()
        {
            KafkaProducer.Dispose();
        }

        public void Log(string message, LogLevel logging)
        {
            if (!string.IsNullOrEmpty(message))
            {
                try
                {
                    KafkaProducer.Produce(Topic, new Message<Null, string> { Value = $"[{logging}] : {message}" });
                    if(AutoFlush)
                    {
                        Flush();
                    }
                    KafkaProducer.Flush();
                }
                catch (ProduceException<Null, string> e)
                {
                    Console.WriteLine($"Erreur lors de l'envoi du message: {e.Error.Reason}");
                }
            }            
        }
        public void Flush()
        {
            KafkaProducer?.Flush(AutoFlushTimeSpan);
        }
    }
}

