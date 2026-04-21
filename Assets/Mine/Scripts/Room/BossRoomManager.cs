using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;

public class BossRoomManager : MonoBehaviour
{
    [Header("Boss 房配置")]
    public Room room;                 // 自身的 Room 组件引用
    public GameObject bossPrefab;     // Boss的预制体
    public Transform bossSpawnPoint;  // Boss生成的指定中心点
    public BoxCollider2D triggerArea; // 触发Boss战的区域

    private GameObject activeBoss;
    private bool encounterStarted = false;

    private void Start()
    {
        if (triggerArea != null) triggerArea.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!encounterStarted && collision.CompareTag("Player"))
        {
            encounterStarted = true;
            StartCoroutine(BossEncounterSequence());
        }
    }

    IEnumerator BossEncounterSequence()
    {
        // 1. 锁上所有的门
        foreach (var exit in room.exits)
        {
            if (exit.doorObject != null)
            {
                var door = exit.doorObject.GetComponent<SlidingDoor>();
                if (door != null) door.SetLock(true);
            }
        }

        // 2. 在指定坐标生成 Boss
        activeBoss = Instantiate(bossPrefab, bossSpawnPoint.position, Quaternion.identity, transform);

        // 获取所有肢体代理组件
        var bodyParts = activeBoss.GetComponentsInChildren<BossBodyPart>();

        // 演出开始：全肢体进入无敌状态
        foreach (var part in bodyParts)
        {
            part.isInvincible = true;
        }

        // 3. 获取所有部位的 Spine 动画与 AI 组件
        var allAnims = activeBoss.GetComponentsInChildren<SkeletonAnimation>();
        var headAI = activeBoss.GetComponentInChildren<BossAI_Head>();
        var clawAIs = activeBoss.GetComponentsInChildren<BossAI_Claw>();

        // 确保一开始 AI 是锁定的
        if (headAI != null) headAI.isIntroFinished = false;
        foreach (var claw in clawAIs) claw.isIntroFinished = false;

        // 4. 所有部位强制播放 Start 动画，并获取最长动画的时间
        float maxAnimDuration = 0f;
        foreach (var anim in allAnims)
        {
            var track = anim.AnimationState.SetAnimation(0, "Start", false);
            if (track != null && track.Animation.Duration > maxAnimDuration)
            {
                maxAnimDuration = track.Animation.Duration;
            }
        }

        // 5. 等待入场演出播完
        yield return new WaitForSeconds(maxAnimDuration);

        // 6. 演出结束：所有部位切回 Idle，并正式激活 AI 开始战斗！
        foreach (var anim in allAnims)
        {
            anim.AnimationState.SetAnimation(0, "Idle", true);
        }

        // 演出结束：解除无敌状态
        foreach (var part in bodyParts)
        {
            part.isInvincible = false;
        }

        if (headAI != null) headAI.isIntroFinished = true;
        foreach (var claw in clawAIs) claw.isIntroFinished = true;

        // 7. 监听 Boss 死亡事件以开启房间
        var bossStats = activeBoss.GetComponentInChildren<BossStats>();
        if (bossStats != null)
        {
            bossStats.OnDeath += OnBossDefeated;
        }
    }

    void OnBossDefeated()
    {
        Debug.Log("<color=yellow>Boss已被击败！房间门解锁。</color>");

        // 解锁所有的门
        foreach (var exit in room.exits)
        {
            if (exit.doorObject != null)
            {
                var door = exit.doorObject.GetComponent<SlidingDoor>();
                if (door != null) door.SetLock(false);
            }
        }
    }
}