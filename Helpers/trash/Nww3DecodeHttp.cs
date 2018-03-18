﻿//using CodeFirstModels.Models;
//using CodeFirstModels.Models.LogModel;
//using CodeFirstModels.Models.OceanModel;
//using EntityFramework.Utilities;
//using Grib.Api;
//using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.Http;
//using Microsoft.Azure.WebJobs.Host;
//using Microsoft.WindowsAzure.Storage;
//using Microsoft.WindowsAzure.Storage.Blob;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using System;
//using System.Collections.Generic;
//using System.Data.Entity;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Threading;
//using System.Threading.Tasks;

//namespace WeatherFunctions
//{
//    public static class Nww3DecodeHttp
//    {
//        private static string StorageAccountName;
//        private static string StorageAccountKey;

//        [FunctionName("Nww3DecodeHttp")]
//        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "Nww3DecodeHttp/{year}/{month}/{day}/{hour}/{fileName}")]HttpRequestMessage req, string year, string month, string day, string hour, string fileName, TraceWriter log)
//        {
//            StorageAccountName = CloudConn.StorageAccountName_OceanRaw;
//            StorageAccountKey = CloudConn.StorageAccountKey_OceanRaw;

//            log.Info("C# HTTP trigger function processed a request.");

//            // Fetching the name from the path parameter in the request URL
//            var path = year + "\\" + month + "\\" + day + "\\" + fileName;
//            var date = Convert.ToDateTime(year + "-" + month + "-" + day + " " + hour + "00:00");

//            var oceanModel = new OCEAN_MODEL();
//            var logModel = new LOG_MODEL();
//            var jobList = logModel.JOB_LIST_OCEAN_WEATHER.Where(d => d.JOB_TYPE == "NWW3_DECODE" && d.FILE_PATH == path && d.IS_COMPLETE_JOB == false);
//            foreach (var item in jobList)
//            {
//                item.IS_COMPLETE_JOB = true;
//                item.REASON_OF_NOT_COMPLETE = "";
//            }
//            oceanModel.SaveChanges();

//            try
//            {
//                var localFilePath = Nww3RawFileDownLoad(path, fileName, log);
//                log.Info($"{localFilePath}");

//                // 이미 db에 데이터가 있는지 확인
//                var checkIsLogExist = oceanModel.NWW3_DB_INSERT_CHECK.Count(d => d.DATE_OF_FILE == date && d.FILE_NAME == fileName.ToLower());
//                if (checkIsLogExist == 0)
//                {
//                    var nowCast = Task.Run(() => Nww3NowCastInsert(year, month, day, hour, path, fileName, localFilePath, log));

//                    if (hour == "99")
//                    {
//                        var forCast = nowCast.ContinueWith(d => Nww3ForcastInsert(year, month, day, hour, path, fileName, localFilePath, log));
//                        forCast.ContinueWith(d => RemoveDirectoryAndFile(localFilePath));
//                    }
//                    else
//                    {
//                        nowCast.ContinueWith(d => RemoveDirectoryAndFile(localFilePath));
//                    }
//                }
//            }
//            catch (Exception e)
//            {
//                jobList = logModel.JOB_LIST_OCEAN_WEATHER.Where(d => d.FILE_PATH == path && d.IS_COMPLETE_JOB == true);
//                foreach (var item in jobList)
//                {
//                    item.IS_COMPLETE_JOB = false;
//                    item.REASON_OF_NOT_COMPLETE = e.ToString();
//                }
//                oceanModel.SaveChanges();
//            }

//            return req.CreateResponse(HttpStatusCode.OK, "Hello " + fileName);
//        }

//        public static string Nww3RawFileDownLoad(string path, string fileName, TraceWriter log)
//        {
//            string storageConnectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
//                                                          StorageAccountName, StorageAccountKey);
//            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
//            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
//            CloudBlobContainer container = blobClient.GetContainerReference("nww3");
//            var localFilePath = Environment.ExpandEnvironmentVariables(@"%HOME%\temp\" + fileName);
//            Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));
//            CloudBlockBlob blockBlob = container.GetBlockBlobReference(path);
//            blockBlob.DownloadToFile(localFilePath, FileMode.Create);
//            log.Info($"blob download complete");
//            return localFilePath;
//        }

