namespace CodeFirstModels.Models.OceanModel
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class RTOFS
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
