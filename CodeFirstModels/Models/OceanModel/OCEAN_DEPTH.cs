namespace CodeFirstModels.Models.OceanModel
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class OCEAN_DEPTH
    {
        public long ID { get; set; }
        [Index("LAT", IsClustered = true)]
        public decimal LAT { get; set; }
        [Index("LON")]
        public decimal LON { get; set; }

        public float DEPTH { get; set; }
    }
}
