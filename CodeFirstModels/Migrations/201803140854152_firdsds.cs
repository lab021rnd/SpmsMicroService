namespace CodeFirstModels.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class firdsds : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_OF_RTOFS_FORECAST", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.CONDITION_OCEAN_WEATHER", "DATE_LAST_OF_RTOFS_FORECAST");
        }
    }
}
