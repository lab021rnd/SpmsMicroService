namespace CodeFirstModels.Models.Lab021Model
{
    using System;

    public partial class ETA_REPORT
    {
        public int ID { get; set; }
        public string SHIP_NAME { get; set; }

        public DateTime REPORT_TIME { get; set; }

        public DateTime ETA_TIME { get; set; }

        public string ETA_PORT { get; set; }

        public string NATION { get; set; }
    }
}