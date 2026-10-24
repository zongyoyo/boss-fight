namespace BossFight.Player
{
    /// <summary>
    /// 要用玩家的元件實作這個介面，由場景裡的 PlayerSpawner 在生成角色後呼叫（T18）。
    /// spawner 的 Awake 最早跑，所以第一次呼叫時接收者自己的 Awake／OnEnable 都還沒跑：這裡只記下引用，照原本的規則在 OnEnable 訂閱。
    /// 換角色（休息區）時會再呼叫一次：元件已經訂閱了就先退訂舊角色、再訂閱新角色。
    /// </summary>
    public interface IPlayerBinding
    {
        void BindPlayer(PlayerRig rig);
    }
}
