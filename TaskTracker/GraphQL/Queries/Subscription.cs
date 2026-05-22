using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using TaskTracker.Models;

namespace TaskTracker.GraphQL.Queries;

public class Subscription
{
    [Subscribe]
    public User CreateUser([EventMessage] User user)
    {
        return user;
    }


    [SubscribeAndResolve]
    public ValueTask<ISourceStream<User>> UpdateUser(Guid userId, [Service] ITopicEventReceiver topicEventReceiver)
    {
        string topicName = $"{userId}_{nameof(Subscription.UpdateUser)}";

        return topicEventReceiver.SubscribeAsync<User>(topicName);
    }
}