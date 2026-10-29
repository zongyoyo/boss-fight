#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
namespace BossFight.DebugTools
{
    /// <summary>有作弊開關（玩家無敵、指定招、小樹精無敵…）的 Debug 工具：關掉開發者模式時 DeveloperMode 呼叫 ClearCheats 全部清掉。</summary>
    public interface IDebugCheats
    {
        void ClearCheats();
    }
}
#endif
