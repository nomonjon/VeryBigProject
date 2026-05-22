using Client;
using Grpc.Net.Client;


using var channel = GrpcChannel.ForAddress("https://localhost:7100");
var client = new Greeter.GreeterClient(channel);

while (true)
{
    if(int.Parse(Console.ReadLine()!) >= 0)
    {
        var reply = await client.SayHelloAsync(new HelloRequest { Name = "Nomonjon" });
        Console.WriteLine("Greeting: " + reply.Message);
    }

}
Console.ReadKey();