//        public static void RemoveDirectoryAndFile(string localFilePath)
//        {
//            System.IO.FileInfo file = new FileInfo(localFilePath);
//            file.Delete();
//        }

//        public static void Nww3NowCastInsert(string year, string month, string day, string hour, string path, string fileName, string localFilePath, TraceWriter log)
//        {
//            log.Info($"Start Insert");
//            var oceanModel = new OCEAN_MODEL();
//            var logModel = new LOG_MODEL();
//            int elementCount = 19;
//            int elementTotalCount = 0;
//            int num = 0;

//            var UTC = new DateTime[2];
//            var LAT = new double[2][];
//            var LON = new double[2][];
//            var ICEC = new double[2][];
//            var SWDIR_SEQ1 = new double[2][];
//            var SWDIR_SEQ2 = new double[2][];
//            var WVDIR = new double[2][];
//            var MWSPER = new double[2][];
//            var SWPER_SEQ1 = new double[2][];
//            var SWPER_SEQ2 = new double[2][];
//            var MVPER = new double[2][];
//            var DIRPW = new double[2][];
//            var PWERPW = new double[2][];
//            var DIRSW = new double[2][];
//            var PWERSW = new double[2][];
//            var HTSGW = new double[2][];
//            var SWELL_SEQ1 = new double[2][];
//            var SWELL_SEQ2 = new double[2][];
//            var WVHGT = new double[2][];
//            var UGRD = new double[2][];
//            var VGRD = new double[2][];
//            var WDIR = new double[2][];
//            var WIND = new double[2][];
//            var MWSDIR = new double[2][];

//            try
//            {
//                GribEnvironment.Init();
//                List<GribMessage> Nww3 = new List<GribMessage>();

//                using (GribFile gribFile = new GribFile(@localFilePath))
//                {
//                    gribFile.Context.EnableMultipleFieldMessages = true;

//                    elementTotalCount = elementCount * 2;
//                    foreach (GribMessage item in gribFile)
//                    {
//                        if (item.StepRange == "6")
//                        {
//                            break;
//                        }
//                        Nww3.Add(item);
//                    }
//                }

//                List<NWW3> wavedataout = new List<NWW3> { };
//                List<NWW3_DB_INSERT_CHECK> nww3DbInsertCheck = new List<NWW3_DB_INSERT_CHECK>();

//                for (int i = 0; i < elementTotalCount; i += elementCount)
//                {
//                    UTC[num] = Nww3.ElementAt(0 + i).Time;
//                    LAT[num] = Nww3.ElementAt(0 + i).GridCoordinateValues.Select(d => d.Latitude).ToArray();
//                    LON[num] = Nww3.ElementAt(0 + i).GridCoordinateValues.Select(d => d.Longitude).ToArray();
//                    Nww3.ElementAt(4 + i).Values(out ICEC[num]);
//                    Nww3.ElementAt(17 + i).Values(out SWDIR_SEQ1[num]);
//                    Nww3.ElementAt(18 + i).Values(out SWDIR_SEQ2[num]);
//                    Nww3.ElementAt(16 + i).Values(out WVDIR[num]);
//                    Nww3.ElementAt(6 + i).Values(out MWSPER[num]);
//                    Nww3.ElementAt(14 + i).Values(out SWPER_SEQ1[num]);
//                    Nww3.ElementAt(15 + i).Values(out SWPER_SEQ2[num]);
//                    Nww3.ElementAt(13 + i).Values(out MVPER[num]);
//                    Nww3.ElementAt(9 + i).Values(out DIRPW[num]);
//                    Nww3.ElementAt(7 + i).Values(out PWERPW[num]);
//                    Nww3.ElementAt(5 + i).Values(out HTSGW[num]);
//                    Nww3.ElementAt(11 + i).Values(out SWELL_SEQ1[num]);
//                    Nww3.ElementAt(12 + i).Values(out SWELL_SEQ2[num]);
//                    Nww3.ElementAt(10 + i).Values(out WVHGT[num]);
//                    Nww3.ElementAt(2 + i).Values(out UGRD[num]);
//                    Nww3.ElementAt(3 + i).Values(out VGRD[num]);
//                    Nww3.ElementAt(1 + i).Values(out WDIR[num]);
//                    Nww3.ElementAt(0 + i).Values(out WIND[num]);
//                    Nww3.ElementAt(8 + i).Values(out MWSDIR[num]);
//                    num++;
//                }

