using Accord.MachineLearning.Structures;
using CodeFirstModels.Models.OceanModel;
using EntityFramework.Utilities;
using Microsoft.Research.Science.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InfraHelper
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            MakeLatLonKey();
        }

        public static void MakeLatLonKey()
        {
            var kdTree = MemoryRoad();

            OCEAN_MODEL oceanModel = new OCEAN_MODEL();
            List<RTOFS_POSITION_CONVERT> oceanLatLon = new List<RTOFS_POSITION_CONVERT>();
            kdTree.Distance = Accord.Math.Distance.Euclidean;

            for (int i = 0; i < 360; i++)
            {
                for (int j = 0; j < 720; j++)
                {
                    var lat = 90 - (float)i / 2;
                    var lon = (float)j / 2;
                    var lonForSearch = (float)j / 2 > 74 ? (float)j / 2 : 360 + (float)j / 2;
                    var keygeo = kdTree.Nearest(new double[] { lat, lonForSearch }, neighbors: 400).OrderBy(s => s.Distance).First().Node.Value;
                    var refid = i * 720 + j;

                    oceanLatLon.Add(new RTOFS_POSITION_CONVERT
                    {
                        LAT = (decimal)lat,
                        LON = (decimal)lon,
                        KEY = keygeo
                    });
                }
            }

            using (var oceanMoel = new OCEAN_MODEL())
            {
                oceanMoel.Database.CommandTimeout = 30000;
                EFBatchOperation.For(oceanMoel, oceanMoel.RTOFS_POSITION_CONVERT).InsertAll(oceanLatLon);
                oceanMoel.Dispose();
                oceanLatLon.Clear();
            }
        }


        public static void WriteBinaryFile()
        {
            //using (Stream stream = File.Open(@"d:\positionConvert3.bin", FileMode.Create))
            //{
            //    var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            //    binaryFormatter.Serialize(stream, oceanLatLon);
            //}
        }


        public static void ReadBinaryFile()
        {
            //using (Stream stream = File.Open(@"d:\positionConvert3.bin", FileMode.Open))
            //{
            //    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

            //    List<RTOFS_POSITION_CONVERT> oceanLatLon2 = (List<RTOFS_POSITION_CONVERT>)bformatter.Deserialize(stream);
            //}

        }


        public static KDTree<int> MemoryRoad()
        {
            int lat = 3298;
            int lon = 4500;

            string path = @"F:\data\rtofs_glo_2ds_f024_daily_prog.nc";
            DataSet ocean = DataSet.Open(path);

            var oceanLon = ocean.Variables[5];  //경도
            var oceanLat = ocean.Variables[6];  //위도

            var latArray = oceanLat.GetData();
            var lonArray = oceanLon.GetData();
            var latLonLen = lat * lon;    //전체 길이

            double[][] getLatLon = new double[latLonLen][];
            int[] pointV = new int[latLonLen];

            for (int i = 0; i < lat; i++)
            {
                for (int j = 0; j < lon; j++)
                {
                    int offset = (i * lon) + j;
                    var OceanLat = Convert.ToSingle(latArray.GetValue(i, j));
                    var OceanLon = Convert.ToSingle(lonArray.GetValue(i, j));
                    getLatLon[offset] = new double[] { OceanLat, OceanLon };
                    pointV[offset] = offset;
                }
            }

            KDTree<int> tree = KDTree.FromData<int>(getLatLon, pointV, false);

            return tree;
        }
    }
}