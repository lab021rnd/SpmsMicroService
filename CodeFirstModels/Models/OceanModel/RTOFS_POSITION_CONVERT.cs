using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeFirstModels.Models.OceanModel
{
    [Serializable]
    public class RTOFS_POSITION_CONVERT
    {
        public int ID { get; set; }
        public decimal LAT { get; set; }
        public decimal LON { get; set; }
        public int KEY { get; set; }
    }
}