//                for (int i = 0; i < num; i++)
//                {
//                    for (int j = 0; j < LAT[i].Length; j++)
//                    {
//                        wavedataout.Add(new NWW3
//                        {
//                            UTC = UTC[i],
//                            LAT = Convert.ToDecimal(LAT[i][j]),
//                            LON = Convert.ToDecimal(LON[i][j]),
//                            SWDIR = Convert.ToSingle(SWDIR_SEQ1[i][j]),
//                            WVDIR = Convert.ToSingle(WVDIR[i][j]),
//                            MWSPER = Convert.ToSingle(MWSPER[i][j]),
//                            SWPER = Convert.ToSingle(SWPER_SEQ1[i][j]),
//                            WVPER = Convert.ToSingle(MVPER[i][j]),
//                            HTSGW = Convert.ToSingle(HTSGW[i][j]),
//                            SWELL = Convert.ToSingle(SWELL_SEQ1[i][j]),
//                            WVHGT = Convert.ToSingle(WVHGT[i][j]),
//                            UGRD = Convert.ToSingle(UGRD[i][j]),
//                            VGRD = Convert.ToSingle(VGRD[i][j]),
//                            MWSDIR = Convert.ToSingle(MWSDIR[i][j])
//                        });
//                    }
//                }
//                Nww3MakeJson(wavedataout);

//                int resumeCount = 0;

//                int insertDataCount = 231120;
//                var checkDate = UTC[0];
//                var checkDate3 = UTC[1];

//                oceanModel.Database.CommandTimeout = 99999;
//                var checkDB = oceanModel.NWW3.FirstOrDefault(s => s.UTC == checkDate);

//                if (checkDB != null)
//                {
//                    var resumeCount1 = oceanModel.NWW3.Count(s => s.UTC == checkDate);
//                    var resumeCount2 = oceanModel.NWW3.Count(s => s.UTC == checkDate3);
//                    resumeCount = resumeCount1 + resumeCount2;

//                    if (resumeCount == insertDataCount * 2)
//                    {
//                        nww3DbInsertCheck.Add(new NWW3_DB_INSERT_CHECK
//                        {
//                            FILE_NAME = fileName.ToLower(),
//                            DATE_OF_FILE = UTC[0],
//                            DATE_INSERTED = DateTime.UtcNow
//                        });

//                        nww3DbInsertCheck.ForEach(s => oceanModel.NWW3_DB_INSERT_CHECK.Add(s));
//                        oceanModel.SaveChanges();
//                        return;
//                    }
//                }

//                var waveSplit = Split(wavedataout, 10000);

//                int index = 0;
//                foreach (var item in waveSplit)
//                {
//                    if (index * 10000 >= resumeCount)
//                    {
//                        EFBatchOperation.For(oceanModel, oceanModel.NWW3).InsertAll(item);
//                        Thread.Sleep(10);
//                    }

//                    var progress = Math.Round((double)index / 0.46, 2);
//                    log.Info($"Wave Nowcast Db insert :{year}-{month}-{day} {hour}:00 / {progress} %");
//                    index++;
//                }

//                waveSplit = null;
//                wavedataout = null;

//                nww3DbInsertCheck.Add(new NWW3_DB_INSERT_CHECK
//                {
//                    FILE_NAME = fileName.ToLower(),
//                    DATE_OF_FILE = UTC[0],
//                    DATE_INSERTED = DateTime.UtcNow
//                });

//                nww3DbInsertCheck.ForEach(s => oceanModel.NWW3_DB_INSERT_CHECK.Add(s));
//                oceanModel.SaveChanges();

//                log.Info($"Wave Db insert Finish");

//                var jobList = logModel.JOB_LIST_OCEAN_WEATHER.Where(d => d.FILE_PATH == path && d.IS_COMPLETE_JOB == true);
//                foreach (var item in jobList)
//                {
//                    item.DATE_OF_COMPLETE_JOB_TIME = DateTime.UtcNow;
//                }
//                oceanModel.SaveChanges();
//            }
//            catch (Exception e)
//            {
//                var jobList = logModel.JOB_LIST_OCEAN_WEATHER.Where(d => d.FILE_PATH == path && d.IS_COMPLETE_JOB == true);
//                foreach (var item in jobList)
//                {
//                    item.IS_COMPLETE_JOB = false;
//                    item.REASON_OF_NOT_COMPLETE = e.ToString();
//                }
//                oceanModel.SaveChanges();
//                log.Info($"{e.ToString()}");
//            }
//        }

