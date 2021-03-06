﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeFirstModels.Models.OceanModel
{
    public class GFS_FORECAST_PROXY
    {
        public long ID { get; set; }
        [Index("UTC", IsClustered = true)]
        [Column(TypeName = "smalldatetime")]
        public DateTime UTC { get; set; }
        [Index("LAT")]
        public decimal LAT { get; set; }

        public decimal LON { get; set; }

        public float TEMP_SURFACE { get; set; }

        public float TEMP_ABOVE_GROUND { get; set; }

        public float PRESSURE_SURFACE { get; set; }

        public float PRESSURE_MSL { get; set; }
    }
}
