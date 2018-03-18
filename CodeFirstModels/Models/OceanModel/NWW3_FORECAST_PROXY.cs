using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace CodeFirstModels.Models.OceanModel
{
    public class NWW3_FORECAST_PROXY
    {
        public long ID { get; set; }

        [Index("UTC", IsClustered = true)]
        [Column(TypeName = "smalldatetime")]
        public DateTime UTC { get; set; }

        [Index("LAT")]
        public decimal LAT { get; set; }

        public decimal LON { get; set; }
        public float HTSGW { get; set; }
        public float MWSDIR { get; set; }
        public float MWSPER { get; set; }
        public float SWELL { get; set; }
        public float SWDIR { get; set; }
        public float SWPER { get; set; }
        public float WVHGT { get; set; }
        public float WVDIR { get; set; }
        public float WVPER { get; set; }
        public float UGRD { get; set; }
        public float VGRD { get; set; }
    }
}