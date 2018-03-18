namespace CodeFirstModels.Models.Lab021Model
{
    using System.Data.Entity;

    public partial class LAB021_MODEL : DbContext
    {
        private const string DbconnString = "data source=218.39.195.13,21000;initial catalog=ETA_ALARM;persist security info=True;user id=sa;password=@120bal@;MultipleActiveResultSets=True;App=EntityFramework";

        public LAB021_MODEL()
            : base(DbconnString)
        {
        }

        public virtual DbSet<ETA_REPORT> ETA_REPORT { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
        }
    }
}