//        public static void Nww3MakeJson(List<NWW3> wavedataout)
//        {
//            string storageConnectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
//                                                          StorageAccountName, StorageAccountKey);
//            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
//            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
//            CloudBlobContainer container = blobClient.GetContainerReference("weatherjson");

//            for (int i = 0; i < 2; i++)
//            {
//                var utc = wavedataout[0].UTC;

//                if (i == 1)
//                {
//                    utc = wavedataout.Last().UTC;
//                }

//                var utcString = utc.ToString("yyyy,MM,dd,HH");
//                var year = utcString.Split(',')[0];
//                var month = utcString.Split(',')[1];
//                var day = utcString.Split(',')[2];
//                var hour = utcString.Split(',')[3];

//                var ugrd = wavedataout.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.UGRD).ToList();
//                var vgrd = wavedataout.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.VGRD).ToList();
//                var htsgw = wavedataout.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.HTSGW).ToList();
//                var mwsdir = wavedataout.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.MWSDIR).ToList();
//                var mwsper = wavedataout.Where(d => d.UTC == utc).OrderBy(d => d.LON).OrderByDescending(d => d.LAT).Select(d => d.MWSPER).ToList();

//                var ugrdJson = new JArray();
//                var vgrdJson = new JArray();
//                var mwsdirJson = new JArray();
//                var mwsperJson = new JArray();

//                foreach (var item in ugrd)
//                {
//                    if (item >= 9999)
//                    {
//                        ugrdJson.Add(0);
//                    }
//                    else
//                    {
//                        ugrdJson.Add(item);
//                    }
//                }

//                foreach (var item in vgrd)
//                {
//                    if (item >= 9999)
//                    {
//                        vgrdJson.Add(0);
//                    }
//                    else
//                    {
//                        vgrdJson.Add(item);
//                    }
//                }

//                foreach (var item in mwsdir)
//                {
//                    if (item >= 9999)
//                    {
//                        mwsdirJson.Add(0);
//                    }
//                    else
//                    {
//                        mwsdirJson.Add(item);
//                    }
//                }

//                foreach (var item in mwsper)
//                {
//                    if (item >= 9999)
//                    {
//                        mwsperJson.Add(0);
//                    }
//                    else
//                    {
//                        mwsperJson.Add(item);
//                    }
//                }

//                var refdateTimeConvert = wavedataout[0].UTC.AddHours(3).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
//                var metadateTimeConvert = wavedataout[0].UTC.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

//                JObject dimensions = new JObject
//                {
//                    {"lat", new JObject {{ "@dimension", "lat" }, { "length", 321 }}},
//                    {"lon", new JObject {{ "@dimension", "lon" }, { "length", 720 }}},
//                    {"time", new JObject {{ "@dimension", "time" }, { "length", 1 }}},
//                    {"featureType", "GRID"},
//                    {"file_format", "GGRIB-2"},
//                    {"history", "Read using CDM IOSP GribCollection v3"}
//                };

//                JObject variables = new JObject
//                {
//                    {"LatLon_Projection", new JObject {{ "@variable", "LatLon_Projection" }, { "earth_radius", 6371229 }, { "grid_mapping_name", "latitude_longitude" }, { "type", "int" }}},

//                    { "Primary_wave_direction_surface", new JObject {{ "@variable", "Primary_wave_direction_surface" }, { "Grib2_Generating_Process_Type", "Forecast" }, { "Grib2_Level_Type", "Ground or water surface" },{ "Grib2_Parameter", new JArray() { 10, 0, 10 } },{ "Grib2_Parameter_Category", "Waves" },{ "Grib2_Parameter_Discipline", "Oceanographic products" },{ "Grib2_Parameter_Name", "Primary wave direction" },{ "Grib_Variable_Id", "VAR_10-0-10_L1" },{ "abbreviation", "DIRPW" } ,{ "coordinates", "reftime time lat lon " },{ "data", new JObject { {"block",0 }, {"method","ppak"  }} },{ "dimensions", new JArray { "time", "lat", "lon" } }, { "grid_mapping", "LatLon_Projection" },{ "long_name", "Primary wave direction @ Ground or water surface" },  { "missing_value", new JValue((object)null) } ,{ "type", "float" }, { "units", "degree_true" } } },

