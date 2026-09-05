using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(EnemyAI_NormalMelee))]
public class EnemyMLAgent : Agent
{
    private EnemyAI_NormalMelee aiController;
    float prevDistance;

    public override void Initialize()
    {
        aiController = GetComponent<EnemyAI_NormalMelee>();
        aiController.mlAgent = this; // 把自己注册给控制器

        if (aiController.player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                aiController.player = playerObj.transform;
        }
    }

    public override void OnEpisodeBegin()
    {
        prevDistance = Vector2.Distance(transform.position, aiController.player.position);

        // 训练重置逻辑：恢复血量、重置位置、重置状态等
        if (aiController.myStats != null)
        {
            aiController.myStats.ResetStats();
        }
        aiController.currentState = EnemyAI_NormalMelee.State.Idle;

        // 记得清空旧奖励
        aiController.ConsumeRewardBuffer();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (aiController.player == null) return;

        // 1. 距离归一化
        float dist = Vector2.Distance(transform.position, aiController.player.position);
        sensor.AddObservation(Mathf.Clamp01(dist / aiController.visionRange));

        // 2. 血量比例
        float hpPercent = aiController.myStats.currentHealth / aiController.myStats.maxHealth;
        sensor.AddObservation(hpPercent);

        // 3. 玩家相对方向
        int dirToPlayer = aiController.player.position.x > transform.position.x ? 1 : -1;
        sensor.AddObservation(dirToPlayer);
        sensor.AddObservation(aiController.facingDirection);

        // 4. 技能冷却情况
        sensor.AddObservation(Time.time >= aiController.lastAttackTime + aiController.sharedAttackCD ? 1f : 0f);
        sensor.AddObservation(Time.time >= aiController.lastDodgeTime + aiController.dodgeCooldown ? 1f : 0f);

        // 5. 自身信息状态
        sensor.AddObservation(aiController.currentState == EnemyAI_NormalMelee.State.Attacking ? 1f : 0f);
        sensor.AddObservation(aiController.currentState == EnemyAI_NormalMelee.State.Dodging ? 1f : 0f);

        // 6. 玩家攻击前摇读取
        var playerStats = aiController.player.GetComponent<PlayerController>();
        if (playerStats != null)
        {
            sensor.AddObservation(playerStats.isAttacking ? 1f : 0f);
            sensor.AddObservation(playerStats.GetAttackWindupProgress());
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (aiController.aiType != EnemyAI_NormalMelee.AIType.MLAgents) return;

        // 如果处于受控状态或死亡，不执行动作
        if (aiController.currentState == EnemyAI_NormalMelee.State.Dodging ||
            aiController.currentState == EnemyAI_NormalMelee.State.Staggered ||
            aiController.currentState == EnemyAI_NormalMelee.State.Broken ||
            aiController.currentState == EnemyAI_NormalMelee.State.Dead ||
            aiController.currentState == EnemyAI_NormalMelee.State.Attacking)
        {
            return;
        }

        int action = actions.DiscreteActions[0];
        float actionPenalty = 0f; // 记录无效操作的惩罚

        //Debug.Log($"[{Time.frameCount}] ML Agent 决定执行: {(EnemyAI_NormalMelee.RLAction)action}");

        switch ((EnemyAI_NormalMelee.RLAction)action)
        {
            case EnemyAI_NormalMelee.RLAction.Idle:
                aiController.DoIdle();
                break;

            case EnemyAI_NormalMelee.RLAction.AttackShort:
                if (Time.time >= aiController.lastAttackTime + aiController.sharedAttackCD)
                    aiController.DoShortAttack();
                else
                    actionPenalty -= 0.2f;
                break;

            case EnemyAI_NormalMelee.RLAction.AttackMid:
                if (Time.time >= aiController.lastAttackTime + aiController.sharedAttackCD)
                    aiController.DoMidAttack();
                else
                    actionPenalty -= 0.2f;
                break;

            case EnemyAI_NormalMelee.RLAction.Dodge:
                // 加上核心的闪避 CD 检查！
                if (Time.time >= aiController.lastDodgeTime + aiController.dodgeCooldown)
                {
                    if (aiController.currentState != EnemyAI_NormalMelee.State.Dodging && aiController.currentState != EnemyAI_NormalMelee.State.Dead)
                    {
                        aiController.DoDodge();
                    }
                }
                else
                {
                    actionPenalty -= 0.2f;
                }
                break;

            case EnemyAI_NormalMelee.RLAction.Chase:
                aiController.DoChase();
                break;
        }

        float currentDist = Vector2.Distance(transform.position, aiController.player.position);

        float delta = prevDistance - currentDist;

        // 接近玩家 → 正奖励
        AddReward(delta * 0.1f);

        prevDistance = currentDist;

        // 提取主脚本中计算好的奖励并提交给 ML-Agents
        float stepReward = aiController.ConsumeRewardBuffer();

        // 给予每帧的时间惩罚，鼓励尽快击败玩家 (可选)
        stepReward -= 0.01f;
        stepReward += actionPenalty; // 加上非法操作惩罚

        AddReward(stepReward);
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        // 屏蔽掉在CD中的攻击动作
        if (Time.time < aiController.lastAttackTime + aiController.sharedAttackCD)
        {
            actionMask.SetActionEnabled(0, (int)EnemyAI_NormalMelee.RLAction.AttackShort, false);
            actionMask.SetActionEnabled(0, (int)EnemyAI_NormalMelee.RLAction.AttackMid, false);
        }

        // 屏蔽掉在CD中的闪避动作
        if (Time.time < aiController.lastDodgeTime + aiController.dodgeCooldown)
        {
            actionMask.SetActionEnabled(0, (int)EnemyAI_NormalMelee.RLAction.Dodge, false);
        }
    }
}