namespace FOC.Application.Save
{
    public interface ISaveMigration
    {
        int FromVersion { get; }

        int ToVersion { get; }

        CampaignSaveData Apply(CampaignSaveData source);
    }
}

