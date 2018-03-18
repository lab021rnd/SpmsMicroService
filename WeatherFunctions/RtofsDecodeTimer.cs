using CodeFirstModels.Models;
using CodeFirstModels.Models.LogModel;
using CodeFirstModels.Models.OceanModel;
using EntityFramework.Utilities;
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
using System.Threading;

namespace WeatherFunctions
{
    public static class RtofsDecodeTimer
    {
        private static OCEAN_MODEL _oceanModel = new OCEAN_MODEL();
        private static LOG_MODEL _logModel = new LOG_MODEL();

        private static string _storageAccountName = CloudConn.StorageAccountName_OceanRaw;
        private static string _storageAccountKey = CloudConn.StorageAccountKey_OceanRaw;
        private static string _storageConnectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", _storageAccountName, _storageAccountKey);
        private static CloudStorageAccount _storageAccount = CloudStorageAccount.Parse(_storageConnectionString);
        private static CloudBlobClient _blobClient = _storageAccount.CreateCloudBlobClient();

        private static List<string[]> _decodeList;
        private static List<JOB_LIST_OCEAN_WEATHER> _jobList = new List<JOB_LIST_OCEAN_WEATHER>();
        private static bool _functionIsRunningOrNot = false;
        private static TraceWriter _log;

        private static List<RTOFS_POSITION_CONVERT> rtofsPostion;

        [FunctionName("RtofsDecordTimer")]
        public static void Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            _log = log;
            _decodeList = new List<string[]>();
            _log.Info($"Rtofs Decord function executed at: {DateTime.Now}");

            if (_functionIsRunningOrNot == true)
            {
                _log.Info($"Other Instance is Running at: {DateTime.Now}");
                return;
            }
            RemoveDirectoryAndFile();
            _functionIsRunningOrNot = true;

            if (rtofsPostion == null)
            {
                rtofsPostion = _oceanModel.RTOFS_POSITION_CONVERT.ToList();
            }