//                     { "Primary_wave_mean_period_surface", new JObject {{ "@variable", "Primary_wave_mean_period_surface" }, { "Grib2_Generating_Process_Type", "Forecast" }, { "Grib2_Level_Type", "Ground or water surface" },{ "Grib2_Parameter", new JArray() { 10, 0, 11 } },{ "Grib2_Parameter_Category", "Waves" },{ "Grib2_Parameter_Discipline", "Oceanographic products" },{ "Grib2_Parameter_Name", "Primary wave mean period" },{ "Grib_Variable_Id", "VAR_10-0-11_L1" },{ "abbreviation", "PERPW" } ,{ "coordinates", "reftime time lat lon " },{ "data", new JObject { {"block",1 }, {"method","ppak"  }} },{ "dimensions", new JArray { "time", "lat", "lon" } }, { "grid_mapping", "LatLon_Projection" },{ "long_name", "Primary wave mean period @ Ground or water surface" },  { "missing_value", new JValue((object)null) } ,{ "type", "float" }, { "units", "s" } } },

//                    { "lat", new JObject {{ "@variable", "lat" }, { "dimensions", new JArray() { "lat" } }, { "sequence", new JObject { { "delta", -0.5 }, { "size", 321 }, { "start", 80 } } }, { "type", "float" },{ "units", "degrees_north" } } },

//                    { "lon", new JObject {{ "@variable", "lon" }, { "dimensions", new JArray() { "lon" } }, { "sequence", new JObject { { "delta", 0.5 }, { "size", 720 }, { "start", 0 } } }, { "type", "float" },{ "units", "degrees_east" } } },

//                    { "time", new JObject {{ "@variable", "time" }, { "calendar", "proleptic_gregorian" }, { "data", new JArray  { refdateTimeConvert } }, { "dimensions", new JArray { "time" } }, { "init-time",  metadateTimeConvert }, { "long_name", "GRIB forecast or observation time" }, { "standard_name", "time" }, { "type", "double" }, { "units", "Hour since "+ metadateTimeConvert } } }
//                };

//                var hearder_detail = new JObject();
//                hearder_detail.Add("@group", ""); ;
//                hearder_detail.Add("Analysis_or_forecast_generating_process_identifier_defined_by_originating_centre", "Global Multi-Grid Wave Model (Static Grids)");
//                hearder_detail.Add("Conventions", "CF-1.6");
//                hearder_detail.Add("GRIB_table_version", "2,1");
//                hearder_detail.Add("Originating_or_generating_Center", "US National Weather Service, National Centres for Environmental Prediction (NCEP)");
//                hearder_detail.Add("Originating_or_generating_Subcenter", "0");
//                hearder_detail.Add("Type_of_generating_process", "Forecast");
//                hearder_detail.Add("converter", "netcdf2json");
//                hearder_detail.Add("dimensions", dimensions);
//                hearder_detail.Add("variables", variables);

//                var wave = new JObject();
//                wave.Add("header", hearder_detail);

//                var block = new JArray() { };
//                block.Add(mwsdirJson);
//                block.Add(mwsperJson);

//                wave.Add("blocks", block);

//                var waveJson = JsonConvert.SerializeObject(wave, Formatting.None);

//                var blobFullPath = Path.Combine(year, month, day, "wave-" + hour + "00.json");
//                CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobFullPath);
//                blockBlob.UploadText(waveJson);

//                hearder_detail = new JObject();
//                hearder_detail.Add("refTime", refdateTimeConvert); ;
//                hearder_detail.Add("nx", 720);
//                hearder_detail.Add("ny", 321);
//                hearder_detail.Add("lo1", 0);
//                hearder_detail.Add("la1", 80);
//                hearder_detail.Add("lo2", 359.5);
//                hearder_detail.Add("la2", -80);
//                hearder_detail.Add("dx", 0.5);
//                hearder_detail.Add("dy", 0.5);

//                var meta1 = new JObject();
//                var meta1_detail = new JObject();
//                meta1_detail.Add("date", metadateTimeConvert);

//                JArray waveOut = new JArray();

//                waveOut.Add(new JObject
//                {
//                    {"header", hearder_detail },
//                    { "data", ugrdJson},
//                    {"meta", meta1_detail }
//                });

