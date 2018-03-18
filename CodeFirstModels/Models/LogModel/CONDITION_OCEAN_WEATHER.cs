using System;

namespace CodeFirstModels.Models.LogModel
{
    public class CONDITION_OCEAN_WEATHER
    {
        public int ID { get; set; }

        public DateTime DATE_LAST_OF_NWW3 { get; set; }
        public DateTime DATE_LAST_OF_NWW3_JSON { get; set; }
        public string DB_OF_NWW3_FORECAST { get; set; }

        public DateTime DATE_LAST_OF_RTOFS { get; set; }
        public DateTime DATE_LAST_FILE_OF_RTOFS_FORECAST { get; set; }
        public string FILE_NAME_LAST_OF_RTOFS_FORECAST { get; set; }
        public DateTime DATE_LAST_OF_RTOFS_JSON { get; set; }
        public string DB_OF_RTOFS_FORECAST { get; set; }

        public DateTime DATE_LAST_OF_GFS { get; set; }

        public DateTime DATE_LAST_FILE_OF_GFS_FORECAST { get; set; }
        public string FILE_NAME_LAST_OF_GFS_FORECAST { get; set; }
        public DateTime DATE_LAST_OF_GFS_JSON { get; set; }
        public string DB_OF_GFS_FORECAST { get; set; }
    }
}