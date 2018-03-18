using CodeFirstModels.Models.Lab021Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Lab021Service
{
    public static class kakaotalkbot
    {
        private static TraceWriter _log;
        private static StringBuilder _mailBody;

        [FunctionName("kakaotalkbot")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "kakaotalkbot/{request}")]HttpRequestMessage req, string request, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");
            _log = log;
            _mailBody = new StringBuilder();
            if (request == "keyboard")
            {
                var jarray = new JArray() { "선박일정", "기능추가","배고파" };
                var myObj = new { type = "buttons", buttons = jarray };
                var jsonToReturn = JsonConvert.SerializeObject(myObj);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonToReturn, Encoding.UTF8, "application/json")
                };
            }

            if (request == "message")
            {
                PostData data = await req.Content.ReadAsAsync<PostData>();

                var content = req.Content;
                if (data.content.ToString() == "배고파")
                {
                    var jarray = new JArray() { "선박일정", "기능추가", "배고파"};
                    var keyboardObject = new { type = "buttons", buttons = jarray };
                    var messageObject = new { text = "으이구, 이젠 살좀 빼야지!!!" };
                    var returnObject = new { keyboard = keyboardObject, message = messageObject };
                    var jsonToReturn = JsonConvert.SerializeObject(returnObject);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(jsonToReturn, Encoding.UTF8, "application/json")
                    };
                }

                if (data.content.ToString() == "기능추가")
                {
                    var jarray = new JArray() { "선박일정", "기능추가", "배고파" };
                    var keyboardObject = new { type = "buttons", buttons = jarray };
                    var messageObject = new { text = "기능 추가가 필요하단 말이지? 한번 생각해볼께.." };
                    var returnObject = new { keyboard = keyboardObject, message = messageObject };
                    var jsonToReturn = JsonConvert.SerializeObject(returnObject);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(jsonToReturn, Encoding.UTF8, "application/json")
                    };
                }

                if (data.content.ToString() == "선박일정")
                {
                    MakeEmailBody();
                    var message = _mailBody.ToString();
                    var jarray = new JArray() { "선박일정", "기능추가", "배고파" };
                    var keyboardObject = new { type = "buttons", buttons = jarray };
                    var messageObject = new { text = message };
                    var returnObject = new { keyboard = keyboardObject, message = messageObject };
                    var jsonToReturn = JsonConvert.SerializeObject(returnObject);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(jsonToReturn, Encoding.UTF8, "application/json")
                    };
                }
            }
            return req.CreateResponse(HttpStatusCode.OK, "Hello");
        }

        public static void MakeEmailBody()
        {
            var _lab021Model = new LAB021_MODEL();
            var shipList = _lab021Model.ETA_REPORT.OrderByDescending(d => d.NATION).ThenBy(d=>d.ETA_TIME).ToList();

            var ttlindex = shipList.Count();
            var index = 0;
            var indexKorea = 0;
            var indexChina = 0;
            var indexKoreaLast = shipList.Where(d => d.NATION.ToLower() == "korea").Count();
            foreach (var item in shipList)
            {
                index++;
                if (index == ttlindex)
                {
                    if (item.NATION.ToLower() == "korea")
                    {
                        if (indexKorea == 0)
                        {
                            _mailBody.AppendFormat("☆먼저 [한국]에 오는 선박일정을 알려줄께!! \n");
                        }
                        _mailBody.AppendFormat("-" + item.SHIP_NAME + "호가 " + item.ETA_TIME.ToString("M월dd일") + ", " + item.ETA_PORT.Split('/')[0] + "항에 도착해~(뿌듯)\n출장 갈때 늘 안전운전하는 것 잊지마!~(찡긋)");
                        indexKorea++;
                    }
                    if (item.NATION.ToLower() == "china")
                    {
                        if (indexChina == 0 && indexKorea == 0)
                        {
                            _mailBody.AppendFormat("☆[한국]에 오는 선박은 없네. [중국]에 오는 선박일정만 알려줄께!! \n\n");
                        }
                        else if (indexChina == 0)
                        {
                            _mailBody.AppendFormat("\n\n☆다음은 [중국]에 오는 선박일정이야!! \n");
                        }

                        _mailBody.AppendFormat("-" + item.SHIP_NAME + "호가 " + item.ETA_TIME.ToString("M월dd일") + ", " + item.ETA_PORT.Split('/')[0] + "항에 도착해~(뿌듯)\n출장 갈때 늘 안전운전 알지?~오늘도 홧팅!(찡긋)");
                        indexChina++;
                    }
                }
                else
                {
                    if (item.NATION.ToLower() == "korea")
                    {
                        if (indexKorea == 0)
                        {
                            _mailBody.AppendFormat("☆먼저 [한국]에 오는 선박일정을 알려줄께!! \n");
                        }

                        if (indexKoreaLast-1 == indexKorea)
                        {
                            _mailBody.AppendFormat("-" + item.SHIP_NAME + "호가 " + item.ETA_TIME.ToString("M월dd일") + ", " + item.ETA_PORT.Split('/')[0] + "항에 들어와.\n");
                        }
                        else
                        {
                        _mailBody.AppendFormat("-" + item.SHIP_NAME + "호가 " + item.ETA_TIME.ToString("M월dd일") + ", " + item.ETA_PORT.Split('/')[0] + "항에,");

                        }
                        indexKorea++;
                    }
                    if (item.NATION.ToLower() == "china")
                    {
                        if (indexChina == 0 && indexKorea == 0)
                        {
                            _mailBody.AppendFormat("☆[한국]에 오는 선박은 없네. [중국]에 오는 선박일정만 알려줄께!! \n");
                        }
                        else if (indexChina == 0)
                        {
                            _mailBody.AppendFormat("☆다음은 [중국]에 오는 선박일정이야!! \n");
                        }
                        _mailBody.AppendFormat("-" + item.SHIP_NAME + "호가 " + item.ETA_TIME.ToString("M월dd일") + ", " + item.ETA_PORT.Split('/')[0] + "항에,");
                        indexChina++;
                    }
                }
                _mailBody.AppendFormat("\n");
            }
        }
    }

    public class PostData
    {
        public string user_key { get; set; }
        public string type { get; set; }
        public string content { get; set; }
    }
}