using System.Threading.Tasks;

namespace QuestPatcher.Core
{
    public interface IUserPrompter
    {
        Task<bool> PromptAppNotInstalled();

        Task<bool> CheckUpdate();
        Task<bool> PromptAdbDisconnect(DisconnectionType type);

        Task<bool> PromptUnstrippedUnityUnavailable();

        Task<bool> PromptFlatScreenWarning();

        Task<bool> Prompt32Bit();

        Task<bool> PromptUnknownModLoader();

        Task PromptUpgradeFromOld();
    }
}
