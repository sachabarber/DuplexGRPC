using Demos;

namespace Grpc.Demo.Server
{
    public class SubscriptionEvent
    {
        public Event Event { get; set; }
        public string SubscriptionId { get; set; }
    }
}
