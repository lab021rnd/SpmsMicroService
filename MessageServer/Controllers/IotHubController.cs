using Microsoft.AspNet.SignalR;
using Microsoft.Azure.Devices.Common;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace MessageServer.Controllers
{
    public class IotHubController : ApiController
    {

        private static string connectionString = "HostName=test180221IoTHub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xDjVtuwU4qx1ughskK8rr61IfTePCr3eGLhgj2yGFFM=";
        private static string iotHubD2cEndpoint = "messages/events";
        private static EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(connectionString, iotHubD2cEndpoint);
        private static int eventHubPartitionsCount = eventHubClient.GetRuntimeInformation().PartitionCount;
        private string consumerGroupName = "web";




        [System.Web.Http.Authorize]
        [Route("api/IotHubController/test/{id}")]
        [HttpGet]
        async public Task<string> Get(string id)
        {

            var iot = new MessageHub();
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
            var context = GlobalHost.ConnectionManager.GetHubContext<MessageHub>();

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
                        context.Clients.Client(id).broadcastMessage(enqueuedTime.ToShortDateString(), connectionDeviceId, data);
                        //context.Clients.Client.Send(enqueuedTime.ToShortDateString(), connectionDeviceId, data);
                    }
                }
            }

          
            return "";
            
        }

        // GET api/<controller>/5
        public string Get(int id)
        {
            return "value";
        }
    }
}