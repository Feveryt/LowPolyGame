using UnityEngine;

/// <summary>
/// 输入管理器
/// 职责：封装 Input System / Input Manager，统一提供输入接口，支持键鼠和手柄
/// </summary>
public class InputManager : MonoBehaviour
{
    // 移动输入：Vector2 Move（WASD / 左摇杆）
    // 视角旋转：Vector2 Look（鼠标 / 右摇杆）
    // 攻击键：Attack（左键 / RT / X键）
    // 技能键：Skill1~Skill4（1~4 / LB+按键）
    // 闪避键：Dodge（空格 / A键）
    // 交互键：Interact（E / Y键）
    // 跳跃键：Jump（Space / A键）
    // 锁定目标：LockOn（中键 / 右摇杆按下）
    // 暂停：Pause（Esc / Start）
    // 是否为手柄模式

    // 输入屏蔽标志（过场/对话时禁止移动输入）
    // 振动反馈：SetVibration(float leftMotor, float rightMotor, float duration)
}
