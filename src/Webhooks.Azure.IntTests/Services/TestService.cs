﻿namespace ServiceStack.Webhooks.Azure.IntTests.Services
{
    internal class TestService : Service
    {
        public IWebhooks Webhooks { get; set; }

        public void Any(RaiseEvent request)
        {
            Webhooks.Publish(request.EventName, new TestEvent());
        }
    }

    [Route("/raise")]
    public class RaiseEvent : IReturnVoid
    {
        public string EventName { get; set; }
    }

    public class TestEvent
    {
        public object A { get; set; }

        public object B { get; set; }

        public object C { get; set; }
    }
}