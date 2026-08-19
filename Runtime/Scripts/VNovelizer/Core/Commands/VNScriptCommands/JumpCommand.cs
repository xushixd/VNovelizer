using UnityEngine;

namespace VNovelizer.Core.Commands
{
    public class JumpCommand : VNCommand
    {
        public override string CommandName { get { return "jump"; } }

        public override bool Execute(string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                Debug.LogError("Jump命令参数不能为空");
                return false;
            }

            string targetID = args.Trim();
            VNManager manager = VNManager.GetInstance();
            if (manager.JumpToContentOrLine(targetID))
                return true;

            Debug.LogError($"[JumpCommand] 未找到指定的 Content/行 ID: {targetID}");
            return false;
        }
    }
}