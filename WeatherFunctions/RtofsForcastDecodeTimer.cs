using CodeFirstModels.Models;
using CodeFirstModels.Models.LogModel;
using CodeFirstModels.Models.OceanModel;
using EntityFramework.Utilities;
using FluentFTP;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Research.Science.Data;
using Microsoft.Research.Science.Data.Factory;
using Microsoft.Research.Science.Data.NetCDF4;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace WeatherFunctions
{
    public static class RtofsForcastDecodeTimer
    {
        private static OCEAN_MODEL _oceanModel = new OCEAN_MODEL();
        private static LOG_MODEL _logModel = new LOG_MODEL();

        private static string _storageAccountName = CloudConn.StorageAccountName_OceanRaw;
        private static string _storageAccountKey = CloudConn.StorageAccountKey_OceanRaw;
        private static string _storageConnectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", _storageAccountName, _storageAccountKey);
        private static CloudStorageAccount _storageAccount = CloudStorageAccount.Parse(_storageConnectionString);
        private static CloudBlobClient _blobClient = _storageAccount.CreateCloudBlobClient();
        private static List<string[]> _decodeList;

        private static string _ftpAccountId = CloudConn.FtpAccountId_Nww3;
        private static string _ftpAccountPw = CloudConn.FtpAccountPw_Nww3;

        private static bool _functionIsRunningOrNot = false;
        private static int _dayToCheckForDownload = 4;
        private static bool _isSuccessDownloadDiag = false;
        private static bool _isSuccessDownloadProg = false;
        private static DateTime _dateNextRtofsForecastFileToGet;

        private static List<string[]> _fileListToNeedDownload;
        private static List<RTOFS_POSITION_CONVERT> rtofsPostion;

        private static TraceWriter _log;

        [FunctionName("RtofsForcastDecodeTimer")]
        public static void Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

            _log = log;
            _fileListToNeedDownload = new List<string[]>();
            // function 다른 instance가 동작하고 있으면 이중으로 실행하지 않도록 작동 중지.
            if (_functionIsRunningOrNot == true)
            {
                _log.Info($"Other Instance is Running at: {DateTime.Now}");
                return;
            }

            if (rtofsPostion == null)
            {
                rtofsPostion = _oceanModel.RTOFS_POSITION_CONVERT.ToList();
            }

            RemoveDirectoryAndFile();

            try
            {
                _functionIsRunningOrNot = true;

                if (_logModel.CONDITION_OCEAN_WEATHER.Single().FILE_NAME_LAST_OF_RTOFS_FORECAST == null || _logModel.CONDITION_OCEAN_WEATHER.Single().DATE_LAST_FILE_OF_RTOFS_FORECAST == new DateTime(1900, 01, 01, 0, 0, 0))
                {
                    var conditionWeatherDb = _logModel.CONDITION_OCEAN_WEATHER.First();
                    conditionWeatherDb.FILE_NAME_LAST_OF_RTOFS_FORECAST = "rtofs_glo_2ds_f024_3hrly_prog.nc";
                    conditionWeatherDb.DATE_LAST_FILE_OF_RTOFS_FORECAST = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.AddDays(-1).Day, 0, 0, 0);
                    _logModel.SaveChanges();
                }

                var fileNextRtofsForecastFileToGet = _logModel.CONDITION_OCEAN_WEATHER.Single().FILE_NAME_LAST_OF_RTOFS_FORECAST;
                var tempHour = fileNextRtofsForecastFileToGet.Split('_')[3].Substring(1);
                var hour = Convert.ToInt32(tempHour);
                _dateNextRtofsForecastFileToGet = _logModel.CONDITION_OCEAN_WEATHER.Single().DATE_LAST_FILE_OF_RTOFS_FORECAST;

                var whichDb = _logModel.CONDITION_OCEAN_WEATHER.First();

                if (hour == 192)
                {
                    hour = 24;
                    _dateNextRtofsForecastFileToGet = _dateNextRtofsForecastFileToGet.AddDays(1);

                    if (whichDb.DB_OF_RTOFS_FORECAST.ToUpper() == "PROXY")
                    {
                        _oceanModel.Database.ExecuteSqlCommand("TRUNCATE TABLE [" + "RTOFS_FORECAST" + "]");
                        _oceanModel.SaveChanges();
                    }
                    else if (whichDb.DB_OF_RTOFS_FORECAST.ToUpper() == "BASIC")
                    {
                        _oceanModel.Database.ExecuteSqlCommand("TRUNCATE TABLE [" + "RTOFS_FORECAST_PROXY" + "]");
                        _oceanModel.SaveChanges();
                    }
                }
                else
                {
                    hour = hour + 3;
                }

                var dateRtofs = _dateNextRtofsForecastFileToGet.ToString("yyyyMMdd");

                tempHour = hour.ToString("000");

                var fileNameRtofsDiag = "rtofs_glo_2ds_f" + tempHour + "_3hrly_diag.nc";
                var fileNameRtofsProg = "rtofs_glo_2ds_f" + tempHour + "_3hrly_prog.nc";

                var remoteFilePathDiag = "/pub/data/nccf/com/rtofs/prod/rtofs." + dateRtofs + "/" + fileNameRtofsDiag;
                var remoteFilePathProg = "/pub/data/nccf/com/rtofs/prod/rtofs." + dateRtofs + "/" + fileNameRtofsProg;

                var localPathDiag = FtpDownLoad(remoteFilePathDiag, fileNameRtofsDiag, _dateNextRtofsForecastFileToGet);
                if (localPathDiag == "false")
                {
                    return;
                }
                _isSuccessDownloadDiag = true;
                var localPathProg = FtpDownLoad(remoteFilePathProg, fileNameRtofsProg, _dateNextRtofsForecastFileToGet);
                if (localPathProg == "false")
                {
                    return;
                }
                _isSuccessDownloadProg = true;

                if (_isSuccessDownloadDiag && _isSuccessDownloadProg)
                {
                    var year = _dateNextRtofsForecastFileToGet.AddHours(hour).Year.ToString();
                    var month = _dateNextRtofsForecastFileToGet.AddHours(hour).Month.ToString();
                    var day = _dateNextRtofsForecastFileToGet.AddHours(hour).Day.ToString();
                    tempHour = _dateNextRtofsForecastFileToGet.AddHours(hour).Hour.ToString();

                    _decodeList = new List<string[]>();
                    _decodeList.Add(new string[] { year, month, day, tempHour, fileNameRtofsDiag, remoteFilePathDiag, localPathDiag });
                    _decodeList.Add(new string[] { year, month, day, tempHour, fileNameRtofsProg, remoteFilePathProg, localPathProg });

                    if (whichDb.DB_OF_RTOFS_FORECAST.ToUpper() == "PROXY")
                    {
                        RtofsForeCastInsertBasic();
                    }
                    else if (whichDb.DB_OF_RTOFS_FORECAST.ToUpper() == "BASIC")
                    {
                        RtofsForeCastInsertProxy();
                    }

                    RemoveDirectoryAndFile();
                }
            }
            catch (Exception e)
            {
                log.Info($"error: {e.ToString()}");
            }
            finally
            {
                _functionIsRunningOrNot = false;
                _isSuccessDownloadDiag = false;
                _isSuccessDownloadProg = false;
            }
        }

        public static void RemoveDirectoryAndFile()
        {
            var tempPath = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\rtofsForecast\");
            System.IO.DirectoryInfo di = new DirectoryInfo(tempPath);

            if (di.Exists)
            {
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
        }

        private static string FtpDownLoad(string filePath, string fileNameRtofs, DateTime dateNextRtofsForecastFileToGet)
        {
            string localFilePath = "";
            // Ftp Check and Download
            using (FtpClient conn = new FtpClient())
            {
                //ms
                conn.SocketPollInterval = 1000;
                conn.ConnectTimeout = 30000;
                conn.ReadTimeout = 300000;
                conn.DataConnectionConnectTimeout = 300000;
                conn.DataConnectionReadTimeout = 300000;

                conn.Host = "ftp.ncep.noaa.gov";
                conn.Credentials = new NetworkCredential(_ftpAccountId, _ftpAccountPw);
                conn.Connect();

                var checkPoint = 0;

                if (conn.FileExists(filePath))
                {
                    var ftpNww3DownloadCheck = new FTP_RAWDATA_DOWNLOAD_CHECK();
                    var oceanJobList = new JOB_LIST_OCEAN_WEATHER();

                    Progress<double> progress = new Progress<double>(x =>
                    {
                        if (x < 0)
                        {
                        }
                        if (x > checkPoint)
                        {
                            checkPoint = checkPoint + 10;

                            _log.Info($"{dateNextRtofsForecastFileToGet.ToString("yyyy-MM-dd mm-HH")} / {filePath}   Progres : {Math.Round(x, 0)} % ");
                        }
                    });

                    localFilePath = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\rtofsForecast\");
                    localFilePath = Path.Combine(localFilePath, dateNextRtofsForecastFileToGet.Year.ToString("0000"), dateNextRtofsForecastFileToGet.Month.ToString("00"), dateNextRtofsForecastFileToGet.Day.ToString("00"), fileNameRtofs);

                    Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));
                    //localFilePath = Path.Combine(localFilePath, fileNameRtofs);
                    conn.DownloadFile(@localFilePath, filePath, true, FtpVerify.Retry, progress);
                    _log.Info($"{dateNextRtofsForecastFileToGet.ToString("yyyy-MM-dd mm-HH")} / {filePath}  Progres : Complete");
                    _log.Info($"===============================================================================================================");
                    conn.Disconnect();
                    return localFilePath;
                }
                else
                {
                    return "false";
                }
            }
        }

        public static void RtofsForeCastInsertBasic()
        {
            DataSetFactory.Register(typeof(NetCDFDataSet));

            using (DataSet diag = DataSet.Open(_decodeList[0][6]))
            {
                using (DataSet prog = DataSet.Open(_decodeList[1][6]))
                {
                    var UTCtemp = prog.Variables.ElementAt(10).GetData().GetValue(0);
                    var SSStemp = prog.Variables.ElementAt(1).GetData();
                    var SSTtemp = prog.Variables.ElementAt(2).GetData();
                    var CURRENT_UVtemp = prog.Variables.ElementAt(4).GetData();
                    var CURRENT_VVtemp = prog.Variables.ElementAt(3).GetData();
                    var ICETHICKNESStemp = diag.Variables.ElementAt(5).GetData();

                    var lonLen = 4500;
                    var latLen = 3298;

                    var utcTime = new DateTime(Convert.ToInt16(_decodeList[0][0]), Convert.ToInt16(_decodeList[0][1]), Convert.ToInt16(_decodeList[0][2]), Convert.ToInt16(_decodeList[0][3]), 0, 0);
                    int offset = 0;

                    var KEY = new int[14841000];
                    var SSS = new float[14841000];
                    var SST = new float[14841000];
                    var CURRENT_UV = new float[14841000];
                    var CURRENT_VV = new float[14841000];
                    var ICE_THICKNESS = new float[14841000];

                    for (int i = 0; i < latLen; i++)
                    {
                        for (int j = 0; j < lonLen; j++)
                        {
                            offset = i * lonLen + j;
                            SSS[offset] = (float)SSStemp.GetValue(0, i, j) > 1000 ? 9999 : (float)SSStemp.GetValue(0, i, j);
                            SST[offset] = (float)SSTtemp.GetValue(0, i, j) > 1000 ? 9999 : (float)SSTtemp.GetValue(0, i, j);
                            CURRENT_UV[offset] = (float)CURRENT_UVtemp.GetValue(0, 0, i, j) > 1000 ? 9999 : (float)CURRENT_UVtemp.GetValue(0, 0, i, j);
                            CURRENT_VV[offset] = (float)CURRENT_VVtemp.GetValue(0, 0, i, j) > 1000 ? 9999 : (float)CURRENT_VVtemp.GetValue(0, 0, i, j);
                            ICE_THICKNESS[offset] = (float)ICETHICKNESStemp.GetValue(0, i, j) > 1000 ? 9999 : (float)ICETHICKNESStemp.GetValue(0, i, j);
                        }
                    }

                    var rtofs = new List<RTOFS_FORECAST>();
                    foreach (var item in rtofsPostion)
                    {
                        rtofs.Add(new RTOFS_FORECAST
                        {
                            UTC = utcTime,
                            LAT = item.LAT,
                            LON = item.LON,
                            SSS = SSS[item.KEY],
                            SST = SST[item.KEY],
                            ICE_THICKNESS = ICE_THICKNESS[item.KEY],
                            CURRENT_UV = CURRENT_UV[item.KEY],
                            CURRENT_VV = CURRENT_VV[item.KEY]
                        });
                    }

                    RtofsMakeJson(rtofs);

                    _oceanModel.Database.CommandTimeout = 99999;
                    var rtofsSplit = Split(rtofs, 10000);

                    foreach (var item in rtofsSplit.Select((value, index) => new { value, index }))
                    {
                        EFBatchOperation.For(_oceanModel, _oceanModel.RTOFS_FORECAST).InsertAll(item.value);
                        Thread.Sleep(10);

                        var progress = Math.Round((double)item.index / 0.259, 2);
                        _log.Info($"Rtofs Forecast Db insert :{_decodeList[0][0]}-{_decodeList[0][1]}-{_decodeList[0][2]} {_decodeList[0][3]}:00 / {progress} %");
                    }

                    rtofsSplit = null;
                    rtofs = null;
                    _log.Info($"Rtofs Forecast Db insert Finish");

                    var conditionWeatherDb = _logModel.CONDITION_OCEAN_WEATHER.First();

                    if (conditionWeatherDb.DATE_LAST_OF_RTOFS_JSON < utcTime)
                    {
                        conditionWeatherDb.DATE_LAST_FILE_OF_RTOFS_FORECAST = _dateNextRtofsForecastFileToGet;
                        conditionWeatherDb.FILE_NAME_LAST_OF_RTOFS_FORECAST = _decodeList[0][4];
                        if (_decodeList[1][4] == "rtofs_glo_2ds_f192_3hrly_prog.nc" || _decodeList[1][4] == "rtofs_glo_2ds_f192_3hrly_diag.nc")
                        {
                            conditionWeatherDb.DB_OF_RTOFS_FORECAST = "BASIC";
                            conditionWeatherDb.DATE_LAST_OF_RTOFS_JSON = utcTime;
                            conditionWeatherDb.DATE_LAST_OF_RTOFS = utcTime;
                        }
                    }
                    _logModel.SaveChanges();
                }
            }
        }

        public static void RtofsForeCastInsertProxy()
        {
            DataSetFactory.Register(typeof(NetCDFDataSet));

            using (DataSet diag = DataSet.Open(_decodeList[0][6]))
            {
                using (DataSet prog = DataSet.Open(_decodeList[1][6]))
                {
                    var UTCtemp = prog.Variables.ElementAt(10).GetData().GetValue(0);
                    var SSStemp = prog.Variables.ElementAt(1).GetData();
                    var SSTtemp = prog.Variables.ElementAt(2).GetData();
                    var CURRENT_UVtemp = prog.Variables.ElementAt(4).GetData();
                    var CURRENT_VVtemp = prog.Variables.ElementAt(3).GetData();
                    var ICETHICKNESStemp = diag.Variables.ElementAt(5).GetData();

                    var lonLen = 4500;
                    var latLen = 3298;

                    var utcTime = new DateTime(Convert.ToInt16(_decodeList[0][0]), Convert.ToInt16(_decodeList[0][1]), Convert.ToInt16(_decodeList[0][2]), Convert.ToInt16(_decodeList[0][3]), 0, 0);
                    int offset = 0;

                    var KEY = new int[14841000];
                    var SSS = new float[14841000];
                    var SST = new float[14841000];
                    var CURRENT_UV = new float[14841000];
                    var CURRENT_VV = new float[14841000];
                    var ICE_THICKNESS = new float[14841000];

                    for (int i = 0; i < latLen; i++)
                    {
                        for (int j = 0; j < lonLen; j++)
                        {
                            offset = i * lonLen + j;
                            SSS[offset] = (float)SSStemp.GetValue(0, i, j) > 1000 ? 9999 : (float)SSStemp.GetValue(0, i, j);
                            SST[offset] = (float)SSTtemp.GetValue(0, i, j) > 1000 ? 9999 : (float)SSTtemp.GetValue(0, i, j);
                            CURRENT_UV[offset] = (float)CURRENT_UVtemp.GetValue(0, 0, i, j) > 1000 ? 9999 : (float)CURRENT_UVtemp.GetValue(0, 0, i, j);
                            CURRENT_VV[offset] = (float)CURRENT_VVtemp.GetValue(0, 0, i, j) > 1000 ? 9999 : (float)CURRENT_VVtemp.GetValue(0, 0, i, j);
                            ICE_THICKNESS[offset] = (float)ICETHICKNESStemp.GetValue(0, i, j) > 1000 ? 9999 : (float)ICETHICKNESStemp.GetValue(0, i, j);
                        }
                    }
                    var rtofsproxy = new List<RTOFS_FORECAST>();
                    var rtofs = new List<RTOFS_FORECAST_PROXY>();
                    foreach (var item in rtofsPostion)
                    {
                        rtofs.Add(new RTOFS_FORECAST_PROXY
                        {
                            UTC = utcTime,
                            LAT = item.LAT,
                            LON = item.LON,
                            SSS = SSS[item.KEY],
                            SST = SST[item.KEY],
                            ICE_THICKNESS = ICE_THICKNESS[item.KEY],
                            CURRENT_UV = CURRENT_UV[item.KEY],
                            CURRENT_VV = CURRENT_VV[item.KEY]
                        });

                        rtofsproxy.Add(new RTOFS_FORECAST
                        {
                            UTC = utcTime,
                            LAT = item.LAT,
                            LON = item.LON,
                            SSS = SSS[item.KEY],
                            SST = SST[item.KEY],
                            ICE_THICKNESS = ICE_THICKNESS[item.KEY],
                            CURRENT_UV = CURRENT_UV[item.KEY],
                            CURRENT_VV = CURRENT_VV[item.KEY]
                        });
                    }

                    RtofsMakeJson(rtofsproxy);

                    _oceanModel.Database.CommandTimeout = 99999;

                    var rtofsSplit = Split(rtofs, 10000);
                    foreach (var item in rtofsSplit.Select((value, index) => new { value, index }))
                    {
                        EFBatchOperation.For(_oceanModel, _oceanModel.RTOFS_FORECAST_PROXY).InsertAll(item.value);
                        Thread.Sleep(10);
                        var progress = Math.Round((double)item.index / 0.259, 2);
                        _log.Info($"Rtofs Nowcast Db insert :{_decodeList[0][0]}-{_decodeList[0][1]}-{_decodeList[0][2]} {_decodeList[0][3]}:00 / {progress} %");
                    }

                    rtofsSplit = null;
                    rtofs = null;
                    _log.Info($"Rtofs Forecast Db insert Finish");

                    var conditionWeatherDb = _logModel.CONDITION_OCEAN_WEATHER.First();
                    if (conditionWeatherDb.DATE_LAST_OF_RTOFS_JSON < utcTime)
                    {
                        conditionWeatherDb.DATE_LAST_FILE_OF_RTOFS_FORECAST = _dateNextRtofsForecastFileToGet;
                        conditionWeatherDb.FILE_NAME_LAST_OF_RTOFS_FORECAST = _decodeList[0][4];
                        if (_decodeList[1][4] == "rtofs_glo_2ds_f192_3hrly_prog.nc" || _decodeList[1][4] == "rtofs_glo_2ds_f192_3hrly_diag.nc")
                        {
                            conditionWeatherDb.DB_OF_RTOFS_FORECAST = "BASIC";
                            conditionWeatherDb.DATE_LAST_OF_RTOFS_JSON = utcTime;
                            conditionWeatherDb.DATE_LAST_OF_RTOFS = utcTime;
                        }
                    }
                    _logModel.SaveChanges();
                }
            }
        }

        public static void RtofsMakeJson(List<RTOFS_FORECAST> rtofsJson)
        {
            CloudBlobContainer container = _blobClient.GetContainerReference("weatherjson");

            var utc = rtofsJson[0].UTC;

            var utcString = utc.ToString("yyyy,MM,dd,HH");
            var year = utcString.Split(',')[0];
            var month = utcString.Split(',')[1];
            var day = utcString.Split(',')[2];
            var hour = utcString.Split(',')[3];

            var SSS = rtofsJson.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.SSS).ToList();
            var SST = rtofsJson.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.SST).ToList();
            var Current_UV = rtofsJson.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.CURRENT_UV).ToList();
            var Current_VV = rtofsJson.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.CURRENT_VV).ToList();

            var sssJson = new JArray();
            var sstJson = new JArray();
            var currentuvJson = new JArray();
            var currentvvJson = new JArray();
            var lastItem = 0f;

            foreach (var item in SSS)
            {
                if (item == 9999)
                {
                    sssJson.Add(lastItem.ToString("0.#"));
                }
                else
                {
                    sssJson.Add(item.ToString("0.#"));
                    lastItem = item;
                }
            }
            lastItem = 0f;
            foreach (var item in SST)
            {
                if (item == 9999)
                {
                    sstJson.Add(lastItem.ToString("0.#"));
                }
                else
                {
                    sstJson.Add(item.ToString("0.#"));
                    lastItem = item;
                }
            }

            foreach (var item in Current_UV)
            {
                if (item == 9999)
                {
                    currentuvJson.Add(0);
                }
                else
                {
                    currentuvJson.Add(item.ToString("0.#"));
                }
            }

            foreach (var item in Current_VV)
            {
                if (item == 9999)
                {
                    currentvvJson.Add(0);
                }
                else
                {
                    currentvvJson.Add(item.ToString("0.#"));
                }
            }

            var refdateTimeConvert = rtofsJson[0].UTC.AddHours(3).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
            var metadateTimeConvert = rtofsJson[0].UTC.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

            var fileDate = utc;

            var hearder_sst = new JObject();

            JObject variables_sst = new JObject
                {
                    { "Temperature_surface_sparse", new JObject {{ "data", new JObject { {"block",0 }} },{ "dimensions", new JArray { "time", "lat", "lon" } } } },

                    { "lat", new JObject {{ "dimensions", new JArray() { "lat" } }, { "sequence", new JObject { { "delta", -0.5 }, { "size", 361 }, { "start", 90 } } }, { "type", "float" },{ "units", "degrees_north" } } },

                    { "lon", new JObject {{ "dimensions", new JArray() { "lon" } }, { "sequence", new JObject { { "delta", 0.5 }, { "size", 720 }, { "start", 0 } } }, { "type", "float" },{ "units", "degrees_east" } } },

                    { "time", new JObject {{ "data", new JArray  { refdateTimeConvert } }, { "dimensions", new JArray { "time" } }, { "init-time",  metadateTimeConvert } } }
                };

            var hearder_detail_sst = new JObject();
            hearder_detail_sst.Add("variables", variables_sst);
            var sst = new JObject();

            sst.Add("header", hearder_detail_sst);
            var block_sst = new JArray() { };

            block_sst.Add(sstJson);
            sst.Add("blocks", block_sst);

            var sstJsonout = JsonConvert.SerializeObject(sst, Formatting.None);
            var blobFullPath = Path.Combine(year, month, day, "sst-" + hour + "00.json");
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobFullPath);
            blockBlob.UploadText(sstJsonout);

            hearder_sst = null;
            variables_sst = null;
            hearder_detail_sst = null;
            sst = null;
            block_sst = null;
            sstJsonout = null;

            var hearder_sss = new JObject();

            JObject variables_sss = new JObject
                {
                    { "Temperature_surface_sparse", new JObject {{ "data", new JObject { {"block",0 }} },{ "dimensions", new JArray { "time", "lat", "lon" } } } },

                    { "lat", new JObject {{ "dimensions", new JArray() { "lat" } }, { "sequence", new JObject { { "delta", -0.5 }, { "size", 361 }, { "start", 90 } } }, { "type", "float" },{ "units", "degrees_north" } } },

                    { "lon", new JObject {{ "dimensions", new JArray() { "lon" } }, { "sequence", new JObject { { "delta", 0.5 }, { "size", 720 }, { "start", 0 } } }, { "type", "float" },{ "units", "degrees_east" } } },

                    { "time", new JObject {{ "data", new JArray  { refdateTimeConvert } }, { "dimensions", new JArray { "time" } }, { "init-time",  metadateTimeConvert } } }
                };

            var hearder_detail_sss = new JObject();
            hearder_detail_sss.Add("variables", variables_sss);
            var sss = new JObject();

            sss.Add("header", hearder_detail_sss);
            var block_sss = new JArray() { };

            block_sss.Add(sssJson);
            sss.Add("blocks", block_sss);

            var sssJsonout = JsonConvert.SerializeObject(sss, Formatting.None);

            blobFullPath = Path.Combine(year, month, day, "sss-" + hour + "00.json");
            blockBlob = container.GetBlockBlobReference(blobFullPath);
            blockBlob.UploadText(sssJsonout);

            hearder_sss = null;
            variables_sss = null;
            hearder_detail_sss = null;
            sss = null;
            block_sss = null;
            sssJsonout = null;

            var hearder_detail = new JObject();
            hearder_detail.Add("refTime", refdateTimeConvert); ;
            hearder_detail.Add("nx", 720);
            hearder_detail.Add("ny", 361);
            hearder_detail.Add("lo1", 0);
            hearder_detail.Add("la1", 90);
            hearder_detail.Add("lo2", 359.5);
            hearder_detail.Add("la2", -90);
            hearder_detail.Add("dx", 0.5);
            hearder_detail.Add("dy", 0.5);

            var meta1 = new JObject();
            var meta1_detail = new JObject();
            meta1_detail.Add("date", metadateTimeConvert);

            JArray currentOut = new JArray();

            currentOut.Add(new JObject
                {
                    {"header", hearder_detail },
                    { "data", currentuvJson},
                    {"meta", meta1_detail }
                });

            currentOut.Add(new JObject
                {
                     {"header", hearder_detail },
                    { "data", currentvvJson},
                    {"meta", meta1_detail }
                });

            var currentJson = JsonConvert.SerializeObject(currentOut, Formatting.None);

            blobFullPath = Path.Combine(year, month, day, "current-" + hour + "00.json");
            blockBlob = container.GetBlockBlobReference(blobFullPath);
            blockBlob.UploadText(currentJson);

            sssJson = null;
            sstJson = null;
            currentvvJson = null;
            currentuvJson = null;
            meta1 = null;
            meta1_detail = null;
            currentOut = null;
            currentJson = null;
            hearder_detail = null;
            meta1 = null;
            metadateTimeConvert = null;
            currentOut = null;
            currentuvJson = null;
            currentvvJson = null;
            currentJson = null;
            _log.Info($"Rtofs Forecast Json File Making Finish");
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