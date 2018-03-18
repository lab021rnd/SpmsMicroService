using Microsoft.AspNet.SignalR;
using Microsoft.Azure.Devices.Common;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MessageServer
{
    public class MessageHub : Hub
    {
        private static string connectionString = "HostName=test180221IoTHub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xDjVtuwU4qx1ughskK8rr61IfTePCr3eGLhgj2yGFFM=";
        private static string iotHubD2cEndpoint = "messages/events";
        private static EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(connectionString, iotHubD2cEndpoint);
        private static int eventHubPartitionsCount = eventHubClient.GetRuntimeInformation().PartitionCount;

        private string consumerGroupName = "web";


        private static CancellationTokenSource tokenSource;
        private static string url = string.Empty;

        public void Hello()
        {
            this.Clients.Caller.broadcastMessage(DateTime.UtcNow.ToLongTimeString(), "Info", "ALL/HELLO OCEAN~");
        }

        public void Send(string longdate, string topic, string message)
        {
            Clients.All.broadcastMessage(longdate, topic, message);
        }

        public async Task Iot()
        {
            string partition = EventHubPartitionKeyResolver.ResolveToPartition("testDevice180221", eventHubPartitionsCount);
            var eventHubReceiver = eventHubClient.GetConsumerGroup(consumerGroupName).CreateReceiver(partition, DateTime.UtcNow);
            while (true)
            {
                var eventData = await eventHubReceiver.ReceiveAsync(TimeSpan.FromSeconds(1));

                if (eventData != null)
                {
                    var data = Encoding.UTF8.GetString(eventData.GetBytes());
                    var enqueuedTime = eventData.EnqueuedTimeUtc.ToLocalTime();

                    // Display only data from the selected device; otherwise, skip.
                    var connectionDeviceId = eventData.SystemProperties["iothub-connection-device-id"].ToString();

                    if (string.CompareOrdinal("testDevice180221", connectionDeviceId) == 0)
                    {
                        Send(enqueuedTime.ToShortDateString(), connectionDeviceId, data);
                    }

                }
            }
        }
    }
}