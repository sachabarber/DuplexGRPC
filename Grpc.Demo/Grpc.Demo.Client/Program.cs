using System;
using System.Threading.Tasks;
using Demos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using grpc = global::Grpc.Core;

namespace Grpc.Demo.Client
{
    class Program
    {
        public static void Main(string[] args)
        {
            Channel channel = new Channel("127.0.0.1:50051", ChannelCredentials.Insecure);
            var client = new PubSub.PubSubClient(channel);
            var subscriber = new Subscriber(client);

            //do some request/reponse stuff in a thread
            Task.Run(async () =>
            {
                while (true)
                {
                    var reply = client.GetAnEvent(new Empty());
                    Console.WriteLine($"GetAnEvent : {reply}");
                    await Task.Delay(2000);
                }
            }).ConfigureAwait(false).GetAwaiter();

            //do some pub.sub stuff in a thread
            Task.Run(async () =>
            {
                await subscriber.Subscribe(Guid.NewGuid().ToString("N"));
            }).ConfigureAwait(false).GetAwaiter();
            

            Console.WriteLine("Hit key to unsubscribe");
            Console.ReadLine();

            subscriber.Unsubscribe();

            Console.WriteLine("Unsubscribed...");

            Console.WriteLine("Hit key to exit...");
            Console.ReadLine();
        }

    }


    public class Subscriber
    {
        private static Demos.PubSub.PubSubClient _pubSubClient;
        private Subscription _subscription;

        public Subscriber(Demos.PubSub.PubSubClient pubSubClient)
        {
            _pubSubClient = pubSubClient;
        }

        public async Task Subscribe(string subscriptionId)
        {
            _subscription = new Subscription() { Id = subscriptionId };
            Console.WriteLine($">> SubscriptionId : {subscriptionId}");
            using (var call = _pubSubClient.Subscribe(_subscription))
            {
                //Receive
                var responseReaderTask = Task.Run(async () =>
                {
                    while (await call.ResponseStream.MoveNext())
                    {
                        Console.WriteLine("Event received: " + call.ResponseStream.Current);
                    }
                });

                await responseReaderTask;
            }
        }

        public void Unsubscribe()
        {
            _pubSubClient.Unsubscribe(_subscription);
        }
    }
}