            try
            {
                _jobList = _logModel.JOB_LIST_OCEAN_WEATHER.Where(d => d.IS_COMPLETE_JOB == false && d.JOB_TYPE == "RTOFS_DECODE").ToList();
                foreach (var item in _jobList)
                {
                    var blobPath = item.FILE_PATH;
                    var year = item.FILE_PATH.Split('\\')[0];
                    var month = item.FILE_PATH.Split('\\')[1];
                    var day = item.FILE_PATH.Split('\\')[2];
                    var fileName = item.FILE_PATH.Split('\\')[3];
                    var hour = item.FILE_PATH.Split('_')[3].Substring(1, 3);

                    int hourTemp = Convert.ToInt16(hour);
                    hour = hourTemp.ToString("000");

                    var fileNameDiag = "rtofs_glo_2ds_n" + hour + "_3hrly_diag.nc";
                    var fileNameProg = "rtofs_glo_2ds_n" + hour + "_3hrly_prog.nc";
                    //var commonFileName = "rtofs_glo_2ds_n" + hour + "_3hrly.nc";

                    var blobPathDiag = year + "\\" + month + "\\" + day + "\\" + fileNameDiag;
                    var blobPathProg = year + "\\" + month + "\\" + day + "\\" + fileNameProg;

                    _decodeList.Add(new string[] { year, month, day, hour, fileNameDiag, blobPathDiag, "" });
                    _decodeList.Add(new string[] { year, month, day, hour, fileNameProg, blobPathProg, "" });

                    RtofsRawFileDownLoad();
                    RtofsNowCastInsert();
                    item.IS_COMPLETE_JOB = true;
                    item.DATE_OF_COMPLETE_JOB_TIME = DateTime.UtcNow;
                    item.REASON_OF_NOT_COMPLETE = "";
                    _logModel.SaveChanges();
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

        public static void RtofsRawFileDownLoad()
        {
            CloudBlobContainer container = _blobClient.GetContainerReference("rtofs");
            var localFilePathDiag = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\rtofs\");
            localFilePathDiag = Path.Combine(localFilePathDiag, _decodeList[0][1], _decodeList[0][1], _decodeList[0][2], _decodeList[0][3], _decodeList[0][4]);
            _decodeList[0][6] = localFilePathDiag;
            Directory.CreateDirectory(Path.GetDirectoryName(localFilePathDiag));
            container.GetBlockBlobReference(_decodeList[0][5]).DownloadToFile(localFilePathDiag, FileMode.Create);

            var localFilePathProg = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\rtofs\");
            localFilePathProg = Path.Combine(localFilePathProg, _decodeList[1][1], _decodeList[1][1], _decodeList[1][2], _decodeList[1][3], _decodeList[1][4]);
            _decodeList[1][6] = localFilePathProg;
            Directory.CreateDirectory(Path.GetDirectoryName(localFilePathProg));
            container.GetBlockBlobReference(_decodeList[1][5]).DownloadToFile(localFilePathProg, FileMode.Create);

            _log.Info($"blob download complete");
        }

        public static void RemoveDirectoryAndFile()
        {
            var tempPath = Environment.ExpandEnvironmentVariables(@"%HOME%\decode\rtofs\");
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

        public static void RtofsNowCastInsert()
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

                    var rtofs = new List<RTOFS>();
                    foreach (var item in rtofsPostion)
                    {
                        rtofs.Add(new RTOFS
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

                    //Nww3MakeJson(wavedataout);

                    var rtofsDbInsertCheck = new List<RTOFS_DB_INSERT_CHECK>();
                    int resumeCount = 0;
                    int insertDataCount = 259200;
                    var checkDate = utcTime;
                    _oceanModel.Database.CommandTimeout = 99999;

                    var checkDB = _oceanModel.RTOFS.FirstOrDefault(s => s.UTC == checkDate);

                    if (checkDB != null)
                    {
                        resumeCount = _oceanModel.RTOFS.Count(s => s.UTC == checkDate);

                        if (resumeCount == insertDataCount)
                        {
                            rtofsDbInsertCheck.Add(new RTOFS_DB_INSERT_CHECK
                            {
                                FILE_NAME = _decodeList[1][4].ToLower(),
                                DATE_OF_FILE = checkDate,
                                DATE_INSERTED = DateTime.UtcNow
                            });
                            rtofsDbInsertCheck.ForEach(s => _oceanModel.RTOFS_DB_INSERT_CHECK.Add(s));
                            _oceanModel.SaveChanges();
                            return;
                        }
                    }

                    var rtofsSplit = Split(rtofs, 10000);

                    foreach (var item in rtofsSplit.Select((value, index) => new { value, index }))
                    {
                        if (item.index * 10000 >= resumeCount)
                        {
                            EFBatchOperation.For(_oceanModel, _oceanModel.RTOFS).InsertAll(item.value);
                            Thread.Sleep(500);
                        }
                        var progress = Math.Round((double)item.index / 0.259, 2);
                        _log.Info($"Rtofs Nowcast Db insert :{_decodeList[0][0]}-{_decodeList[0][1]}-{_decodeList[0][2]} {_decodeList[0][3]}:00 / {progress} %");
                    }

                    rtofsSplit = null;
                    rtofs = null;
                    rtofsDbInsertCheck.Add(new RTOFS_DB_INSERT_CHECK
                    {
                        FILE_NAME = _decodeList[1][4].ToLower(),
                        DATE_OF_FILE = checkDate,
                        DATE_INSERTED = DateTime.UtcNow
                    });

                    rtofsDbInsertCheck.ForEach(s => _oceanModel.RTOFS_DB_INSERT_CHECK.Add(s));
                    _oceanModel.SaveChanges();
                    _log.Info($"Wave Db insert Finish");
                }
            }
        }

        public static void RtofsMakeJson(List<NWW3> wavedataout)
        {
            CloudBlobContainer container = _blobClient.GetContainerReference("weatherjson");

            for (int i = 0; i < 2; i++)
            {
                var utc = wavedataout[0].UTC;

                if (i == 1)
                {
                    utc = wavedataout.Last().UTC;
                }

                var utcString = utc.ToString("yyyy,MM,dd,HH");
                var year = utcString.Split(',')[0];
                var month = utcString.Split(',')[1];
                var day = utcString.Split(',')[2];
                var hour = utcString.Split(',')[3];

                var ugrd = wavedataout.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.UGRD).ToList();
                var vgrd = wavedataout.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.VGRD).ToList();
                var htsgw = wavedataout.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.HTSGW).ToList();
                var mwsdir = wavedataout.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.MWSDIR).ToList();
                var mwsper = wavedataout.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.MWSPER).ToList();

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
                        ugrdJson.Add(item);
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
                        vgrdJson.Add(item);
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
                        mwsdirJson.Add(item);
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
                        mwsperJson.Add(item);
                    }
                }

                var refdateTimeConvert = wavedataout[0].UTC.AddHours(3).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
                var metadateTimeConvert = wavedataout[0].UTC.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

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
            }
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