//                waveOut.Add(new JObject
//                {
//                     {"header", hearder_detail },
//                    { "data", vgrdJson},
//                    {"meta", meta1_detail }
//                });
//                var windJson = JsonConvert.SerializeObject(waveOut, Formatting.None);

//                //var lastDate = new JObject();
//                //var lastDateJson = JsonConvert.SerializeObject(lastDate, Formatting.None);

//                blobFullPath = Path.Combine(year, month, day, "wind-" + hour + "00.json");
//                blockBlob = container.GetBlockBlobReference(blobFullPath);
//                blockBlob.UploadText(windJson);
//            }
//        }

//        public static void Nww3ForcastInsert(string year, string month, string day, string hour, string path, string fileName, string localFilePath, TraceWriter log)
//        {
//            log.Info($"forcast Strars");
//            var oceanModel = new OCEAN_MODEL();
//            int elementCount = 19;
//            int elementTotalCount = 0;
//            int num = 0;
//            oceanModel.Database.ExecuteSqlCommand("TRUNCATE TABLE [" + "NWW3_FORECAST" + "]");
//            oceanModel.SaveChanges();

//            var UTC = new DateTime();
//            var LAT = new double[231120];
//            var LON = new double[231120];
//            var ICEC = new double[231120];
//            var SWDIR_SEQ1 = new double[231120];
//            var SWDIR_SEQ2 = new double[231120];
//            var WVDIR = new double[231120];
//            var MWSPER = new double[231120];
//            var SWPER_SEQ1 = new double[231120];
//            var SWPER_SEQ2 = new double[231120];
//            var MVPER = new double[231120];
//            var DIRPW = new double[231120];
//            var PWERPW = new double[231120];
//            var DIRSW = new double[231120];
//            var PWERSW = new double[231120];
//            var HTSGW = new double[231120];
//            var SWELL_SEQ1 = new double[231120];
//            var SWELL_SEQ2 = new double[231120];
//            var WVHGT = new double[231120];
//            var UGRD = new double[231120];
//            var VGRD = new double[231120];
//            var WDIR = new double[231120];
//            var WIND = new double[231120];
//            var MWSDIR = new double[231120];

//            GribEnvironment.Init();
//            List<GribMessage> Nww3 = new List<GribMessage>();

//            try
//            {
//                using (GribFile gribFile = new GribFile(@localFilePath))
//                {
//                    gribFile.Context.EnableMultipleFieldMessages = true;
//                    elementTotalCount = elementCount * 81;
//                    foreach (GribMessage item in gribFile)
//                    {
//                        Nww3.Add(item);
//                    }
//                }
//                List<NWW3_FORECAST> waveForecastDataout = new List<NWW3_FORECAST> { };

//                for (int i = 0; i < elementTotalCount; i += elementCount * 8)
//                {
//                    UTC = Nww3.ElementAt(0 + i).Time;
//                    LAT = Nww3.ElementAt(0 + i).GridCoordinateValues.Select(d => d.Latitude).ToArray();
//                    LON = Nww3.ElementAt(0 + i).GridCoordinateValues.Select(d => d.Longitude).ToArray();
//                    Nww3.ElementAt(4 + i).Values(out ICEC);
//                    Nww3.ElementAt(17 + i).Values(out SWDIR_SEQ1);
//                    Nww3.ElementAt(18 + i).Values(out SWDIR_SEQ2);
//                    Nww3.ElementAt(16 + i).Values(out WVDIR);
//                    Nww3.ElementAt(6 + i).Values(out MWSPER);
//                    Nww3.ElementAt(14 + i).Values(out SWPER_SEQ1);
//                    Nww3.ElementAt(15 + i).Values(out SWPER_SEQ2);
//                    Nww3.ElementAt(13 + i).Values(out MVPER);
//                    Nww3.ElementAt(9 + i).Values(out DIRPW);
//                    Nww3.ElementAt(7 + i).Values(out PWERPW);
//                    Nww3.ElementAt(5 + i).Values(out HTSGW);
//                    Nww3.ElementAt(11 + i).Values(out SWELL_SEQ1);
//                    Nww3.ElementAt(12 + i).Values(out SWELL_SEQ2);
//                    Nww3.ElementAt(10 + i).Values(out WVHGT);
//                    Nww3.ElementAt(2 + i).Values(out UGRD);
//                    Nww3.ElementAt(3 + i).Values(out VGRD);
//                    Nww3.ElementAt(1 + i).Values(out WDIR);
//                    Nww3.ElementAt(0 + i).Values(out WIND);
//                    Nww3.ElementAt(8 + i).Values(out MWSDIR);
//                    num++;

