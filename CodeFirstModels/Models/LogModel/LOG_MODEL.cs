namespace CodeFirstModels.Models.LogModel
{
    using System.Data.Entity;

    public partial class LOG_MODEL : DbContext
    {
        
        private const string DbconnString = "data source=test180219-sqldatabase.database.windows.net;initial catalog=LOG_MODEL;persist security info=True;user id=lab021;password=@120bal@;MultipleActiveResultSets=True;App=EntityFramework";

        public LOG_MODEL()
            : base(DbconnString)
        {
        }

        public virtual DbSet<JOB_LIST_OCEAN_WEATHER> JOB_LIST_OCEAN_WEATHER { get; set; }
        public virtual DbSet<CONDITION_OCEAN_WEATHER> CONDITION_OCEAN_WEATHER { get; set; }
        
        public LOG_MODEL(string cs)
          : base(cs)
        {
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
        }
    }
}
