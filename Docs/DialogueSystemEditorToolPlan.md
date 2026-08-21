# 对话系统编辑器工具

## 目标

该工具将 NPC 对话保存为独立 DialogueAsset。场景 NPC 只需要挂载 NpcDialogueInteractor 并绑定资产，因此文本、头像、分支和事件不会堆在场景文件中。

## 配置流程

1. 打开 Tools > Dialogue Editor，点击 Create NPC Dialogue 创建一个独立 NPC 对话资产。
2. 绑定 NPC 名称、头像图片和头像显示侧。头像使用 Unity Sprite，可选择放在对话框左侧或右侧。
3. 点击 Create Presentation Settings，在 Inspector 中配置全局玩家名称、玩家头像和显示侧。该资产位于 Assets/Resources/Dialogue，运行时会自动加载。
4. 使用 Add NPC Line 与 Add Player Line 添加顺序台词。没有选项的末尾台词会自动连接到新台词。
5. 在 NPC 节点的 Player Choices 中添加玩家回答；每条回答的 Target Node Id 指向后续节点，-1 表示该回答结束对话。
6. 填写节点 Event ID 可在台词显示时广播业务事件。完成事件只在首次完成时广播一次。
7. 给 NPC 场景对象添加 NpcDialogueInteractor，将创建的 DialogueAsset 拖入 Dialogue 字段。

## 运行规则

- 玩家进入 NPC 默认 1 米范围会看到“按 E 对话”；手柄南键同样可开始交互。
- 对话打开后，玩家移动和视角输入会锁定，世界时间继续运行。
- NPC 台词的选项会显示为玩家回答；选择后先显示玩家文本，再进入对应目标节点。
- 任意分支走到结束后，资产 ID 会保存到本机进度。之后再次互动只显示 Completion Text。
- 点击继续、鼠标点击选项或 UI 的确认输入可推进；取消输入关闭当前对话。

## 事件接收

在需要响应台词事件的组件中监听 DialogueNodeEvent。其中 EventId 是编辑器填写的稳定字符串，DialogueId 和 NodeId 用于追踪来源。

    using QFramework;
    using UnityEngine;

    public class DialogueQuestBridge : MonoBehaviour, IController
    {
        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private void Awake()
        {
            this.RegisterEvent<DialogueNodeEvent>(OnDialogueNode)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        private void OnDialogueNode(DialogueNodeEvent dialogueEvent)
        {
            if (dialogueEvent.EventId == "quest.blacksmith.start")
                Debug.Log("开始铁匠任务");
        }
    }

## 校验与测试

编辑器会检查入口、空文本、无效跳转、不可达节点和循环引用。发布前至少验证线性对话、分支汇合、结束栏、头像左右显示、事件接收、E/手柄交互，以及重启后仍保留的完成状态。
