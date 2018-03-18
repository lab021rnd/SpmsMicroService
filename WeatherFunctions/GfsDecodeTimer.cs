using CodeFirstModels.Models;
using CodeFirstModels.Models.LogModel;
using CodeFirstModels.Models.OceanModel;
using EntityFramework.Utilities;
using Grib.Api;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace WeatherFunctions
{
    public static class GfsDecodeTimer
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

        [FunctionName("GfsDecodeTimer")]
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
                _jobList = _logModel.JOB_LIST_OCEAN_WEATHER.Where(d => d.IS_COMPLETE_JOB == false && d.JOB_TYPE == "GFS_DECODE").ToList();
                foreach (var item in _jobList)
                {
                    var blobPath = item.FILE_PATH;
                    var year = item.FILE_PATH.Split('\\')[0];
                    var month = item.FILE_PATH.Split('\\')[1];
                    var day = item.FILE_PATH.Split('\\')[2];
                    var fileName = item.FILE_PATH.Split('\\')[3];
                    var hour = Convert.ToString(Convert.ToInt32(item.FILE_PATH.Split('.')[1].Substring(1, 2)) + Convert.ToInt32(item.FILE_PATH.Split('.')[4].Substring(2, 2)));
                    var localFilePath = "";

                    _decodeList = new List<string> { year, month, day, hour, fileName, blobPath, localFilePath };
                    GfsRawFileDownLoad();
                    GfsNowCastInsert();

                    var whichDb = _logModel.CONDITION_OCEAN_WEATHER.First();

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

        public static void GfsRawFileDownLoad()
        {
            CloudBlobContainer container = _blobClient.GetContainerReference("gfs");
            var localFilePath = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\gfs\");
            localFilePath = Path.Combine(localFilePath, _decodeList[0], _decodeList[1], _decodeList[2], _decodeList[3], _decodeList[4]);
            Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(_decodeList[5]);
            blockBlob.DownloadToFile(localFilePath, FileMode.Create);
            _log.Info($"blob download complete");
            _decodeList[6] = localFilePath;
        }

        public static void RemoveDirectoryAndFile()
        {
            var tempPath = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\gfs\");
            System.IO.DirectoryInfo di = new DirectoryInfo(tempPath);

            if (di.Exists)
            {
                _log.Info($"delete start");
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                    _log.Info($"delete file : : {file.FullName.ToString()}");
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                    _log.Info($"delete directory : : {dir.FullName.ToString()}");
                }
            }
        }

        public static void GfsNowCastInsert()
        {
            _log.Info($"Start Insert");
            List<GFS_DB_INSERT_CHECK> gfsDbIndertCheck = new List<GFS_DB_INSERT_CHECK>();

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

                if (_oceanModel.GFS_DB_INSERT_CHECK.Where(s => s.DATE_OF_FILE == temp_surface.Time).Count() > 0)
                {
                    _log.Info("TRIGGER/*/" + "Gfs DB에 " + temp_surface.Time.ToString() + "데이터가 이미 있습니다.");
                    return;
                }
                var temp_above_ground = file.Where(d => d.ParameterName == "Temperature" && d.TypeOfLevel == "heightAboveGround" && d.Level == 2).Single();
                var press_surface = file.Where(d => d.ParameterName == "Pressure" && d.TypeOfLevel == "surface" && d.Level == 0).Single();
                var press_msl = file.Where(d => d.ParameterName == "Pressure reduced to MSL" && d.TypeOfLevel == "meanSea" && d.Level == 0).Single();

                ////////////////////////////////////////////////////////////////////////
                //////////////////DB INSERT
                ////////////////////////////////////////////////////////////////////////

                UTC = temp_surface.Time;
                LAT = temp_surface.GridCoordinateValues.Select(d => d.Latitude).ToArray();
                LON = temp_surface.GridCoordinateValues.Select(d => d.Longitude).ToArray();

                temp_surface.Values(out temp_surfaceValue);
                temp_above_ground.Values(out temp_above_groundValue);
                press_surface.Values(out press_surfaceValue);
                press_msl.Values(out press_mslValue);
            }

            List<GFS> gfsList = new List<GFS>();
            for (int j = 0; j < LAT.Length; j++)
            {
                ////////////////////////////////////////////////////////////////////////
                //////////////////예보 데이터
                ////////////////////////////////////////////////////////////////////////

                gfsList.Add(new GFS
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
            ////////////////////////////////////////////////////////////////////////
            //////////////////DB INSERT
            ////////////////////////////////////////////////////////////////////////

            int resumeCount = 0;
            int insertDataCount = 259920;
            var checkDate = UTC;

            _oceanModel.Database.CommandTimeout = 99999;
            GFS checkDB = _oceanModel.GFS.FirstOrDefault(s => s.UTC == checkDate);

            if (checkDB != null)
            {
                var resumeCount1 = _oceanModel.GFS.Count(s => s.UTC == checkDate);
                resumeCount = resumeCount1;
                if (resumeCount == insertDataCount)
                {
                    gfsDbIndertCheck.Add(new GFS_DB_INSERT_CHECK
                    {
                        FILE_NAME = _decodeList[4].ToLower(),
                        DATE_OF_FILE = UTC,
                        DATE_INSERTED = DateTime.UtcNow
                    });
                    gfsDbIndertCheck.ForEach(s => _oceanModel.GFS_DB_INSERT_CHECK.Add(s));
                    _oceanModel.SaveChanges();
                    return;
                }

                _log.Info("TRIGGER/*/" + "Gfs 데이터를 " + resumeCount.ToString() + "개부터 이어 받습니다.");
            }

            var gfsSplit = Split(gfsList, 10000);

            int index = 0;
            foreach (var item in gfsSplit)
            {
                if (index * 10000 >= resumeCount)
                {
                    EFBatchOperation.For(_oceanModel, _oceanModel.GFS).InsertAll(item);
                    Thread.Sleep(500);
                }
                var progress = Math.Round((double)index / 0.26, 2);
                _log.Info($"Gfs Nowcast Db insert :{_decodeList[0]}-{_decodeList[1]}-{_decodeList[2]} {_decodeList[3]}:00 / {progress} %");
                index++;
            }
            gfsSplit = null;
            gfsList = null;

            gfsDbIndertCheck.Add(new GFS_DB_INSERT_CHECK
            {
                FILE_NAME = _decodeList[4].ToLower(),
                DATE_OF_FILE = UTC,
                DATE_INSERTED = DateTime.UtcNow
            });

            gfsDbIndertCheck.ForEach(s => _oceanModel.GFS_DB_INSERT_CHECK.Add(s));
            _oceanModel.SaveChanges();

            _log.Info($"Gfs Db insert Finish");
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