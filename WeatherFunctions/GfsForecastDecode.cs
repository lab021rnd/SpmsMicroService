using CodeFirstModels.Models;
using CodeFirstModels.Models.LogModel;
using CodeFirstModels.Models.OceanModel;
using EntityFramework.Utilities;
using FluentFTP;
using Grib.Api;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace WeatherFunctions
{
    public static class GfsForecastDecode
    {
        private static OCEAN_MODEL _oceanModel = new OCEAN_MODEL();
        private static LOG_MODEL _logModel = new LOG_MODEL();

        private static string _storageAccountName = CloudConn.StorageAccountName_OceanRaw;
        private static string _storageAccountKey = CloudConn.StorageAccountKey_OceanRaw;
        private static string _storageConnectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", _storageAccountName, _storageAccountKey);
        private static CloudStorageAccount _storageAccount = CloudStorageAccount.Parse(_storageConnectionString);
        private static CloudBlobClient _blobClient = _storageAccount.CreateCloudBlobClient();
        private static string[] _decodeList;

        private static string _ftpAccountId = CloudConn.FtpAccountId_Nww3;
        private static string _ftpAccountPw = CloudConn.FtpAccountPw_Nww3;

        private static bool _functionIsRunningOrNot = false;
        private static int _dayToCheckForDownload = 4;
        private static DateTime _dateNextGfsForecastFileToGet;

        private static List<string[]> _fileListToNeedDownload;

        private static TraceWriter _log;

        [FunctionName("GfsForecastDecode")]
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

            RemoveDirectoryAndFile();

            try
            {
                _functionIsRunningOrNot = true;

                if (_logModel.CONDITION_OCEAN_WEATHER.Single().FILE_NAME_LAST_OF_GFS_FORECAST == null || _logModel.CONDITION_OCEAN_WEATHER.Single().DATE_LAST_FILE_OF_GFS_FORECAST == new DateTime(1900, 01, 01, 0, 0, 0))
                {
                    var conditionWeatherDb = _logModel.CONDITION_OCEAN_WEATHER.First();
                    conditionWeatherDb.FILE_NAME_LAST_OF_GFS_FORECAST = "gfs.t00z.pgrb2b.0p50.f003";
                    conditionWeatherDb.DATE_LAST_FILE_OF_GFS_FORECAST = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0, 0, 0);
                    _logModel.SaveChanges();
                }

                var fileNextGfsForecastFileToGet = _logModel.CONDITION_OCEAN_WEATHER.Single().FILE_NAME_LAST_OF_GFS_FORECAST;
                var tempHour = fileNextGfsForecastFileToGet.Split('.')[4].Substring(1, 3);
                var hour = Convert.ToInt32(tempHour);
                _dateNextGfsForecastFileToGet = _logModel.CONDITION_OCEAN_WEATHER.Single().DATE_LAST_FILE_OF_GFS_FORECAST;

                var whichDb = _logModel.CONDITION_OCEAN_WEATHER.First();

                if (hour == 210)
                {
                    hour = 06;
                    _dateNextGfsForecastFileToGet = _dateNextGfsForecastFileToGet.AddDays(1);

                    if (whichDb.DB_OF_GFS_FORECAST.ToUpper() == "PROXY")
                    {
                        _oceanModel.Database.ExecuteSqlCommand("TRUNCATE TABLE [" + "GFS_FORECAST" + "]");
                        _oceanModel.SaveChanges();
                    }
                    else if (whichDb.DB_OF_GFS_FORECAST.ToUpper() == "BASIC")
                    {
                        _oceanModel.Database.ExecuteSqlCommand("TRUNCATE TABLE [" + "GFS_FORECAST_PROXY" + "]");
                        _oceanModel.SaveChanges();
                    }
                }
                else
                {
                    hour = hour + 3;
                }

                tempHour = hour.ToString("000");
                var fileNameGfs = "gfs.t00z.pgrb2.0p50.f" + tempHour;
                var tempfolderName = "gfs." + _dateNextGfsForecastFileToGet.ToString("yyyyMMdd") + "00";

                var remoteFilePath = "/pub/data/nccf/com/gfs/prod/" + tempfolderName + "/" + fileNameGfs;

                var localPath = FtpDownLoad(remoteFilePath, fileNameGfs, _dateNextGfsForecastFileToGet);

                if (localPath != "false")
                {
                    var year = _dateNextGfsForecastFileToGet.AddHours(hour).Year.ToString();
                    var month = _dateNextGfsForecastFileToGet.AddHours(hour).Month.ToString();
                    var day = _dateNextGfsForecastFileToGet.AddHours(hour).Day.ToString();
                    tempHour = _dateNextGfsForecastFileToGet.AddHours(hour).Hour.ToString();

                    _decodeList = new string[] { year, month, day, tempHour, fileNameGfs, remoteFilePath, localPath };

                    if (whichDb.DB_OF_GFS_FORECAST.ToUpper() == "PROXY")
                    {
                        GfsForeCastInsertBasic();
                    }
                    else if (whichDb.DB_OF_GFS_FORECAST.ToUpper() == "BASIC")
                    {
                        GfsForeCastInsertProxy();
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
            }
        }

        public static void RemoveDirectoryAndFile()
        {
            var tempPath = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\gfsForecast\");
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

        private static string FtpDownLoad(string filePath, string fileNameGfs, DateTime dateNextGfsForecastFileToGet)
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

                            _log.Info($"{dateNextGfsForecastFileToGet.ToString("yyyy-MM-dd mm-HH")} / {filePath}   Progres : {Math.Round(x, 0)} % ");
                        }
                    });

                    localFilePath = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\gfsForecast\");
                    localFilePath = Path.Combine(localFilePath, dateNextGfsForecastFileToGet.Year.ToString("0000"), dateNextGfsForecastFileToGet.Month.ToString("00"), dateNextGfsForecastFileToGet.Day.ToString("00"), fileNameGfs);

                    Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));
                    conn.DownloadFile(@localFilePath, filePath, true, FtpVerify.Retry, progress);
                    _log.Info($"{dateNextGfsForecastFileToGet.ToString("yyyy-MM-dd mm-HH")} / {filePath}  Progres : Complete");
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

        public static void GfsForeCastInsertBasic()
        {
            DateTime UTC;
            double[] LAT;
            double[] LON;
            double[] temp_surfaceValue;
            double[] temp_above_groundValue;
            double[] press_surfaceValue;
            double[] press_mslValue;

            GribEnvironment.Init();
            List<GribMessage> gfs = new List<GribMessage>();
            _log.Info($"Start Insert : {_decodeList[6]}");
            using (GribFile file = new GribFile(_decodeList[6]))
            {
                file.Context.EnableMultipleFieldMessages = true;

                var temp_surface = file.Where(d => d.ParameterName == "Temperature" && d.TypeOfLevel == "surface" && d.Level == 0).Single();
                var temp_above_ground = file.Where(d => d.ParameterName == "Temperature" && d.TypeOfLevel == "heightAboveGround" && d.Level == 2).Single();
                var press_surface = file.Where(d => d.ParameterName == "Pressure" && d.TypeOfLevel == "surface" && d.Level == 0).Single();
                var press_msl = file.Where(d => d.ParameterName == "Pressure reduced to MSL" && d.TypeOfLevel == "meanSea" && d.Level == 0).Single();

                UTC = temp_surface.Time;
                LAT = temp_surface.GridCoordinateValues.Select(d => d.Latitude).ToArray();
                LON = temp_surface.GridCoordinateValues.Select(d => d.Longitude).ToArray();

                temp_surface.Values(out temp_surfaceValue);
                temp_above_ground.Values(out temp_above_groundValue);
                press_surface.Values(out press_surfaceValue);
                press_msl.Values(out press_mslValue);
            }

            List<GFS_FORECAST> gfsList = new List<GFS_FORECAST>();
            for (int j = 0; j < LAT.Length; j++)
            {
                gfsList.Add(new GFS_FORECAST
                {
                    UTC = UTC,
                    LAT = Convert.ToDecimal(LAT[j]),
                    LON = Convert.ToDecimal(LON[j]),
                    TEMP_SURFACE = Convert.ToSingle(temp_surfaceValue[j] - 273.15f),
                    TEMP_ABOVE_GROUND = Convert.ToSingle(temp_above_groundValue[j] - 273.15f),
                    PRESSURE_SURFACE = Convert.ToSingle(press_surfaceValue[j]),
                    PRESSURE_MSL = Convert.ToSingle(press_mslValue[j]),
                });
            }

            _oceanModel.Database.CommandTimeout = 99999;
            var gfsSplit = Split(gfsList, 10000);
            int index = 0;
            foreach (var item in gfsSplit)
            {
                EFBatchOperation.For(_oceanModel, _oceanModel.GFS_FORECAST).InsertAll(item);
                Thread.Sleep(10);
                var progress = Math.Round((double)index / 0.26, 2);
                _log.Info($"Gfs Forecast Db insert :{_decodeList[0]}-{_decodeList[1]}-{_decodeList[2]} {_decodeList[3]}:00 / {progress} %");
                index++;
            }
            gfsSplit = null;
            gfsList = null;

            _log.Info($"Gfs Forecast Db insert :{_decodeList[0]}-{_decodeList[1]}-{_decodeList[2]} {_decodeList[3]}:00 / 100 %");
            var conditionWeatherDb = _logModel.CONDITION_OCEAN_WEATHER.First();
            conditionWeatherDb.DATE_LAST_FILE_OF_GFS_FORECAST = _dateNextGfsForecastFileToGet;
            conditionWeatherDb.FILE_NAME_LAST_OF_GFS_FORECAST = _decodeList[4];

            if (_decodeList[4] == "gfs.t00z.pgrb2.0p50.f210")
            {
                conditionWeatherDb.DB_OF_GFS_FORECAST = "BASIC";
                conditionWeatherDb.DATE_LAST_OF_GFS = UTC;
                //conditionWeatherDb.DATE_LAST_OF_GFS_JSON = UTC;
            }
            _logModel.SaveChanges();
        }

        public static void GfsForeCastInsertProxy()
        {
            DateTime UTC;
            double[] LAT;
            double[] LON;
            double[] temp_surfaceValue;
            double[] temp_above_groundValue;
            double[] press_surfaceValue;
            double[] press_mslValue;

            GribEnvironment.Init();
            List<GribMessage> gfs = new List<GribMessage>();
            _log.Info($"Start Insert : {_decodeList[6]}");
            using (GribFile file = new GribFile(_decodeList[6]))
            {
                file.Context.EnableMultipleFieldMessages = true;

                var temp_surface = file.Where(d => d.ParameterName == "Temperature" && d.TypeOfLevel == "surface" && d.Level == 0).SingleOrDefault();
                var temp_above_ground = file.Where(d => d.ParameterName == "Temperature" && d.TypeOfLevel == "heightAboveGround" && d.Level == 2).SingleOrDefault();
                var press_surface = file.Where(d => d.ParameterName == "Pressure" && d.TypeOfLevel == "surface" && d.Level == 0).SingleOrDefault();
                var press_msl = file.Where(d => d.ParameterName == "Pressure reduced to MSL" && d.TypeOfLevel == "meanSea" && d.Level == 0).SingleOrDefault();

                UTC = temp_surface.Time;
                LAT = temp_surface.GridCoordinateValues.Select(d => d.Latitude).ToArray();
                LON = temp_surface.GridCoordinateValues.Select(d => d.Longitude).ToArray();

                temp_surface.Values(out temp_surfaceValue);
                temp_above_ground.Values(out temp_above_groundValue);
                press_surface.Values(out press_surfaceValue);
                press_msl.Values(out press_mslValue);
            }

            List<GFS_FORECAST_PROXY> gfsList = new List<GFS_FORECAST_PROXY>();
            for (int j = 0; j < LAT.Length; j++)
            {
                gfsList.Add(new GFS_FORECAST_PROXY
                {
                    UTC = UTC,
                    LAT = Convert.ToDecimal(LAT[j]),
                    LON = Convert.ToDecimal(LON[j]),
                    TEMP_SURFACE = Convert.ToSingle(temp_surfaceValue[j] - 273.15f),
                    TEMP_ABOVE_GROUND = Convert.ToSingle(temp_above_groundValue[j] - 273.15f),
                    PRESSURE_SURFACE = Convert.ToSingle(press_surfaceValue[j]),
                    PRESSURE_MSL = Convert.ToSingle(press_mslValue[j]),
                });
            }

            _oceanModel.Database.CommandTimeout = 99999;
            var gfsSplit = Split(gfsList, 10000);
            int index = 0;
            foreach (var item in gfsSplit)
            {
                EFBatchOperation.For(_oceanModel, _oceanModel.GFS_FORECAST_PROXY).InsertAll(item);
                Thread.Sleep(500);
                var progress = Math.Round((double)index / 0.26, 2);
                _log.Info($"Gfs Forecast Db insert :{_decodeList[0]}-{_decodeList[1]}-{_decodeList[2]} {_decodeList[3]}:00 / {progress} %");
                index++;
            }
            gfsSplit = null;
            gfsList = null;

            _log.Info($"Gfs Forecast Db insert :{_decodeList[0]}-{_decodeList[1]}-{_decodeList[2]} {_decodeList[3]}:00 / 100 %");

            var conditionWeatherDb = _logModel.CONDITION_OCEAN_WEATHER.First();

            conditionWeatherDb.DATE_LAST_FILE_OF_GFS_FORECAST = _dateNextGfsForecastFileToGet;
            conditionWeatherDb.FILE_NAME_LAST_OF_GFS_FORECAST = _decodeList[4];

            if (_decodeList[4] == "gfs.t00z.pgrb2.0p50.f210")
            {
                conditionWeatherDb.DB_OF_GFS_FORECAST = "PROXY";
                conditionWeatherDb.DATE_LAST_OF_GFS = UTC;
                //conditionWeatherDb.DATE_LAST_OF_GFS_JSON = UTC;
            }
            _logModel.SaveChanges();
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