//                    for (int j = 0; j < LAT.Length; j++)
//                    {
//                        waveForecastDataout.Add(new NWW3_FORECAST
//                        {
//                            UTC = UTC,
//                            LAT = Convert.ToDecimal(LAT[j]),
//                            LON = Convert.ToDecimal(LON[j]),
//                            SWDIR = Convert.ToSingle(SWDIR_SEQ1[j]),
//                            WVDIR = Convert.ToSingle(WVDIR[j]),
//                            MWSPER = Convert.ToSingle(MWSPER[j]),
//                            SWPER = Convert.ToSingle(SWPER_SEQ1[j]),
//                            WVPER = Convert.ToSingle(MVPER[j]),
//                            HTSGW = Convert.ToSingle(HTSGW[j]),
//                            SWELL = Convert.ToSingle(SWELL_SEQ1[j]),
//                            WVHGT = Convert.ToSingle(WVHGT[j]),
//                            UGRD = Convert.ToSingle(UGRD[j]),
//                            VGRD = Convert.ToSingle(VGRD[j]),
//                            MWSDIR = Convert.ToSingle(MWSDIR[j])
//                        });
//                    }
//                    var waveForcastSplit = Split(waveForecastDataout, 10000);

//                    var length = waveForcastSplit.Count;
//                    int index = 0;

//                    foreach (var item in waveForcastSplit)
//                    {
//                        EFBatchOperation.For(oceanModel, oceanModel.NWW3_FORECAST).InsertAll(item);
//                        Thread.Sleep(10);
//                        var progress = Math.Round((double)index / 0.23, 2);
//                        log.Info($"Wave Forcast Db insert :{item[0].UTC.ToString("yyyy-MM-dd HH:mm:00")} / {progress} %");
//                        index++;
//                    }
//                    waveForcastSplit.Clear();
//                    waveForecastDataout.Clear();
//                }
//            }
//            catch (Exception e)
//            {
//                log.Info($"error!!");
//                log.Info($"{e.ToString()}");
//            }
//        }

//        private static List<List<T>> Split<T>(List<T> collection, int size)
//        {
//            var chunks = new List<List<T>>();
//            var chunkCount = collection.Count / size;

//            if (collection.Count % size > 0)
//                chunkCount++;

//            for (var i = 0; i < chunkCount; i++)
//                chunks.Add(collection.Skip(i * size).Take(size).ToList());

//            return chunks;
//        }

//        private static async Task MainAsync()
//        {
//            Console.WriteLine("Sample start: {0}", DateTime.Now);
//            Console.WriteLine();
//            Stopwatch timer = new Stopwatch();
//            timer.Start();

//            // Construct the Storage account connection string
//            string storageConnectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
//                                                            StorageAccountName, StorageAccountKey);

//            // Retrieve the storage account
//            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);

//            // Create the blob client, for use in obtaining references to blob storage containers
//            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

//            // Use the blob client to create the containers in Azure Storage if they don't yet exist
//            const string appContainerName = "application";
//            const string inputContainerName = "input";
//            const string outputContainerName = "output";
//            await CreateContainerIfNotExistAsync(blobClient, appContainerName);
//            await CreateContainerIfNotExistAsync(blobClient, inputContainerName);
//            await CreateContainerIfNotExistAsync(blobClient, outputContainerName);
//        }

//        /// <summary>
//        /// Creates a container with the specified name in Blob storage, unless a container with that name already exists.
//        /// </summary>
//        /// <param name="blobClient">A <see cref="Microsoft.WindowsAzure.Storage.Blob.CloudBlobClient"/>.</param>
//        /// <param name="containerName">The name for the new container.</param>
//        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
//        private static async Task CreateContainerIfNotExistAsync(CloudBlobClient blobClient, string containerName)
//        {
//            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

//            if (await container.CreateIfNotExistsAsync())
//            {
//                Console.WriteLine("Container [{0}] created.", containerName);
//            }
//            else
//            {
//                Console.WriteLine("Container [{0}] exists, skipping creation.", containerName);
//            }
//        }
//    }
//}