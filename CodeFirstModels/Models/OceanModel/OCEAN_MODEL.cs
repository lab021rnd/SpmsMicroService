namespace CodeFirstModels.Models.OceanModel
{
    using System.Data.Entity;

    public partial class OCEAN_MODEL : DbContext
    {

        private const string DbconnString = "data source=test180219-sqldatabase.database.windows.net;initial catalog=OCEAN_MODEL;persist security info=True;user id=lab021;password=@120bal@;MultipleActiveResultSets=True;App=EntityFramework";

        public OCEAN_MODEL()
            : base(DbconnString)
        {
        }

        public virtual DbSet<GFS> GFS { get; set; }
        public virtual DbSet<GFS_FORECAST> GFS_FORECAST { get; set; }
        public virtual DbSet<GFS_FORECAST_PROXY> GFS_FORECAST_PROXY { get; set; }
        public virtual DbSet<GFS_DB_INSERT_CHECK> GFS_DB_INSERT_CHECK { get; set; }

        public virtual DbSet<NWW3> NWW3 { get; set; }
        public virtual DbSet<NWW3_FORECAST> NWW3_FORECAST { get; set; }
        public virtual DbSet<NWW3_FORECAST_PROXY> NWW3_FORECAST_PROXY { get; set; }
        public virtual DbSet<NWW3_DB_INSERT_CHECK> NWW3_DB_INSERT_CHECK { get; set; }

        public virtual DbSet<RTOFS> RTOFS { get; set; }
        public virtual DbSet<RTOFS_FORECAST> RTOFS_FORECAST { get; set; }
        public virtual DbSet<RTOFS_FORECAST_PROXY> RTOFS_FORECAST_PROXY { get; set; }
        public virtual DbSet<RTOFS_DB_INSERT_CHECK> RTOFS_DB_INSERT_CHECK { get; set; }

        public virtual DbSet<FTP_RAWDATA_DOWNLOAD_CHECK> FTP_RAWDATA_DOWNLOAD_CHECK { get; set; }
        public virtual DbSet<RTOFS_POSITION_CONVERT> RTOFS_POSITION_CONVERT { get; set; }
        public virtual DbSet<OCEAN_DEPTH> OCEAN_DEPTH { get; set; }

        public OCEAN_MODEL(string cs)
          : base(cs)
        {
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
        }
    }
}
