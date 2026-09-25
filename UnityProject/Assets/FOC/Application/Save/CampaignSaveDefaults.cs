using System.Collections.Generic;

namespace FOC.Application.Save
{
    public static class CampaignSaveDefaults
    {
        public static SaveMigrationPipeline CreateMigrationPipeline() => new SaveMigrationPipeline(new ISaveMigration[]
        {
            new CampaignSaveV1ToV2Migration(),
            new CampaignSaveV2ToV3Migration(),
            new CampaignSaveV3ToV4Migration(),
            new CampaignSaveV4ToV5Migration(),
            new CampaignSaveV5ToV6Migration(),
            new CampaignSaveV6ToV7Migration(),
            new CampaignSaveV7ToV8Migration(),
            new CampaignSaveV8ToV9Migration(),
            new CampaignSaveV9ToV10Migration(),
            new CampaignSaveV10ToV11Migration(),
            new CampaignSaveV11ToV12Migration(),
            new CampaignSaveV12ToV13Migration(),
            new CampaignSaveV13ToV14Migration(),
        });

        public static IReadOnlyList<ISaveMigration> OrderedMigrations => new ISaveMigration[]
        {
            new CampaignSaveV1ToV2Migration(), new CampaignSaveV2ToV3Migration(), new CampaignSaveV3ToV4Migration(),
            new CampaignSaveV4ToV5Migration(), new CampaignSaveV5ToV6Migration(), new CampaignSaveV6ToV7Migration(),
            new CampaignSaveV7ToV8Migration(), new CampaignSaveV8ToV9Migration(), new CampaignSaveV9ToV10Migration(),
            new CampaignSaveV10ToV11Migration(), new CampaignSaveV11ToV12Migration(), new CampaignSaveV12ToV13Migration(), new CampaignSaveV13ToV14Migration(),
        };
    }
}
