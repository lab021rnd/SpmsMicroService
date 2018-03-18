namespace CodeFirstModels.Models.OceanModel
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class FTP_RAWDATA_DOWNLOAD_CHECK
    {
        public long ID { get; set; }
        [Index("UTC")]
        public DateTime DATE_OF_FILE { get; set; }

        public string FILE_TYPE { get; set; }

        public DateTime TIME_DOWNLOADED { get; set; }

        public string FILE_PATH { get; set; }
    }
}
