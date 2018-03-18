namespace CodeFirstModels.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class fsdfsss : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_FILE_OF_RTOFS_FORECAST", c => c.DateTime(nullable: false));
            AddColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_FILE_OF_GFS_FORECAST", c => c.DateTime(nullable: false));
            DropColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_OF_RTOFS_FORECAST");
            DropColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_OF_GFS_FORECAST");
        }
        
        public override void Down()
        {
            AddColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_OF_GFS_FORECAST", c => c.DateTime(nullable: false));
            AddColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_OF_RTOFS_FORECAST", c => c.DateTime(nullable: false));
            DropColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_FILE_OF_GFS_FORECAST");
            DropColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_FILE_OF_RTOFS_FORECAST");
        }
    }
}
