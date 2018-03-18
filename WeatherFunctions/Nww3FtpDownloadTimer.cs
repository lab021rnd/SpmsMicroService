using CodeFirstModels.Models;
using CodeFirstModels.Models.LogModel;
using CodeFirstModels.Models.OceanModel;
using FluentFTP;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace WeatherFunctions
{
    public static class Nww3FtpDownloadTimer
    {
        private static OCEAN_MODEL _oceanModel;
        private static LOG_MODEL _logModel;

        private static string _storageAccountName = CloudConn.StorageAccountName_OceanRaw;
        private static string _storageAccountKey = CloudConn.StorageAccountKey_OceanRaw;
        private static string _storageConnectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", _storageAccountName, _storageAccountKey);
        private static CloudStorageAccount _storageAccount = CloudStorageAccount.Parse(_storageConnectionString);
        private static CloudBlobClient _blobClient = _storageAccount.CreateCloudBlobClient();

        private static string _ftpAccountId = CloudConn.FtpAccountId_Nww3;
        private static string _ftpAccountPw = CloudConn.FtpAccountPw_Nww3;

        private static bool _functionIsRunningOrNot = false;
        private static int _dayToCheckForDownload = 7;

        private static List<string[]> _fileListToNeedDownload;

        private static TraceWriter _log;

        [FunctionName("Nww3RawDownloadTimer")]
        public static void Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            _oceanModel = new OCEAN_MODEL();
            _logModel = new LOG_MODEL();
            _log = log;
            _fileListToNeedDownload = new List<string[]>();
            // function 다른 instance가 동작하고 있으면 이중으로 실행하지 않도록 작동 중지.
            if (_functionIsRunningOrNot == true)
            {
                _log.Info($"Other Instance is Running at: {DateTime.Now}");
                return;
            }

            try
            {
                _functionIsRunningOrNot = true;
                CheckToGetWaveFile();
                FtpDownLoad();
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

        private static void CheckToGetWaveFile()
        {
            //Wave 파일 중 받아야 되는 파일 목록 생성
            var checkData = DateTime.UtcNow.AddDays(_dayToCheckForDownload * -1);
            checkData = new DateTime(checkData.Year, checkData.Month, checkData.Day, 0, 0, 0);

            for (int i = 0; i < _dayToCheckForDownload + 1; i++)
            {
                var tempyyyyMMdd = checkData.AddDays(i).ToString("yyyyMMdd");
                var year = checkData.AddDays(i).ToString("yyyy");
                var month = checkData.AddDays(i).ToString("MM");
                var day = checkData.AddDays(i).ToString("dd");

                for (int j = 0; j < 4; j++)
                {
                    var hour = (j * 6).ToString("00");
                    var tempyyyyMMddhhmm = checkData.AddDays(i);

                    var tempFileName = "gwes00.glo_30m.t" + hour + "z.grib2";
                    var filePath = "/pub/data/nccf/com/wave/prod/gwes." + tempyyyyMMdd + "/" + tempFileName;
                    var isExistWaveFile = _oceanModel.FTP_RAWDATA_DOWNLOAD_CHECK.Where(d => d.DATE_OF_FILE == tempyyyyMMddhhmm && d.FILE_PATH == tempFileName).Count();
                    if (isExistWaveFile == 0)
                    {
                        _fileListToNeedDownload.Add(new string[] { year, month, day, hour, filePath, tempFileName });
                    }
                }
            }
        }

        private static void FtpDownLoad()
        {
            // Retrieve the storage account
            CloudBlobContainer container = _blobClient.GetContainerReference("nww3");

            // Ftp Check and Download
            using (FtpClient conn = new FtpClient())
            {
                conn.SocketPollInterval = 1000;
                conn.ConnectTimeout = 30000;
                conn.ReadTimeout = 300000;
                conn.DataConnectionConnectTimeout = 300000;
                conn.DataConnectionReadTimeout = 300000;

                conn.Host = "ftp.ncep.noaa.gov";
                conn.Credentials = new NetworkCredential(_ftpAccountId, _ftpAccountPw);
                conn.Connect();

                foreach (var item in _fileListToNeedDownload)
                {
                    var checkPoint = 0;

                    if (conn.FileExists(item[4]))
                    {
                        var ftpNww3DownloadCheck = new FTP_RAWDATA_DOWNLOAD_CHECK();
                        var oceanJobList = new JOB_LIST_OCEAN_WEATHER();
                        var blobFullPath = Path.Combine(item[0], item[1], item[2], item[5]);
                        Progress<double> progress = new Progress<double>(x =>
                        {
                            // When progress in unknown, -1 will be sent
                            if (x < 0)
                            {
                            }
                            if (x > checkPoint)
                            {
                                checkPoint = checkPoint + 10;

                                _log.Info($"{item[0]}-{item[1]}-{item[2]} {item[3]}:00 / {item[5]}   Progres : {Math.Round(x, 0)} % ");
                            }
                        });

                        conn.RetryAttempts = 3;
                        ftpNww3DownloadCheck.DATE_OF_FILE = new DateTime(Convert.ToInt32(item[0]), Convert.ToInt32(item[1]), Convert.ToInt32(item[2]), 0, 0, 0);
                        ftpNww3DownloadCheck.FILE_TYPE = "NWW3";
                        ftpNww3DownloadCheck.FILE_PATH = item[5];
                        ftpNww3DownloadCheck.TIME_DOWNLOADED = DateTime.UtcNow;

                        MemoryStream streamFmFtp = new MemoryStream();
                        conn.Download(streamFmFtp, item[4], progress);

                        _log.Info($"{item[0]}-{item[1]}-{item[2]} {item[3]}:00 / {item[5]}   Progres : Complete");
                        _log.Info($"========================================================================================================");

                        CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobFullPath);
                        streamFmFtp.Position = 0;
                        using (var fileStream = streamFmFtp)
                        {
                            blockBlob.UploadFromStream(streamFmFtp);
                        }
                        _oceanModel.FTP_RAWDATA_DOWNLOAD_CHECK.Add(ftpNww3DownloadCheck);
                        _oceanModel.SaveChanges();

                        oceanJobList.DATE_OF_JOB_TRIGGERED = DateTime.UtcNow; //job을 등록한 시각
                        oceanJobList.JOB_TYPE = "NWW3_DECODE"; //job 종류
                        oceanJobList.DATE_OF_COMPLETE_JOB_TIME = new DateTime(2000, 01, 01, 0, 0, 0); //job 완료시간
                        oceanJobList.IS_COMPLETE_JOB = false; // job 수행여부
                        oceanJobList.REASON_OF_NOT_COMPLETE = "not started"; // job이 수행되지 않은 이유

                        oceanJobList.FILE_PATH = blobFullPath; // blobpath
                        _logModel.JOB_LIST_OCEAN_WEATHER.Add(oceanJobList);
                        _logModel.SaveChanges();
                    }
                }
                conn.Disconnect();
            }
        }

        private static string GetContainerSasUrl(CloudBlobClient blobClient, string containerName, SharedAccessBlobPermissions permissions)
        {
            // Set the expiry time and permissions for the container access signature. In this case, no start time is specified,
            // so the shared access signature becomes valid immediately
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(2),
                Permissions = permissions
            };

            // Generate the shared access signature on the container, setting the constraints directly on the signature
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            string sasContainerToken = container.GetSharedAccessSignature(sasConstraints);

            // Return the URL string for the container, including the SAS token
            return String.Format("{0}{1}", container.Uri, sasContainerToken);
        }
    }
}