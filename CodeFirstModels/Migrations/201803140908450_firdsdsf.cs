namespace CodeFirstModels.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class firdsdsf : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.CONDITION_OCEAN_WEATHER", "FILE_NAME_LAST_OF_RTOFS_FORECAST", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.CONDITION_OCEAN_WEATHER", "FILE_NAME_LAST_OF_RTOFS_FORECAST");
        }
    }
}
