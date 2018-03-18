namespace CodeFirstModels.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class fsdf : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_OF_GFS_FORECAST", c => c.DateTime(nullable: false));
            AddColumn("dbo.CONDITION_OCEAN_WEATHER", "FILE_NAME_LAST_OF_GFS_FORECAST", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.CONDITION_OCEAN_WEATHER", "FILE_NAME_LAST_OF_GFS_FORECAST");
            DropColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_OF_GFS_FORECAST");
        }
    }
}
