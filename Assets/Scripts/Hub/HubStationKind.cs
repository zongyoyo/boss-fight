namespace BossFight.Hub
{
    /// <summary>休息區的四個站點（T20）。資產存數字，新增時往後加。</summary>
    public enum HubStationKind
    {
        /// <summary>中心小屋的村長：對話、任務（T24）。</summary>
        Chief,
        /// <summary>鐵匠鋪的鐵砧：裝備（T22）。</summary>
        Blacksmith,
        /// <summary>營火：角色、技能樹（T21、T23）。</summary>
        Campfire,
        /// <summary>傳送門：挑戰清單、出發。</summary>
        Portal,
    }
}
