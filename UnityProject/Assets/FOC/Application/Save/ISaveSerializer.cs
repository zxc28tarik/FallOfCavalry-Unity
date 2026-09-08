namespace FOC.Application.Save
{
    public interface ISaveSerializer
    {
        string Serialize(CampaignSaveData data);

        SaveReadResult Deserialize(string content);
    }
}

