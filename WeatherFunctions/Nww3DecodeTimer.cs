using CodeFirstModels.Models;
using CodeFirstModels.Models.LogModel;
using CodeFirstModels.Models.OceanModel;
using EntityFramework.Utilities;
using Grib.Api;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace WeatherFunctions
{
    public static class Nww3DecodeTimer
    {
        private static OCEAN_MODEL _oceanModel = new OCEAN_MODEL();
        private static LOG_MODEL _logModel = new LOG_MODEL();

        private static string _storageAccountName = CloudConn.StorageAccountName_OceanRaw;
        private static string _storageAccountKey = CloudConn.StorageAccountKey_OceanRaw;
        private static string _storageConnectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", _storageAccountName, _storageAccountKey);
        private static CloudStorageAccount _storageAccount = CloudStorageAccount.Parse(_storageConnectionString);
        private static CloudBlobClient _blobClient = _storageAccount.CreateCloudBlobClient();

        private static List<string> _decodeList;
        private static List<JOB_LIST_OCEAN_WEATHER> _jobList;
        private static bool _functionIsRunningOrNot = false;
        private static TraceWriter _log;

        [FunctionName("Nww3DecodeTimer")]
        public static void Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            _log = log;
            _decodeList = new List<string>();
            _log.Info($"Nww3 Decord function executed at: {DateTime.Now}");

            if (_functionIsRunningOrNot == true)
            {
                _log.Info($"Other Instance is Running at: {DateTime.Now}");
                return;
            }
            RemoveDirectoryAndFile();
            _functionIsRunningOrNot = true;
            try
            {
                _jobList = _logModel.JOB_LIST_OCEAN_WEATHER.Where(d => d.IS_COMPLETE_JOB == false && d.JOB_TYPE == "NWW3_DECODE").ToList();
                foreach (var item in _jobList)
                {
                    var blobPath = item.FILE_PATH;
                    var year = item.FILE_PATH.Split('\\')[0];
                    var month = item.FILE_PATH.Split('\\')[1];
                    var day = item.FILE_PATH.Split('\\')[2];
                    var fileName = item.FILE_PATH.Split('\\')[3];
                    var hour = item.FILE_PATH.Split('.')[2].Substring(1, 2);
                    var localFilePath = "";

                    _decodeList = new List<string> { year, month, day, hour, fileName, blobPath, localFilePath };
                    Nww3RawFileDownLoad();
                    Nww3NowCastInsert();

                    var whichDb = _logModel.CONDITION_OCEAN_WEATHER.First();

                    if (hour == "00")
                    {
                        if (whichDb.DB_OF_NWW3_FORECAST.ToUpper() == "PROXY")
                        {
                            Nww3ForcastInsertBasic();
                        }
                        else if (whichDb.DB_OF_NWW3_FORECAST.ToUpper() == "BASIC")
                        {
                            Nww3ForcastInsertProxy();
                        }
                    }

                    item.IS_COMPLETE_JOB = true;
                    item.DATE_OF_COMPLETE_JOB_TIME = DateTime.UtcNow;
                    item.REASON_OF_NOT_COMPLETE = "";
                    _logModel.SaveChanges();
                    RemoveDirectoryAndFile();
                }
            }
            catch (Exception e)
            {
                _log.Error($"Error : : {e.ToString()}");
            }
            finally
            {
                RemoveDirectoryAndFile();
                _functionIsRunningOrNot = false;
            }
        }

        public static void Nww3RawFileDownLoad()
        {
            CloudBlobContainer container = _blobClient.GetContainerReference("nww3");
            var localFilePath = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\nww3\");
            localFilePath = Path.Combine(localFilePath, _decodeList[0], _decodeList[1], _decodeList[2], _decodeList[3], _decodeList[4]);
            Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(_decodeList[5]);
            blockBlob.DownloadToFile(localFilePath, FileMode.Create);
            _log.Info($"blob download complete");
            _decodeList[6] = localFilePath;
        }

        public static void RemoveDirectoryAndFile()
        {
            var tempPath = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\nww3\");
            System.IO.DirectoryInfo di = new DirectoryInfo(tempPath);
            _log.Info($"delete start");
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
                _log.Info($"delete file : : {file.Name.ToString()}");
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
                _log.Info($"delete directory : : {dir.Name.ToString()}");
            }
        }

        public static void Nww3NowCastInsert()
        {
            _log.Info($"Start Insert");
            int elementCount = 19; // 한 셋트 데이터 종류 (변경하지 말것)
            int needDecordDateSetCount = 2; // 3시간 데이터 2개를 저장.
            int elementTotalCount = elementCount * needDecordDateSetCount;

            var UTC = new DateTime[2];
            var LAT = new double[2][];
            var LON = new double[2][];
            var HTSGW = new double[2][];
            var MWSDIR = new double[2][];
            var MWSPER = new double[2][];
            var SWELL = new double[2][];
            var SWDIR = new double[2][];
            var SWPER = new double[2][];
            var WVHGT = new double[2][];
            var WVDIR = new double[2][];
            var WVPER = new double[2][];
            var UGRD = new double[2][];
            var VGRD = new double[2][];

            GribEnvironment.Init();
            List<GribMessage> Nww3 = new List<GribMessage>();
            _log.Info($"Start Insert : {_decodeList[6]}");
            using (GribFile gribFile = new GribFile(_decodeList[6]))
            {
                gribFile.Context.EnableMultipleFieldMessages = true;
                foreach (GribMessage item in gribFile)
                {
                    if (item.StepRange == "6")
                    {
                        break;
                    }
                    Nww3.Add(item);
                }
            }

            List<NWW3> wavedataout = new List<NWW3> { };
            List<NWW3_DB_INSERT_CHECK> nww3DbInsertCheck = new List<NWW3_DB_INSERT_CHECK>();

            int num = 0;
            for (int i = 0; i < elementTotalCount; i += elementCount)
            {
                UTC[num] = Nww3.ElementAt(0 + i).Time;
                LAT[num] = Nww3.ElementAt(0 + i).GridCoordinateValues.Select(d => d.Latitude).ToArray();
                LON[num] = Nww3.ElementAt(0 + i).GridCoordinateValues.Select(d => d.Longitude).ToArray();
                Nww3.ElementAt(5 + i).Values(out HTSGW[num]);
                Nww3.ElementAt(8 + i).Values(out MWSDIR[num]);
                Nww3.ElementAt(6 + i).Values(out MWSPER[num]);
                Nww3.ElementAt(11 + i).Values(out SWELL[num]);
                Nww3.ElementAt(17 + i).Values(out SWDIR[num]);
                Nww3.ElementAt(14 + i).Values(out SWPER[num]);
                Nww3.ElementAt(10 + i).Values(out WVHGT[num]);
                Nww3.ElementAt(16 + i).Values(out WVDIR[num]);
                Nww3.ElementAt(13 + i).Values(out WVPER[num]);
                Nww3.ElementAt(2 + i).Values(out UGRD[num]);
                Nww3.ElementAt(3 + i).Values(out VGRD[num]);
                num++;
            }

            for (int i = 0; i < num; i++)
            {
                List<NWW3> nww3Json = new List<NWW3>();
                for (int j = 0; j < LAT[i].Length; j++)
                {
                    wavedataout.Add(new NWW3
                    {
                        UTC = UTC[i],
                        LAT = Convert.ToDecimal(LAT[i][j]),
                        LON = Convert.ToDecimal(LON[i][j]),
                        HTSGW = Convert.ToSingle(HTSGW[i][j]),
                        MWSDIR = Convert.ToSingle(MWSDIR[i][j]),
                        MWSPER = Convert.ToSingle(MWSPER[i][j]),
                        SWELL = Convert.ToSingle(SWELL[i][j]),
                        SWDIR = Convert.ToSingle(SWDIR[i][j]),
                        SWPER = Convert.ToSingle(SWPER[i][j]),
                        WVHGT = Convert.ToSingle(WVHGT[i][j]),
                        WVDIR = Convert.ToSingle(WVDIR[i][j]),
                        WVPER = Convert.ToSingle(WVPER[i][j]),
                        UGRD = Convert.ToSingle(UGRD[i][j]),
                        VGRD = Convert.ToSingle(VGRD[i][j]),
                    });

                    nww3Json.Add(new NWW3
                    {
                        UTC = UTC[i],
                        LAT = Convert.ToDecimal(LAT[i][j]),
                        LON = Convert.ToDecimal(LON[i][j]),
                        HTSGW = Convert.ToSingle(HTSGW[i][j]),
                        MWSDIR = Convert.ToSingle(MWSDIR[i][j]),
                        MWSPER = Convert.ToSingle(MWSPER[i][j]),
                        SWELL = Convert.ToSingle(SWELL[i][j]),
                        SWDIR = Convert.ToSingle(SWDIR[i][j]),
                        SWPER = Convert.ToSingle(SWPER[i][j]),
                        WVHGT = Convert.ToSingle(WVHGT[i][j]),
                        WVDIR = Convert.ToSingle(WVDIR[i][j]),
                        WVPER = Convert.ToSingle(WVPER[i][j]),
                        UGRD = Convert.ToSingle(UGRD[i][j]),
                        VGRD = Convert.ToSingle(VGRD[i][j]),
                    });
                }
                Nww3MakeJson(nww3Json);
            }

            int resumeCount = 0;

            int insertDataCount = 231120;
            var checkDate = UTC[0];
            var checkDate3 = UTC[1];

            _oceanModel.Database.CommandTimeout = 99999;
            var checkDB = _oceanModel.NWW3.FirstOrDefault(s => s.UTC == checkDate);

            if (checkDB != null)
            {
                var resumeCount1 = _oceanModel.NWW3.Count(s => s.UTC == checkDate);
                var resumeCount2 = _oceanModel.NWW3.Count(s => s.UTC == checkDate3);
                resumeCount = resumeCount1 + resumeCount2;

                if (resumeCount == insertDataCount * 2)
                {
                    nww3DbInsertCheck.Add(new NWW3_DB_INSERT_CHECK
                    {
                        FILE_NAME = _decodeList[4].ToLower(),
                        DATE_OF_FILE = UTC[0],
                        DATE_INSERTED = DateTime.UtcNow
                    });

                    nww3DbInsertCheck.ForEach(s => _oceanModel.NWW3_DB_INSERT_CHECK.Add(s));
                    _oceanModel.SaveChanges();
                    return;
                }
            }

            var waveSplit = Split(wavedataout, 10000);

            int index = 0;
            foreach (var item in waveSplit)
            {
                if (index * 10000 >= resumeCount)
                {
                    EFBatchOperation.For(_oceanModel, _oceanModel.NWW3).InsertAll(item);
                    Thread.Sleep(500);
                }

                var progress = Math.Round((double)index / 0.46, 2);
                _log.Info($"Wave Nowcast Db insert :{_decodeList[0]}-{_decodeList[1]}-{_decodeList[2]} {_decodeList[3]}:00 / {progress} %");
                index++;
            }

            waveSplit = null;
            wavedataout = null;

            nww3DbInsertCheck.Add(new NWW3_DB_INSERT_CHECK
            {
                FILE_NAME = _decodeList[4].ToLower(),
                DATE_OF_FILE = UTC[0],
                DATE_INSERTED = DateTime.UtcNow
            });

            nww3DbInsertCheck.ForEach(s => _oceanModel.NWW3_DB_INSERT_CHECK.Add(s));
            _oceanModel.SaveChanges();

            _log.Info($"Wave Db insert Finish");
        }

        public static void Nww3ForcastInsertBasic()
        {
            _log.Info($"forcast Strars");

            int elementCount = 19; // 한 셋트 데이터 종류 (변경하지 말것)
            int needDecordDateSetCount = 81; // 3시간 데이터 2개를 저장.
            int elementTotalCount = elementCount * needDecordDateSetCount;
            int dataSetGap = 1; //1는 3시간, 2는 6시간마다 forecast 데이터를 밀어넣음.

            _oceanModel.Database.ExecuteSqlCommand("TRUNCATE TABLE [" + "NWW3_FORECAST" + "]");
            _oceanModel.SaveChanges();

            var UTC = new DateTime();
            var LAT = new double[231120];
            var LON = new double[231120];
            var HTSGW = new double[231120];
            var MWSDIR = new double[231120];
            var MWSPER = new double[231120];
            var SWELL = new double[231120];
            var SWDIR = new double[231120];
            var SWPER = new double[231120];
            var WVHGT = new double[231120];
            var WVDIR = new double[231120];
            var WVPER = new double[231120];
            var UGRD = new double[231120];
            var VGRD = new double[231120];

            GribEnvironment.Init();
            List<GribMessage> Nww3 = new List<GribMessage>();

            using (GribFile gribFile = new GribFile(_decodeList[6]))
            {
                gribFile.Context.EnableMultipleFieldMessages = true;
                elementTotalCount = elementCount * 81;
                foreach (GribMessage item in gribFile)
                {
                    Nww3.Add(item);
                }
            }
            List<NWW3_FORECAST> waveForecastDataout = new List<NWW3_FORECAST> { };

            int index = 0;
            for (int i = 0; i < elementTotalCount; i += elementCount * dataSetGap)
            {
                UTC = Nww3.ElementAt(0 + i).Time;
                LAT = Nww3.ElementAt(0 + i).GridCoordinateValues.Select(d => d.Latitude).ToArray();
                LON = Nww3.ElementAt(0 + i).GridCoordinateValues.Select(d => d.Longitude).ToArray();

                Nww3.ElementAt(5 + i).Values(out HTSGW);
                Nww3.ElementAt(8 + i).Values(out MWSDIR);
                Nww3.ElementAt(6 + i).Values(out MWSPER);
                Nww3.ElementAt(11 + i).Values(out SWELL);
                Nww3.ElementAt(17 + i).Values(out SWDIR);
                Nww3.ElementAt(14 + i).Values(out SWPER);
                Nww3.ElementAt(10 + i).Values(out WVHGT);
                Nww3.ElementAt(16 + i).Values(out WVDIR);
                Nww3.ElementAt(13 + i).Values(out WVPER);
                Nww3.ElementAt(2 + i).Values(out UGRD);
                Nww3.ElementAt(3 + i).Values(out VGRD);

                var nww3Json = new List<NWW3>();
                for (int j = 0; j < LAT.Length; j++)
                {
                    waveForecastDataout.Add(new NWW3_FORECAST
                    {
                        UTC = UTC,
                        LAT = Convert.ToDecimal(LAT[j]),
                        LON = Convert.ToDecimal(LON[j]),
                        HTSGW = Convert.ToSingle(HTSGW[j]),
                        MWSDIR = Convert.ToSingle(MWSDIR[j]),
                        MWSPER = Convert.ToSingle(MWSPER[j]),
                        SWELL = Convert.ToSingle(SWELL[j]),
                        SWDIR = Convert.ToSingle(SWDIR[j]),
                        SWPER = Convert.ToSingle(SWPER[j]),
                        WVHGT = Convert.ToSingle(WVHGT[j]),
                        WVDIR = Convert.ToSingle(WVDIR[j]),
                        WVPER = Convert.ToSingle(WVPER[j]),
                        UGRD = Convert.ToSingle(UGRD[j]),
                        VGRD = Convert.ToSingle(VGRD[j])
                    });

                    nww3Json.Add(new NWW3
                    {
                        UTC = UTC,
                        LAT = Convert.ToDecimal(LAT[j]),
                        LON = Convert.ToDecimal(LON[j]),
                        HTSGW = Convert.ToSingle(HTSGW[j]),
                        MWSDIR = Convert.ToSingle(MWSDIR[j]),
                        MWSPER = Convert.ToSingle(MWSPER[j]),
                        SWELL = Convert.ToSingle(SWELL[j]),
                        SWDIR = Convert.ToSingle(SWDIR[j]),
                        SWPER = Convert.ToSingle(SWPER[j]),
                        WVHGT = Convert.ToSingle(WVHGT[j]),
                        WVDIR = Convert.ToSingle(WVDIR[j]),
                        WVPER = Convert.ToSingle(WVPER[j]),
                        UGRD = Convert.ToSingle(UGRD[j]),
                        VGRD = Convert.ToSingle(VGRD[j])
                    });
                }
                //nowcast json은 이미 만들어서 건너뜀.
                if (i > 0)
                {
                    Nww3MakeJson(nww3Json);
                }

                var waveForcastSplit = Split(waveForecastDataout, 10000);

                var length = waveForcastSplit.Count;

                foreach (var item in waveForcastSplit)
                {
                    EFBatchOperation.For(_oceanModel, _oceanModel.NWW3_FORECAST).InsertAll(item);
                    Thread.Sleep(500);
                    var progress = Math.Round((double)index / 0.23 / (81 / dataSetGap), 2);
                    _log.Info($"Wave Forcast Db insert :{item[0].UTC.ToString("yyyy-MM-dd HH:mm:00")} / {progress} %");
                    index++;
                }
                waveForcastSplit.Clear();
                waveForecastDataout.Clear();
            }
            var conditionWeatherDb = _logModel.CONDITION_OCEAN_WEATHER.First();
            conditionWeatherDb.DATE_LAST_OF_NWW3 = UTC;
            conditionWeatherDb.DATE_LAST_OF_NWW3_JSON = UTC;
            conditionWeatherDb.DB_OF_NWW3_FORECAST = "BASIC";
            _logModel.SaveChanges();
        }

        public static void Nww3ForcastInsertProxy()
        {
            _log.Info($"forcast Strars");

            int elementCount = 19; // 한 셋트 데이터 종류 (변경하지 말것)
            int needDecordDateSetCount = 81; // 3시간 데이터 2개를 저장.
            int elementTotalCount = elementCount * needDecordDateSetCount;
            int dataSetGap = 1; //1는 3시간, 2는 6시간마다 forecast 데이터를 밀어넣음.

            _oceanModel.Database.ExecuteSqlCommand("TRUNCATE TABLE [" + "NWW3_FORECAST_PROXY" + "]");
            _oceanModel.SaveChanges();

            var UTC = new DateTime();
            var LAT = new double[231120];
            var LON = new double[231120];
            var HTSGW = new double[231120];
            var MWSDIR = new double[231120];
            var MWSPER = new double[231120];
            var SWELL = new double[231120];
            var SWDIR = new double[231120];
            var SWPER = new double[231120];
            var WVHGT = new double[231120];
            var WVDIR = new double[231120];
            var WVPER = new double[231120];
            var UGRD = new double[231120];
            var VGRD = new double[231120];

            GribEnvironment.Init();
            List<GribMessage> Nww3 = new List<GribMessage>();

            using (GribFile gribFile = new GribFile(_decodeList[6]))
            {
                gribFile.Context.EnableMultipleFieldMessages = true;
                elementTotalCount = elementCount * 81;
                foreach (GribMessage item in gribFile)
                {
                    Nww3.Add(item);
                }
            }
            List<NWW3_FORECAST_PROXY> waveForecastDataout = new List<NWW3_FORECAST_PROXY> { };

            int index = 0;
            for (int i = 0; i < elementTotalCount; i += elementCount * dataSetGap)
            {
                UTC = Nww3.ElementAt(0 + i).Time;
                LAT = Nww3.ElementAt(0 + i).GridCoordinateValues.Select(d => d.Latitude).ToArray();
                LON = Nww3.ElementAt(0 + i).GridCoordinateValues.Select(d => d.Longitude).ToArray();

                Nww3.ElementAt(5 + i).Values(out HTSGW);
                Nww3.ElementAt(8 + i).Values(out MWSDIR);
                Nww3.ElementAt(6 + i).Values(out MWSPER);
                Nww3.ElementAt(11 + i).Values(out SWELL);
                Nww3.ElementAt(17 + i).Values(out SWDIR);
                Nww3.ElementAt(14 + i).Values(out SWPER);
                Nww3.ElementAt(10 + i).Values(out WVHGT);
                Nww3.ElementAt(16 + i).Values(out WVDIR);
                Nww3.ElementAt(13 + i).Values(out WVPER);
                Nww3.ElementAt(2 + i).Values(out UGRD);
                Nww3.ElementAt(3 + i).Values(out VGRD);

                var nww3Json = new List<NWW3>();
                for (int j = 0; j < LAT.Length; j++)
                {
                    waveForecastDataout.Add(new NWW3_FORECAST_PROXY
                    {
                        UTC = UTC,
                        LAT = Convert.ToDecimal(LAT[j]),
                        LON = Convert.ToDecimal(LON[j]),
                        HTSGW = Convert.ToSingle(HTSGW[j]),
                        MWSDIR = Convert.ToSingle(MWSDIR[j]),
                        MWSPER = Convert.ToSingle(MWSPER[j]),
                        SWELL = Convert.ToSingle(SWELL[j]),
                        SWDIR = Convert.ToSingle(SWDIR[j]),
                        SWPER = Convert.ToSingle(SWPER[j]),
                        WVHGT = Convert.ToSingle(WVHGT[j]),
                        WVDIR = Convert.ToSingle(WVDIR[j]),
                        WVPER = Convert.ToSingle(WVPER[j]),
                        UGRD = Convert.ToSingle(UGRD[j]),
                        VGRD = Convert.ToSingle(VGRD[j])
                    });

                    nww3Json.Add(new NWW3
                    {
                        UTC = UTC,
                        LAT = Convert.ToDecimal(LAT[j]),
                        LON = Convert.ToDecimal(LON[j]),
                        HTSGW = Convert.ToSingle(HTSGW[j]),
                        MWSDIR = Convert.ToSingle(MWSDIR[j]),
                        MWSPER = Convert.ToSingle(MWSPER[j]),
                        SWELL = Convert.ToSingle(SWELL[j]),
                        SWDIR = Convert.ToSingle(SWDIR[j]),
                        SWPER = Convert.ToSingle(SWPER[j]),
                        WVHGT = Convert.ToSingle(WVHGT[j]),
                        WVDIR = Convert.ToSingle(WVDIR[j]),
                        WVPER = Convert.ToSingle(WVPER[j]),
                        UGRD = Convert.ToSingle(UGRD[j]),
                        VGRD = Convert.ToSingle(VGRD[j])
                    });
                }
                //nowcast json은 이미 만들어서 건너뜀.
                if (i > 0)
                {
                    Nww3MakeJson(nww3Json);
                }

                var waveForcastSplit = Split(waveForecastDataout, 10000);

                var length = waveForcastSplit.Count;

                foreach (var item in waveForcastSplit)
                {
                    EFBatchOperation.For(_oceanModel, _oceanModel.NWW3_FORECAST_PROXY).InsertAll(item);
                    Thread.Sleep(500);
                    var progress = Math.Round((double)index / 0.23 / (81 / dataSetGap), 2);
                    _log.Info($"Wave Forcast Db insert :{item[0].UTC.ToString("yyyy-MM-dd HH:mm:00")} / {progress} %");
                    index++;
                }
                waveForcastSplit.Clear();
                waveForecastDataout.Clear();
            }
            var conditionWeatherDb = _logModel.CONDITION_OCEAN_WEATHER.First();
            conditionWeatherDb.DATE_LAST_OF_NWW3 = UTC;
            conditionWeatherDb.DATE_LAST_OF_NWW3_JSON = UTC;
            conditionWeatherDb.DB_OF_NWW3_FORECAST = "PROXY";
            _logModel.SaveChanges();
        }

        public static void Nww3MakeJson(List<NWW3> nww3Json)
        {
            CloudBlobContainer container = _blobClient.GetContainerReference("weatherjson");

            var utc = nww3Json[0].UTC;

            var utcString = utc.ToString("yyyy,MM,dd,HH");
            var year = utcString.Split(',')[0];
            var month = utcString.Split(',')[1];
            var day = utcString.Split(',')[2];
            var hour = utcString.Split(',')[3];

            var ugrd = nww3Json.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.UGRD).ToList();
            var vgrd = nww3Json.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.VGRD).ToList();
            var htsgw = nww3Json.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.HTSGW).ToList();
            var mwsdir = nww3Json.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.MWSDIR).ToList();
            var mwsper = nww3Json.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.MWSPER).ToList();

            var ugrdJson = new JArray();
            var vgrdJson = new JArray();
            var mwsdirJson = new JArray();
            var mwsperJson = new JArray();

            foreach (var item in ugrd)
            {
                if (item >= 9999)
                {
                    ugrdJson.Add(0);
                }
                else
                {
                    ugrdJson.Add(item.ToString("0.#"));
                }
            }

            foreach (var item in vgrd)
            {
                if (item >= 9999)
                {
                    vgrdJson.Add(0);
                }
                else
                {
                    vgrdJson.Add(item.ToString("0.#"));
                }
            }

            foreach (var item in mwsdir)
            {
                if (item >= 9999)
                {
                    mwsdirJson.Add(0);
                }
                else
                {
                    mwsdirJson.Add(item.ToString("0.#"));
                }
            }

            foreach (var item in mwsper)
            {
                if (item >= 9999)
                {
                    mwsperJson.Add(0);
                }
                else
                {
                    mwsperJson.Add(item.ToString("0.#"));
                }
            }

            var refdateTimeConvert = nww3Json[0].UTC.AddHours(3).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
            var metadateTimeConvert = nww3Json[0].UTC.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

            JObject dimensions = new JObject
                {
                    {"lat", new JObject {{ "@dimension", "lat" }, { "length", 321 }}},
                    {"lon", new JObject {{ "@dimension", "lon" }, { "length", 720 }}},
                    {"time", new JObject {{ "@dimension", "time" }, { "length", 1 }}},
                    {"featureType", "GRID"},
                    {"file_format", "GGRIB-2"},
                    {"history", "Read using CDM IOSP GribCollection v3"}
                };

            JObject variables = new JObject
                {
                    {"LatLon_Projection", new JObject {{ "@variable", "LatLon_Projection" }, { "earth_radius", 6371229 }, { "grid_mapping_name", "latitude_longitude" }, { "type", "int" }}},

                    { "Primary_wave_direction_surface", new JObject {{ "@variable", "Primary_wave_direction_surface" }, { "Grib2_Generating_Process_Type", "Forecast" }, { "Grib2_Level_Type", "Ground or water surface" },{ "Grib2_Parameter", new JArray() { 10, 0, 10 } },{ "Grib2_Parameter_Category", "Waves" },{ "Grib2_Parameter_Discipline", "Oceanographic products" },{ "Grib2_Parameter_Name", "Primary wave direction" },{ "Grib_Variable_Id", "VAR_10-0-10_L1" },{ "abbreviation", "DIRPW" } ,{ "coordinates", "reftime time lat lon " },{ "data", new JObject { {"block",0 }, {"method","ppak"  }} },{ "dimensions", new JArray { "time", "lat", "lon" } }, { "grid_mapping", "LatLon_Projection" },{ "long_name", "Primary wave direction @ Ground or water surface" },  { "missing_value", new JValue((object)null) } ,{ "type", "float" }, { "units", "degree_true" } } },

                     { "Primary_wave_mean_period_surface", new JObject {{ "@variable", "Primary_wave_mean_period_surface" }, { "Grib2_Generating_Process_Type", "Forecast" }, { "Grib2_Level_Type", "Ground or water surface" },{ "Grib2_Parameter", new JArray() { 10, 0, 11 } },{ "Grib2_Parameter_Category", "Waves" },{ "Grib2_Parameter_Discipline", "Oceanographic products" },{ "Grib2_Parameter_Name", "Primary wave mean period" },{ "Grib_Variable_Id", "VAR_10-0-11_L1" },{ "abbreviation", "PERPW" } ,{ "coordinates", "reftime time lat lon " },{ "data", new JObject { {"block",1 }, {"method","ppak"  }} },{ "dimensions", new JArray { "time", "lat", "lon" } }, { "grid_mapping", "LatLon_Projection" },{ "long_name", "Primary wave mean period @ Ground or water surface" },  { "missing_value", new JValue((object)null) } ,{ "type", "float" }, { "units", "s" } } },

                    { "lat", new JObject {{ "@variable", "lat" }, { "dimensions", new JArray() { "lat" } }, { "sequence", new JObject { { "delta", -0.5 }, { "size", 321 }, { "start", 80 } } }, { "type", "float" },{ "units", "degrees_north" } } },

                    { "lon", new JObject {{ "@variable", "lon" }, { "dimensions", new JArray() { "lon" } }, { "sequence", new JObject { { "delta", 0.5 }, { "size", 720 }, { "start", 0 } } }, { "type", "float" },{ "units", "degrees_east" } } },

                    { "time", new JObject {{ "@variable", "time" }, { "calendar", "proleptic_gregorian" }, { "data", new JArray  { refdateTimeConvert } }, { "dimensions", new JArray { "time" } }, { "init-time",  metadateTimeConvert }, { "long_name", "GRIB forecast or observation time" }, { "standard_name", "time" }, { "type", "double" }, { "units", "Hour since "+ metadateTimeConvert } } }
                };

            var hearder_detail = new JObject();
            hearder_detail.Add("@group", ""); ;
            hearder_detail.Add("Analysis_or_forecast_generating_process_identifier_defined_by_originating_centre", "Global Multi-Grid Wave Model (Static Grids)");
            hearder_detail.Add("Conventions", "CF-1.6");
            hearder_detail.Add("GRIB_table_version", "2,1");
            hearder_detail.Add("Originating_or_generating_Center", "US National Weather Service, National Centres for Environmental Prediction (NCEP)");
            hearder_detail.Add("Originating_or_generating_Subcenter", "0");
            hearder_detail.Add("Type_of_generating_process", "Forecast");
            hearder_detail.Add("converter", "netcdf2json");
            hearder_detail.Add("dimensions", dimensions);
            hearder_detail.Add("variables", variables);

            var wave = new JObject();
            wave.Add("header", hearder_detail);

            var block = new JArray() { };
            block.Add(mwsdirJson);
            block.Add(mwsperJson);

            wave.Add("blocks", block);

            var waveJson = JsonConvert.SerializeObject(wave, Formatting.None);

            var blobFullPath = Path.Combine(year, month, day, "wave-" + hour + "00.json");
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobFullPath);
            blockBlob.UploadText(waveJson);

            hearder_detail = new JObject();
            hearder_detail.Add("refTime", refdateTimeConvert); ;
            hearder_detail.Add("nx", 720);
            hearder_detail.Add("ny", 321);
            hearder_detail.Add("lo1", 0);
            hearder_detail.Add("la1", 80);
            hearder_detail.Add("lo2", 359.5);
            hearder_detail.Add("la2", -80);
            hearder_detail.Add("dx", 0.5);
            hearder_detail.Add("dy", 0.5);

            var meta1 = new JObject();
            var meta1_detail = new JObject();
            meta1_detail.Add("date", metadateTimeConvert);

            JArray waveOut = new JArray();

            waveOut.Add(new JObject
                {
                    {"header", hearder_detail },
                    { "data", ugrdJson},
                    {"meta", meta1_detail }
                });

            waveOut.Add(new JObject
                {
                     {"header", hearder_detail },
                    { "data", vgrdJson},
                    {"meta", meta1_detail }
                });
            var windJson = JsonConvert.SerializeObject(waveOut, Formatting.None);

            blobFullPath = Path.Combine(year, month, day, "wind-" + hour + "00.json");
            blockBlob = container.GetBlockBlobReference(blobFullPath);
            blockBlob.UploadText(windJson);

            //var conditionWeatherDb = _logModel.CONDITION_OCEAN_WEATHER.First();
            //conditionWeatherDb.DATE_LAST_OF_NWW3_JSON = utc;
            //conditionWeatherDb.DATE_LAST_OF_NWW3 = utc;
            //_logModel.SaveChanges();
        }

        private static List<List<T>> Split<T>(List<T> collection, int size)
        {
            var chunks = new List<List<T>>();
            var chunkCount = collection.Count / size;

            if (collection.Count % size > 0)
                chunkCount++;

            for (var i = 0; i < chunkCount; i++)
                chunks.Add(collection.Skip(i * size).Take(size).ToList());

            return chunks;
        }
    }
}