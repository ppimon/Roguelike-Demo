using UnityEngine;
using Spine.Unity;

/// <summary>
/// Boss 肢体代理器：负责转发伤害给大脑，并根据大脑的状态（破防、恢复、死亡）播放对应的表现动画。
/// </summary>
public class BossBodyPart : MonoBehaviour, IDamageable
{
    public enum PartType { Head, LeftHand, RightHand }

    [Header("部位设置")]
    public PartType myType;
    public BossStats bossBrain;      // 指向父节点的 Boss 大脑
    public Collider2D partCollider;  // 本部位的受击判定框
    public SkeletonAnimation spineAnim;

    [Header("战斗状态")]
    public bool isInvincible = false;// 无敌状态标记

    [Header("动画名称")]
    public string idleAnim = "Idle";
    public string brokenAnim = "Broken";
    public string recoverAnim = "Recover";
    public string dieAnim = "Die";   // 【新增】死亡动画名称

    void Start()
    {
        // 确保启动时引用不为空
        if (bossBrain == null) bossBrain = GetComponentInParent<BossStats>();
        if (partCollider == null) partCollider = GetComponent<Collider2D>();
        if (spineAnim == null) spineAnim = GetComponent<SkeletonAnimation>();

        // 订阅 Boss 大脑的事件
        if (bossBrain != null)
        {
            bossBrain.OnBroken += HandleBossBroken;
            bossBrain.OnRecover += HandleBossRecover;
            bossBrain.OnDeath += HandleBossDeath; // 【新增】订阅大脑的死亡事件
        }

        // 初始化：头部默认无敌
        if (myType == PartType.Head && partCollider != null)
        {
            partCollider.enabled = false;
        }
    }

    void OnDestroy()
    {
        // 安全取消订阅
        if (bossBrain != null)
        {
            bossBrain.OnBroken -= HandleBossBroken;
            bossBrain.OnRecover -= HandleBossRecover;
            bossBrain.OnDeath -= HandleBossDeath;
        }
    }

    // --- 核心：伤害转发 ---
    public void TakeDamage(AttackImpact impact)
    {
        if (isInvincible) return; // 如果是无敌状态，直接无视伤害

        if (bossBrain != null)
        {
            bossBrain.TakeDamage(impact);
        }
    }

    // --- 机制联动：当大脑宣布韧性归零时 ---
    void HandleBossBroken()
    {
        if (myType == PartType.Head)
        {
            // 头部：开启碰撞体，允许被玩家暴打，掉落动画只播一次
            if (partCollider != null) partCollider.enabled = true;
            PlayAnim(brokenAnim, false);
        }
        else
        {
            // 【修复】左右手：播放一次瘫痪/抽搐动画，然后自动排队切回 Idle
            spineAnim.AnimationState.SetAnimation(0, brokenAnim, false);
            spineAnim.AnimationState.AddAnimation(0, idleAnim, true, 0f);
        }
    }

    // --- 机制联动：当大脑宣布韧性恢复时 ---
    void HandleBossRecover()
    {
        if (myType == PartType.Head)
        {
            // 头部：关闭碰撞体重新无敌，播放升空恢复动画
            if (partCollider != null) partCollider.enabled = false;
            PlayAnim(recoverAnim, false);
        }
        else
        {
            // 左右手：主动恢复待机
            PlayAnim(idleAnim, true);
        }
    }

    // --- 【新增】机制联动：当大脑宣布死亡时 ---
    void HandleBossDeath()
    {
        // 1. 关闭判定框，防止死尸还能被打出特效
        if (partCollider != null) partCollider.enabled = false;

        // 2. 强行“拔电源”：关闭 AI 脚本！
        // 这样可以彻底阻止 AI 继续在后台索敌、读秒，防止它们把死亡动画顶替掉
        var headAI = GetComponent<BossAI_Head>();
        if (headAI != null) headAI.enabled = false;

        var clawAI = GetComponent<BossAI_Claw>();
        if (clawAI != null) clawAI.enabled = false;

        // 3. 播放死亡动画 (不循环)
        PlayAnim(dieAnim, false);
    }

    // 辅助播放动画
    void PlayAnim(string animName, bool loop)
    {
        if (spineAnim != null && !string.IsNullOrEmpty(animName))
        {
            spineAnim.AnimationState.SetAnimation(0, animName, loop);
        }
    }
}