[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0) [![Build status](https://ci.appveyor.com/api/projects/status/j2a8skqibee6d7vt/branch/master?svg=true)](https://ci.appveyor.com/project/JezzSantos/servicestack-webhooks/branch/master) [![NuGet](https://img.shields.io/nuget/v/ServiceStack.Webhooks.svg?label=ServiceStack.Webhooks)](https://www.nuget.org/packages/ServiceStack.Webhooks) [![NuGet](https://img.shields.io/nuget/v/ServiceStack.Webhooks.Azure.svg?label=ServiceStack.Webhooks.Azure)](https://www.nuget.org/packages/ServiceStack.Webhooks.Azure) 

# ServiceStack.Webhooks
Add Webhooks to your ServiceStack services

# Overview

This project aims to make it very easy to expose your own Webhooks to your ServiceStack services, and to manage 3rd party subscriptions to those webhooks. 

The project has these aims:

1. To deliver this capability in a small set of nuget packages. For example: The `ServiceStack.Webhooks` package would deliver the `AppHost` plugin (`WebhookFeature`) and the ability to manage subscriptions (built-in services, operations etc.), and the components to raise custom events from within your service (i.e. `IWebhooks.Publish(string eventName, TDto data)`). Then other packages like `ServiceStack.Webhooks.Azure` or `ServiceStack.Webhooks.Redis` etc. would deliver various technologiess with components responsible for relaying/delivering events to registered subscribers (over HTTP).
2. Make it simple to configure the Webhooks in your service (i.e. in your `AppHost` just add the `WebhookFeature` plugin, and then register your chosen technology components for it).
3. Make it each component extensible and customizable to work with your services. i.e. allowing you to use your favorite data repositories (for subsciption management), and to integrate the pub/sub mechanics with your favorite technologies in your host architecture. (i.e. buses, queues, functions, etc.)

For example: In one service, you may want to store the Webhook subscriptions in a MongoDB database, and have an Azure worker role relay the events to subscribers from a (reliable) cloud queue.
In another service, you may want to store subscriptions in Ormlite SQL database, and relay events to subscribers directly from within the same service on a background thread.

The choice should be yours. 

If you cant find the component you want for your architecture, it should be easy for you to build add your own and just plug it in.

# Contribute!

Want to get involved? Want to help add this capability to your services? just send us a message or pull-request!

# Getting Started

Install from NuGet:
```
Install-Package ServiceStack.Webhooks
```

Simply add the `WebhookFeature` in your `AppHost.Configure()` method:

```
public override void Configure(Container container)
{
    Plugins.Add(new WebhookFeature());
}
```

Note: You must add the `WebhookFeature` after you use either of these features:
* `Plugins.Add(new ValidationFeature();`
* `Plugins.Add(new AuthFeature());`

## Subscription Service

The `WebhookFeature` plugin automatically installs a built-in (and secure) subscription management API in your service on the following routes:

* POST /webhooks/subscriptions - creates a new subscription (for the current user)
* GET /webhooks/subscriptions - lists all subscriptions (for the current user)
* GET /webhooks/subscriptions/{Id} - gets the details of the subscription
* PUT /webhooks/subscriptions/{Id} - updates the subscription
* DELETE /webhooks/subscriptions/{Id} - deletes the subscription

This allows any users of your web service to create webhook registrations (subscribers to webhook events) for the events you raise in your service.

Note: Webhook subscriptions will be associated to the UserId (`ISession.UserId`) of the user using your service.

Note: This service uses role-based authorization to restrict who can call what, you can customize those roles. (see later)

## Components

By default, the `WebhookFeature` uses a `MemoryWebhookSubscriptionStore` to store all registered subscriptions and a `AppHostWebhookEventSink` to relay raised events directly from your service to subscribers.

**WARNING:** In production systems these default components will need to be replaced, by customizing your configuration of the `WebhookFeature`: 

* Configure a `IWebhookSubscriptionStore` with one that is more appropriate to more persistent storage, like an OrmLiteStore or RedisStore, or a stores subscriptions using a database of your choice. WARNING: If you don't do this, and you continue to use the built-in `MemoryWebhookSubscriptionStore` your subscriptions will be lost when your host/site is restarted.
* (Optional) Consider configuring a `IWebhookEventSink` with one that introduces some buffering between raising events and POSTing them to registered subscribers, like an Trigger, Queue, Bus-based implementation of your choice. WARNING: If you don't do this, and you continue to use the built-in `AppHostWebhookEventSink` your subscribers will be notified in the same thread that you raised the event, which can slow down your service significantly. 

## Raising Events

To raise events from your own services, add the `IWebhooks` dependency to your service, and call: `IWebhooks.Publish<TDto>(string eventName, TDto data)`. Simple as that.

```
internal class HelloService : Service
{
    public IWebhooks Webhooks { get; set; }

    public HelloResponse Any(Hello request)
    {
        Webhooks.Publish("hello", new HelloEvent());
    }
}
```

## How It Works

When you register the `WebhookFeature` in your AppHost, it installs the subscriptions API, and the basic components to support raising events.

By default, the `AppHostWebhookEventSink` is used as the event sink.

When events are raised to it, the sink queries the `ISubscriptionsService.Search(eventName)` (in-proc) to fetch all the subscriptions to POST events to. It caches those subscriptions for a TTL (say 60s), to reduce the number of times the query for the same event is made (to avoid chatter as events are raised in your services). Then is dispatches the notification of that event to all registered subscribers (over HTTP). It will retry 3 times before giving up (`EventServiceClient.Post`).

![](https://raw.githubusercontent.com/jezzsantos/ServiceStack.Webhooks/master/docs/images/AppHostEventSink.PNG)

WARNING: The `AppHostWebhookEventSink` can work well in testing, but it is going to slow down your service request times, as it has to notify each of the subscribers, and that network latency is added to the call time of your API (since it is done in-proc and on the same thread as that of the web request that raised the event).

* We recommend only using the `AppHostWebhookEventSink` in testing and non-production systems.
* We recommend, configuring a `IWebhookEventSink` that scales better with your architecture, and decouples the raising of events from the notifying of subscribers.

## Customizing

There are various components of the webhook architecture that you can extend with your own pieces to suit your needs:

### Subscription Service

The subscription service is automatically built-in to your service when you add the `WebhookFeature`.
If you prefer to roll your own subscription service, you can turn off the built-in one like this:

```
public override void Configure(Container container)
{
   // Add other plugins first

    Plugins.Add(new WebhookFeature
    {
        IncludeSubscriptionService = false
    });
}
```

By default, the subscription service is secured by role-based access if you already have the `AuthFeature` plugin in your AppHost.
If you don't use the `AuthFeature` then the subscription service is not secured, and can be used by anyone using your API.

If you use the `AuthFeature`, rememmber to add the `WebhookFeature` plugin after you add the `AuthFeature` plugin.

When the subscription service is secured, by default, the following roles are protecting the following operations:
* POST /webhooks/subscriptions - "user"
* GET /webhooks/subscriptions - "user"
* GET /webhooks/subscriptions/{Id} - "user"
* PUT /webhooks/subscriptions/{Id} - "user"
* DELETE /webhooks/subscriptions/{Id} - "user"
* GET /webhooks/subscriptions/search - "service"

These roles are configurable by setting the following properties of the `WebhookFeature` when you register it:
```
public override void Configure(Container container)
{
    // Add the AuthFeature plugin first
    Plugins.Add(new AuthFeature(......);

    Plugins.Add(new WebhookFeature
    {
        SubscriptionAccessRoles = "accessrole1",
        SubscriptionSearchRoles = "searchrole1;searchrole2"
    });

}
```

### Subscription Store

Subscriptons for webhooks need to be stored (`IWebhookSubscriptionStore`), once a user of your service registers a webhook (using the registration API: `POST /webhooks/subscriptions`)

You specify your store by registering it in the IOC container.
If you specify no store, the default `MemoryWebhookSubscriptionStore` will be used, but beware that your subscriptions will be lost whenever your service is restarted.

```
public override void Configure(Container container)
{
    // Register your own subscription store
    container.Register<IWebhookSubscriptionStore>(new MyDbSubscriptionStore());

    Plugins.Add(new WebhookFeature();
}
```

### Events Sink

When events are raised they are passed to sink (typically temporarily, until relayed to all registered subscribers) in the event sink (`IWebhookEventSink`). Ideally, for scalability and performance, that is not done on the same thread nor in the same process.

You specify your sink by registering it in the container.
If you specify no sink, the default `AppHostWebhookEventSink` will be used, but beware that this sink is not optimized for scale in production systems.

```
public override void Configure(Container container)
{
    // Register your own event store
    container.Register<IWebhookEventSink>(new MyDbEventSink());

    Plugins.Add(new WebhookFeature();
}
```

## Azure Extensions

If you deploy your web service to Microsoft Azure, you can use Azure storage Tables and Queues to implement the various components of the webhooks.

Subscriptions can be stored in Azure table storage, and events can be queued and relayed by a WorkerRole from an Azure queue.

![](https://raw.githubusercontent.com/jezzsantos/ServiceStack.Webhooks/master/docs/images/AzureQueueEventSink.PNG)

Install from NuGet:
```
Install-Package Servicestack.Webhooks.Azure
```

Add the `WebhookFeature` in your `AppHost.Configure()` method, and register the Azure components:

```
public override void Configure(Container container)
{
    container.Register<IWebhookSubscriptionStore>(new AzureTableWebhookSubscriptionStore());
    container.Register<IWebhookEventSink>(new AzureQueueWebhookEventSink());

    Plugins.Add(new WebhookFeature();
}
```

By default, these services will connect to the local Azure Emulator (UseDevelopmentStorage=true) which might be fine for testing you service, but after you have deployed to your cloud, you will want to provide different storage connection strings.

### Configuring Azure Storage Credentials

If you use the overload constructors, and pass in the `IAppSettings`, like this:

```
public override void Configure(Container container)
{
    container.Register<IWebhookSubscriptionStore>(new AzureTableWebhookSubscriptionStore(appSettings));
    container.Register<IWebhookEventSink>(new AzureQueueWebhookEventSink(appSettings));

    Plugins.Add(new WebhookFeature();
}
```
then:

* `AzureTableWebhookSubscriptionStore` will try to use a setting called: 'AzureTableWebhookSubscriptionStore.ConnectionString' for its storage connection
* `AzureQueueWebhookEventSink` will try to use a setting called: 'AzureQueueWebhookEventSink.ConnectionString' for its storage connection

Otherwise, you can set those values directly in code when you register the services:

```
public override void Configure(Container container)
{
    container.Register<IWebhookSubscriptionStore>(new AzureTableWebhookSubscriptionStore
        {
            ConnectionString = "connectionstring",
        });
    container.Register<IWebhookEventSink>(new AzureQueueWebhookEventSink
        {
            ConnectionString = "connectionstring",
        });

    Plugins.Add(new WebhookFeature();
}
```

### Configuring Azure Resources

By default, 

* `AzureTableWebhookSubscriptionStore` will create and use a table named: 'webhooksubscriptions'
* `AzureQueueWebhookEventSink` will create and use a queue named: 'webhookevents'

You can change those values when you register the services.

```
public override void Configure(Container container)
{
    container.Register<IWebhookSubscriptionStore>(new AzureTableWebhookSubscriptionStore
        {
            TableName = "mytablename",
        });
    container.Register<IWebhookEventSink>(new AzureQueueWebhookEventSink
        {
            QueueName = "myqueuename",
        });

    Plugins.Add(new WebhookFeature();
}
```



