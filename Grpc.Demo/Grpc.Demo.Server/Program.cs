using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Demos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Grpc.Demo.Server
{
    public class PubSubImpl : PubSub.PubSubBase
    {
        private readonly BufferBlock<SubscriptionEvent> _buffer = new BufferBlock<SubscriptionEvent>();

        public PubSubImpl()
        {
            SubscriberWritersMap = new Dictionary<string, IServerStreamWriter<Event>>();
        }

        public override async Task Subscribe(Subscription subscription, IServerStreamWriter<Event> responseStream, ServerCallContext context)
        {
            //Dict to hold a streamWriter for each subscriber.
            SubscriberWritersMap[subscription.Id] = responseStream;

            while (SubscriberWritersMap.Count > 0)
            {
                //Wait on BufferBlock from MS Dataflow package.
                var subscriptionEvent = await _buffer.ReceiveAsync();
                if (SubscriberWritersMap.ContainsKey(subscriptionEvent.SubscriptionId))
                {
                    await SubscriberWritersMap[subscriptionEvent.SubscriptionId].WriteAsync(subscriptionEvent.Event);
                }

            }
        }

        public override Task<Unsubscription> Unsubscribe(Subscription request, ServerCallContext context)
        {
            SubscriberWritersMap.Remove(request.Id);
            return Task.FromResult(new Unsubscription() { Id = request.Id });
        }

        public override Task<Event> GetAnEvent(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new Event { Value  = DateTime.Now.ToLongTimeString() });
        }


        public void Publish(SubscriptionEvent subscriptionEvent)
        {
            _buffer.Post(subscriptionEvent);
        }

        public Dictionary<string, IServerStreamWriter<Event>> SubscriberWritersMap { get; private set; }
       
    }


 

    class Program
    {
        const int Port = 50051;

        public static void Main(string[] args)
        {

            //var subManager = new SubscriptionManager();

            var service = new PubSubImpl();
            Core.Server server = new Core.Server
            {
                Services = { Demos.PubSub.BindService(service) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();


            bool shouldRun = true;

            Random rand = new Random(1000);


            Thread t = new Thread(() =>
            {
                while (shouldRun)
                {

                    if (service.SubscriberWritersMap.Any())
                    {
                        var indexedKeys = service.SubscriberWritersMap.Select((kvp, idx) => new
                        {
                            Idx = idx,
                            Key = kvp.Key

                        });

                        var subscriptionIdx = rand.Next(service.SubscriberWritersMap.Count);
                        var randomSubscriptionId = indexedKeys.Single(x => x.Idx == subscriptionIdx).Key;
                        service.Publish(new SubscriptionEvent()
                        {
                            Event = new Event()
                                {Value = $"And event for '{randomSubscriptionId}' {Guid.NewGuid().ToString("N")}"},
                            SubscriptionId = randomSubscriptionId
                        });
                    }

                    Thread.Sleep(2000);

                }
            });
            t.Start();

            Console.WriteLine("Greeter server listening on port " + Port);
            Console.WriteLine("Press any key to stop the server...");




            Console.ReadKey();
            shouldRun = false;

            server.ShutdownAsync().Wait();
        }
    }
}
