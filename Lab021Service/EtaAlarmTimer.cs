using CodeFirstModels.Models.Lab021Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Lab021Service
{
    public static class EtaAlarmTimer
    {
        private static List<ShipItem> _shipList;
        private static TraceWriter _log;
        private static List<ETA_REPORT> _etaReport = new List<ETA_REPORT>();
        private static string _apiKey = "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";
        private static bool _functionIsRunningOrNot = false;
        private static StringBuilder _mailBody;

        [FunctionName("EtaAlarmTimer")]
        public static void Run([TimerTrigger("0 30 23 * * *")]TimerInfo myTimer, TraceWriter log)
        {
            _log = log;
            _shipList = new List<ShipItem>();
            _mailBody = new StringBuilder();
            var lab021Model = new LAB021_MODEL();
            
            lab021Model.SaveChanges();
            shipListInit();

            if (_functionIsRunningOrNot == true)
            {
                _log.Info($"Other Instance is Running at: {DateTime.Now}");
                return;
            }
            _functionIsRunningOrNot = true;
            try
            {
                SendMail();
            }
            catch (Exception e)
            {
                _log.Info($"Error!! : {e.ToString()}");
                _functionIsRunningOrNot = false;
            }
            finally
            {
                _functionIsRunningOrNot = false;
            }
        }

        public static void SendMail()
        {
            var client = new SendGridClient(_apiKey);

            var from = new EmailAddress("lab021@lab021.co.kr", "Office Bot");
            var subject = "ETA ALIMY - " + DateTime.Now.ToString();

            var to = new EmailAddress("service@lab021.co.kr", "서비스팀");
            var plainTextContent = "";

            var mailBody = MakeEmailBody();
            var htmlContent = mailBody.ToString();
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            var response = client.SendEmailAsync(msg);
        }

        public static StringBuilder MakeEmailBody()
        {
            var mailBody = new StringBuilder();
            _etaReport = new List<ETA_REPORT>();

            foreach (var item in _shipList)
            {
                var endDt = DateTime.UtcNow.AddDays(3).ToString("yyyyMMdd");
                var startDt = DateTime.UtcNow.AddDays(-5).ToString("yyyyMMdd");
                var apiUrl = "http://poserp.possm.com/comm/restful/getPositionRpt.ex?vslCd=" + item.shipCode + "&fromDt=" + startDt + "&toDt=" + endDt;

                WebClient webClient = new WebClient();
                Stream stream = webClient.OpenRead(apiUrl);
                string responseJSON = new StreamReader(stream).ReadToEnd();

                var rootResult = JsonConvert.DeserializeObject<dynamic>(responseJSON);
                foreach (var item2 in rootResult)
                {
                    foreach (var item3 in item2)
                    {
                        Dictionary<string, string> temp = new Dictionary<string, string>();

                        foreach (var item4 in item3)
                        {
                            temp.Add(Convert.ToString(item4.Name), Convert.ToString(item4.Value));
                        }
                        try
                        {
                            var koreacheck = ParseToString(temp["ETA PORT"]).ToLower().Contains("korea");
                            var chniacheck = ParseToString(temp["ETA PORT"]).ToLower().Contains("china");

                            if (koreacheck)
                            {
                                _etaReport.Add(new ETA_REPORT
                                {
                                    SHIP_NAME = ParseToString(temp["VESSEL NAME"]),
                                    REPORT_TIME = ParseToDate(temp["REPORT TIME"]),
                                    ETA_PORT = ParseToString(temp["ETA PORT"]),
                                    ETA_TIME = ParseToDate(temp["ETA TIME"]),
                                    NATION = ParseToString("KOREA")
                                });
                            }
                            else if (chniacheck)
                            {
                                _etaReport.Add(new ETA_REPORT
                                {
                                    SHIP_NAME = ParseToString(temp["VESSEL NAME"]),
                                    REPORT_TIME = ParseToDate(temp["REPORT TIME"]),
                                    ETA_PORT = ParseToString(temp["ETA PORT"]),
                                    ETA_TIME = ParseToDate(temp["ETA TIME"]),
                                    NATION = ParseToString("CHINA")
                                });
                            }
                        }
                        catch { }
                    }
                }
            }

            using (var lab021Model = new LAB021_MODEL())
            {
                var resulttemp = _etaReport.OrderByDescending(d => d.REPORT_TIME).ToList();

                var resulttemp2 = new List<ETA_REPORT>();
                foreach (var item in resulttemp)
                {
                    if (resulttemp2.Where(d => d.SHIP_NAME == item.SHIP_NAME).Count() == 0)
                    {
                        resulttemp2.Add(item);
                    }
                }

                var resulttemp3 = resulttemp2.OrderByDescending(d => d.NATION).ThenBy(d=>d.ETA_TIME).ToList();
                lab021Model.Database.ExecuteSqlCommand("TRUNCATE TABLE [" + "ETA_REPORT" + "]");
                mailBody.AppendFormat("ETA 알리미");
                mailBody.AppendFormat("<br>");
                mailBody.AppendFormat("===============================================");
                mailBody.AppendFormat("<br>");
                foreach (var item in resulttemp3)
                {
                    mailBody.AppendFormat("선 박 이 름  : " + item.SHIP_NAME);
                    mailBody.AppendFormat("<br>");
                    mailBody.AppendFormat("보 고 시 각  : " + item.REPORT_TIME.ToString());
                    mailBody.AppendFormat("<br>");
                    mailBody.AppendFormat("도착예정장소 : " + item.ETA_PORT);
                    mailBody.AppendFormat("<br>");
                    mailBody.AppendFormat("도착예정시각 : " + item.ETA_TIME.ToString());
                    mailBody.AppendFormat("<br>");
                    mailBody.AppendFormat("국        가 : " + item.NATION);
                    mailBody.AppendFormat("<br>");
                    mailBody.AppendFormat("===============================================");
                    mailBody.AppendFormat("<br>");
                    lab021Model.ETA_REPORT.Add(item);
                    lab021Model.SaveChanges();
                }
            }
            return mailBody;
        }

        private static void shipListInit()
        {
            var shipNames = new string[] { "PAN BEGONIA", "PAN CROCUS", "PAN AMBER", "PAN BONITA", "PAN KRISTINE", "ARBORELLA ", "BRASSIANA", "DELICATA ", "HALOPHYLA", "CITRIODORA", "PAN MUTIARA", "PAN ENERGEN", "PAN CLOVER", "PAN FLOWER", "PAN PRIDE", "PAN RAPIDO", "PAN QUEEN", "PAN SPIRIT", "PAN GLORIS", "PAN GLOBAL ", "PAN HORIZON", "PAN DAISY", "PAN JASMINE", "PAN UNITY", "PAN TOPAZ", "PAN IVY", "PAN HARMONY", "PAN EDELWEISS", "PAN BICORN", "PAN CERES", "PAN MARGARET ", "SUN ORCHID", "POS TOKYO", "POS YOKOHAMA", "PAN FREESIA", "OCEAN MASTER", "NEW JOY", "PAN VIVA", "PAN KYLA", "NEW HERALD", "SEA PONTA DA MADEIRA ", "PAN KOMIPO ", "PAN ADVANCE ", "PAN COSMOS", "PAN DELIGHT", "PAN DANGJIN", "PAN FREEDOM", "PAN ACACIA", "PAN BONA", "PAN DREAM", "PAN EMERALD", "PAN CHAMPION", "PAN IRIS", "PAN JOY", "PAN HOPE", "PAN GOLD", "SUN SHINE", "SUN RISE", "BUM YOUNG", "BUM SHIN", "LNG KOLT", "GRAND ACE7", "SUPER HERO", "GRAMD ACE5", "GRAND ACE11", "GRAND ACE8", "SUPER INFINITY", "SUPER EASTERN", "SUPER FORTE", "GRAND ACE6 ", "GRAND ACE10 ", "GRAND ACE2 ", "GRAND ACE1", "GRAND ACE12" };
            var shipCodes = new string[] { "SPQB", "SPQC", "SPJA", "SPJB", "SPQK", "SPKA", "SPKB", "SPKD", "SPKH", "SPKC", "SPJM", "SPJE", "SPJC", "SPJF", "SPQP", "SPQR", "SPQQ", "SPQS", "SPQG", "SPJG", "SPJH", "SPQD", "SPQJ", "SPQU", "SPQT", "SPQI", "SPQH", "SPQE", "SPXB", "SPXC", "SPQM", "SPQO", "SPTY", "SPYO", "SPQF", "SPOM", "SPNJ", "SPQV", "SPJK", "SPNH", "SPVP", "SPLA", "SPXA", "SPLC", "SPLD", "SPLB", "SPRF", "SPRA", "SPRB", "SPRD", "SPRE", "SPRC", "SPRI", "SPRJ", "SPRH", "SPRG", "SPHA", "SPHB", "SPBY", "SPBS", "LTST", "SPTI", "SPYH", "SPTD", "SPTG", "SPTF", "SPYI", "SPET", "SPFT", "SPTE", "SPTK", "SPTB", "SPTA", "SPTH" };

            foreach (var item in shipNames.Select((value, index) => new { value, index }))
            {
                _shipList.Add(new ShipItem
                {
                    shipName = item.value,
                    shipCode = shipCodes[item.index]
                });
            }
        }

        public static float ParseToSingle(string input)
        {
            float convertFloat;

            if (!float.TryParse(input, out convertFloat))
            {
                return -9999; ;
            }
            return convertFloat;
        }

        public static int ParseToInt(string input)
        {
            int convertInt;

            if (!int.TryParse(input, out convertInt))
            {
                return -9999; ;
            }
            return convertInt;
        }

        public static string ParseToString(string input)
        {
            if (input == null || input == "" || input == "null")
            {
                return "-9999";
            }
            return input;
        }

        public static DateTime ParseToDate(string input)
        {
            DateTime convertedDate;

            if (!DateTime.TryParse(input, out convertedDate))
            {
                return new DateTime(2000, 1, 1); ;
            }

            return convertedDate;
        }

        public static float ParseToPosition(string input)
        {
            if (input == null || input == "" || input == "null")
            {
                return -9999;
            }

            float Position = 0.0f;

            if (input.Last() == 'N' || input.Last() == 'E')
            {
                Position = Convert.ToSingle(input.Substring(0, input.Count() - 1));
            }
            else if (input.Last() == 'S' || input.Last() == 'W')
            {
                Position = Convert.ToSingle(input.Substring(0, input.Count() - 1)) * -1;
            }
            else
            {
                return -9999;
            }

            return Position;
        }
    }

    public class ShipItem
    {
        public string shipName { get; set; }
        public string shipCode { get; set; }
    }
}