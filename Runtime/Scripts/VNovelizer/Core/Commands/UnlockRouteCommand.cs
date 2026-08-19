using UnityEngine;

namespace VNovelizer.Core.Commands
{
    /// <summary>
    /// 解锁路线图节点。
    /// </summary>
    public class UnlockRouteCommand : VNCommand
    {
        public override string CommandName { get { return "unlockroute"; } }

        public override bool Execute(string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                Debug.LogError("UnlockRoute 命令参数不能为空");
                return false;
            }

            GlobalDataManager.GetInstance().UnlockRoute(args.Trim());
            return true;
        }
    }
}
