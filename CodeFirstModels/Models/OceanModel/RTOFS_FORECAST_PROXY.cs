using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeFirstModels.Models.OceanModel
{
    public class RTOFS_FORECAST_PROXY
    {
        public long ID { get; set; }
        [Index("UTC", IsClustered = true)]
        [Column(TypeName = "smalldatetime")]

        public DateTime UTC { get; set; }
        [Index("LAT")]
        public decimal LAT { get; set; }

        public decimal LON { get; set; }

        public float SSS { get; set; }

        public float SST { get; set; }

        public float ICE_THICKNESS { get; set; }

        public float CURRENT_UV { get; set; }

        public float CURRENT_VV { get; set; }
    }
}
