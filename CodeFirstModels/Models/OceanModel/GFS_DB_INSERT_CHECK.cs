namespace CodeFirstModels.Models.OceanModel
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class GFS_DB_INSERT_CHECK
    {
        public long ID { get; set; }
        [Index("UTC")]
        public DateTime DATE_OF_FILE { get; set; }

        public string FILE_NAME { get; set; }

        public DateTime DATE_INSERTED { get; set; }
    }
}
