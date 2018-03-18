using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace CodeFirstModels.Models.LogModel
{
    public class JOB_LIST_OCEAN_WEATHER
    {
        public long ID { get; set; }

        public string JOB_TYPE { get; set; }

        [Index("dateOfJobTriggered", IsClustered = true)]
        public DateTime DATE_OF_JOB_TRIGGERED { get; set; }

        [Index("isCompleteJob")]
        public bool IS_COMPLETE_JOB { get; set; }

        public DateTime DATE_OF_COMPLETE_JOB_TIME { get; set; }

        public string FILE_PATH { get; set; }

        public string REASON_OF_NOT_COMPLETE { get; set; }
    }
}