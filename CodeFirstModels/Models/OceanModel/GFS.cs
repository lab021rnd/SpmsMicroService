namespace CodeFirstModels.Models.OceanModel
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class GFS
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
