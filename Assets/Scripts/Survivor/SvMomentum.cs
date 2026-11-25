using UnityEngine;

namespace ParryRL.Survivor
{
    /// <summary>
    /// 기세 (2026-11-25 사용자 결정 "속도감이 우리 게임의 강점"): 달리고 · 보법을 밟고 · 뛰고 · 쓰러뜨리기를 이어 가면 쌓이고,
    /// 멈추거나 맞으면 줄어든다. 단계(1 순풍 · 2 질풍 · 3 광풍)마다 이동과 공격 간격이 빨라진다. 수치는 모두 가안.
    /// </summary>
    public class SvMomentum : MonoBehaviour
    {
        public const float Max = 100f;
        public static readonly float[] Steps = { 25f, 55f, 85f };
        public static readonly string[] Names = { "", "순풍", "질풍", "광풍" };

        [Header("쌓기 (가안)")]
        [SerializeField] private float movePerSecond = 5f;
        [SerializeField] private float dashGain = 8f;
        [SerializeField] private float jumpGain = 3f;
        [SerializeField] private float leapGain = 7f;
        [SerializeField] private float diveGain = 8f;
        [SerializeField] private float killGain = 0.7f;
        [Header("줄기 (가안)")]
        [SerializeField] private float fadePerSecond = 5f;
        [SerializeField] private float standPerSecond = 16f;
        [SerializeField] private float standGrace = 0.6f;
        [SerializeField] private float hurtLoss = 30f;
        [Header("단계마다 (가안)")]
        [SerializeField] private float speedPerStage = 0.06f;
        [SerializeField] private float cooldownPerStage = 0.06f;

        public float Value { get; private set; }
        public int Stage { get; private set; }
        /// <summary>단계가 오른 순간 (HUD·연출용).</summary>
        public event System.Action<int> StageUp;

        private SvPlayer _player;
        private float _stillFor, _dashAt = -9f, _jumpAt = -9f;

        public float SpeedMul => 1f + speedPerStage * Stage;
        public float CooldownMul => 1f - cooldownPerStage * Stage;

        private void Awake()
        {
            _player = GetComponent<SvPlayer>();
            _player.Damaged += OnHurt;
        }

        private void OnDestroy()
        {
            if (_player != null) _player.Damaged -= OnHurt;
        }

        private void OnHurt(int amount) => Set(Value - hurtLoss);

        public void OnDash()
        {
            // 제자리에서 연달아 눌러 채우지 못하게, 방금 밟았으면 적게
            Set(Value + dashGain * Mathf.Clamp01((Time.time - _dashAt) / 0.9f));
            _dashAt = Time.time;
        }

        public void OnJump(bool second)
        {
            Set(Value + (second ? leapGain : jumpGain) * Mathf.Clamp01((Time.time - _jumpAt) / (second ? 0.3f : 1f)));
            _jumpAt = Time.time;
        }

        public void OnDive(float power) => Set(Value + diveGain * (0.4f + 0.6f * power));
        public void OnKill() => Set(Value + killGain);

        /// <summary>테스트용.</summary>
        public void Set(float v)
        {
            Value = Mathf.Clamp(v, 0f, Max);
            int s = 0;
            for (int i = 0; i < Steps.Length; i++)
                // 한 번 오른 단계는 조금 내려가도 바로 꺼지지 않는다
                if (Value >= Steps[i] - (Stage > i ? 8f : 0f)) s = i + 1;
            if (s == Stage) return;
            bool up = s > Stage;
            Stage = s;
            if (up) StageUp?.Invoke(s);
        }

        private void Update()
        {
            if (SvGame.InputBlocked || _player.Dead) return;
            float dt = Time.deltaTime;
            bool moving = _player.MoveInput.sqrMagnitude > 0.01f || _player.Dashing || _player.Airborne;
            _stillFor = moving ? 0f : _stillFor + dt;
            float v = Value;
            if (moving)
            {
                // 걷기만으로는 1단계까지, 그 위는 보법·경공·처치로 올려야 한다
                if (v < Steps[0]) v += movePerSecond * dt;
                else v -= fadePerSecond * 0.5f * dt;
            }
            else v -= (_stillFor > standGrace ? standPerSecond : fadePerSecond) * dt;
            Set(v);
        }
    }
}
