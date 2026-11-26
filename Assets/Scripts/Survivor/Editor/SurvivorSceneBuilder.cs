using System.IO;
using System.Linq;
using ParryRL.Survivor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ParryRL.EditorTools
{
    /// <summary>
    /// 뱀서류 시험판 씬(<c>Assets/Scenes/Survivor.unity</c>)과 그림 묶음을 만든다. 씬은 생성물이라 직접 고치지 말고 이 파일을 고친다.
    /// 배치: <c>-executeMethod ParryRL.EditorTools.SurvivorSceneBuilder.Build</c> / 시험판만 들어간 빌드: <c>.BuildWindows</c>.
    /// 기존 씬·빌드 설정은 건드리지 않는다.
    /// </summary>
    public static class SurvivorSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Survivor.unity";
        private const string ArtPath = "Assets/Art/Survivor/SurvivorArt.asset";
        private const float HeroHeight = 1.7f;

        [MenuItem("Tools/ParryRL/뱀서 시험판 씬 생성")]
        public static void Build()
        {
            var art = BuildArt();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            cam.AddComponent<SvView>();

            var game = new GameObject("Game").AddComponent<SvGame>();
            // NewScene이 안 쓰는 에셋을 내리므로 씬을 만든 뒤 다시 읽어 연결한다
            art = AssetDatabase.LoadAssetAtPath<SurvivorArt>(ArtPath);
            var so = new SerializedObject(game);
            so.FindProperty("art").objectReferenceValue = art;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SurvivorSceneBuilder] {ScenePath} 생성 (퇴마사 그림 {(art != null && art.heroIdle != null ? "있음" : "없음")})");
        }

        private const string ArtRoot = "Assets/Art/Survivor";
        private const float BossHeight = 3.9f;
        /// <summary>염라 몸 높이 (퇴마사의 약 7배 — 상반신만 솟은 거인, 가안) · 손 높이.</summary>
        private const float YeomraHeight = 12.5f, YeomraHandHeight = 6.4f;
        private const string FloorPath = "Assets/Art/Survivor/Ground/floor_a.png";
        // 바닥 타일 한 장의 크기 (유닛) — 풀 한 포기가 퇴마사 무릎 높이쯤 되게
        private const float FloorTileSize = 6.5f;

        private static SurvivorArt BuildArt()
        {
            var hero = ImportCharacter($"{ArtRoot}/Hero", "hero", HeroHeight);
            ImportFloor(FloorPath);
            ImportMusic($"{ArtRoot}/Resources/Music");
            var fx = ImportFlat($"{ArtRoot}/Fx", "", new Vector2(0.5f, 0.5f), sizeIsOneUnit: true);
            var props = ImportFlat($"{ArtRoot}/Props", "", new Vector2(0.5f, 0f), sizeIsOneUnit: false);
            var ui = ImportUi($"{ArtRoot}/Ui");
            var fg = ImportFlat($"{ArtRoot}/Fg", "", new Vector2(0.5f, 0.5f), sizeIsOneUnit: false);

            var art = AssetDatabase.LoadAssetAtPath<SurvivorArt>(ArtPath);
            if (art == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ArtPath)!);
                art = ScriptableObject.CreateInstance<SurvivorArt>();
                AssetDatabase.CreateAsset(art, ArtPath);
            }
            art.heroIdle = Frames(hero, "hero_base_").FirstOrDefault();
            art.heroRun = Frames(hero, "hero_run_");
            art.heroSwing = Frames(hero, "hero_swing_");
            art.heroSlam = Frames(hero, "hero_slam_");
            art.heroThrow = Frames(hero, "hero_throw_");
            art.heroJump = Frames(hero, "hero_jump_");
            art.heroDash = Frames(hero, "hero_dash_");
            art.heroDown = Frames(hero, "hero_down_");
            art.heroParry = Frames(hero, "hero_parry_");
            art.heroHurt = Frames(hero, "hero_hurt_");
            art.heroVictory = Frames(hero, "hero_victory_");
            art.heroGiantSummon = Frames(hero, "hero_giantsummon_");
            art.heroSummon = Frames(hero, "hero_summon_");
            art.heroHeight = HeroHeight;
            ImportRig(art);
            ImportVfx();
            art.changgwi = Mob("Changgwi", "changgwi", 1.58f, 7f);
            art.dokkaebibul = Mob("Dokkaebibul", "dokkaebibul", 1.0f, 9f);
            art.elite = Mob("Elite", "elite", 2.9f, 6f);
            art.changgwiDark = Dark(Mob("ChanggwiDark", "changgwidark", 1.58f, 7f), art.changgwi);
            art.dokkaebibulDark = Dark(Mob("DokkaebibulDark", "dokkaebibuldark", 1.0f, 9f), art.dokkaebibul);
            art.dalgyal = Mob("Dalgyal", "dalgyal", 1.75f, 6f);
            art.smallDokkaebi = Mob("SmallDokkaebi", "smalldokkaebi", 1.1f, 12f);
            art.dalgyalDark = Dark(Mob("DalgyalDark", "dalgyaldark", 1.75f, 6f), art.dalgyal);
            art.smallDokkaebiDark = Dark(Mob("SmallDokkaebiDark", "smalldokkaebidark", 1.1f, 12f), art.smallDokkaebi);
            // 불가살이: 한 폴더에 1·2페이즈가 함께 있다 (몸 높이 3유닛 — 퇴마사의 약 1.8배, 가안)
            var boss = ImportCharacter($"{ArtRoot}/Boss", "boss", BossHeight);
            var bossDeath = Frames(boss, "boss_death_");
            art.bossP1 = new SvMobArt { idle = Frames(boss, "boss_p1base_").FirstOrDefault(), move = Frames(boss, "boss_p1walk_"),
                attack = Frames(boss, "boss_p1attack_"), death = bossDeath, moveFps = 6f, height = BossHeight };
            art.bossP2 = new SvMobArt { idle = Frames(boss, "boss_p2base_").FirstOrDefault(), move = Frames(boss, "boss_p2walk_"),
                attack = Frames(boss, "boss_p2attack_"), death = bossDeath, moveFps = 8f, height = BossHeight * 1.15f };
            art.bossSpines = Frames(boss, "boss_p1spines_");
            art.bossTransform = Frames(boss, "boss_transform_");
            art.bossCharge = Frames(boss, "boss_p2charge_");
            art.bossEntrance = Frames(boss, "boss_entrance_");
            // 염라대왕 (S15): 몸 셋 · 손 넷
            var yeom = ImportCharacter($"{ArtRoot}/Yeomra", "yeomra", YeomraHeight);
            art.yeomra = new SvMobArt { idle = Frames(yeom, "yeomra_body_").FirstOrDefault(), moveFps = 1f, height = YeomraHeight };
            art.yeomraRage = Frames(yeom, "yeomra_rage_").FirstOrDefault();
            art.yeomraStun = Frames(yeom, "yeomra_stun_").FirstOrDefault();
            var yhand = ImportCharacter($"{ArtRoot}/Yeomra", "yeomrahand", YeomraHandHeight);
            art.yeomraSkullRest = Frames(yhand, "yeomrahand_skullrest_").FirstOrDefault();
            art.yeomraSkullRaise = Frames(yhand, "yeomrahand_skullraise_").FirstOrDefault();
            art.yeomraHumanRest = Frames(yhand, "yeomrahand_humanrest_").FirstOrDefault();
            art.yeomraHumanRaise = Frames(yhand, "yeomrahand_humanraise_").FirstOrDefault();
            art.heroUltimate = Frames(hero, "hero_ultimate_");

            art.fxSlash = Frames(fx, "slash_");
            art.fxSlamRing = Frames(fx, "slamring_");
            art.fxCounter = Frames(fx, "counter_");
            art.fxTalisman = Frames(fx, "talisman_");
            art.fxTalismanBurst = Frames(fx, "talburst_");
            art.fxHitSpark = Frames(fx, "hitspark_");
            art.fxParry = Frames(fx, "parry_");
            art.fxLevelUp = Frames(fx, "levelup_");
            art.gems = Frames(fx, "gem_");
            art.fxSpike = Frames(fx, "spike_");
            // S3·S4 그림 (cut_sprites.py가 만들면 연결, 없으면 빈 칸 → 코드가 S2 그림으로 대신함)
            art.fxStrikePillar = Frames(fx, "strikepillar_");
            art.fxStrikeGround = Frames(fx, "strikeground_");
            art.fxDebris = Frames(fx, "debris_");
            art.fxUltSeal = Frames(fx, "ultseal_");
            art.fxUltPillar = Frames(fx, "ultpillar_");
            art.fxBossEmerge = Frames(fx, "bossemerge_");
            art.fxBossRoar = Frames(fx, "bossroar_");
            art.fxEliteSpawn = Frames(fx, "elitespawn_");
            art.fxKillBurst = Frames(fx, "killburst_");
            art.fxSlash1 = Frames(fx, "slasht1_");
            art.fxSlash2 = Frames(fx, "slasht2_");
            art.fxSlash3 = Frames(fx, "slasht3_");
            art.fxHitBig = Frames(fx, "hitbig_");
            art.fxKillBurstB = Frames(fx, "killburstb_");
            art.fxBellRing = Frames(fx, "bellring_");
            art.fxLevelBurst = Frames(fx, "levelburst_");
            art.fxDashTrail = Frames(fx, "dashtrail_");
            art.fxScorch = Frames(fx, "scorch_");
            art.fxSweep = Frames(fx, "sweep_");
            art.fxUltWave = Frames(fx, "ultwave_");
            art.fxEliteRend = Frames(fx, "eliterend_");
            art.fxEliteAura = Frames(fx, "eliteaura_");
            art.fxBossCrash = Frames(fx, "bosscrash_");
            art.fxChargeTrail = Frames(fx, "chargetrail_");
            art.fxBossRoarRing = Frames(fx, "bossroarring_");
            // S3 패링·반격은 더 화려한 새 그림으로 바꿔 끼운다
            var parry3 = Frames(fx, "parryburst_");
            if (parry3.Length > 0) art.fxParry = parry3;
            var counter3 = Frames(fx, "counter2_");
            if (counter3.Length > 0) art.fxCounter = counter3;
            art.fxCrack = Frames(fx, "crack_");

            // 소품: 무덤 2 · 비석 1 · 장승 3 · 초가 2 · 고사목 3 (세운 높이는 가안 — 퇴마사 1.7 기준)
            var propList = new System.Collections.Generic.List<Sprite>();
            var heights = new System.Collections.Generic.List<float>();
            void AddProps(string prefix, params float[] h)
            {
                var f = Frames(props, prefix);
                for (int i = 0; i < f.Length && i < h.Length; i++) { propList.Add(f[i]); heights.Add(h[i]); }
            }
            AddProps("grave_", 1.3f, 1.4f, 1.6f);
            AddProps("jangseung_", 2.4f, 2.4f, 2.3f);
            AddProps("hut_", 3.6f, 3.4f);
            AddProps("tree_", 4.4f, 3.4f, 2.6f);
            art.props = propList.ToArray();
            art.propHeights = heights.ToArray();
            art.decals = Frames(props, "decal_");
            art.walls = Frames(props, "wall_");
            art.fences = Frames(props, "fence_");
            art.village = Frames(props, "village_");
            art.folk = Frames(props, "folk_");
            art.pines = Frames(props, "pine_");
            art.bushes = Frames(props, "bush_");
            art.rocks = Frames(props, "rock_");
            art.lights = Frames(props, "light_");
            art.decals2 = Frames(props, "decalb_");
            art.fgBranches = Frames(fg, "fgbranch_");
            art.fgGrass = Frames(fg, "fggrass_");

            // ui/bars.png 조각: 0·1 = 빈 틀, 2 = 청록 채움, 3 = 옥색 채움
            art.barFrame = Frames(ui, "bar_0").FirstOrDefault();
            art.expFill = Frames(ui, "bar_2").FirstOrDefault();
            art.hpFill = Frames(ui, "bar_3").FirstOrDefault();
            art.bossFill = Frames(ui, "bar_boss").FirstOrDefault();
            art.card = Frames(ui, "card_").FirstOrDefault();
            art.skillIcons = Frames(ui, "icon_");
            art.icons2 = Frames(ui, "icon2_").OrderBy(p => p.name.Length).ThenBy(p => p.name, System.StringComparer.Ordinal).ToArray();
            art.menuBg = Frames(ui, "menubg_").FirstOrDefault();
            art.panel = Frames(ui, "panel_").FirstOrDefault();
            art.button = Frames(ui, "button_0").FirstOrDefault();
            art.slotWeapon = Frames(ui, "slotframe_0").FirstOrDefault();
            art.slotPassive = Frames(ui, "slotframe_1").FirstOrDefault();
            art.banner = Frames(ui, "banner_").FirstOrDefault();
            art.bossTitle = Frames(ui, "bosstitle_").FirstOrDefault();
            art.warnBand = Frames(ui, "warnband_").FirstOrDefault();
            art.arrowElite = Frames(ui, "arrow_0").FirstOrDefault();
            art.arrowBoss = Frames(ui, "arrow_1").FirstOrDefault();
            art.ultFrame = Frames(ui, "ultgauge_0").FirstOrDefault();
            art.ultFill = Frames(ui, "ultgauge_1").FirstOrDefault();

            // S6
            art.fxParryGuard = Frames(fx, "parryguard_");
            art.fxGiantStaff = Frames(fx, "giantstaff_");
            art.fxLineCrack = Frames(fx, "linecrack_");
            art.gemBig = Frames(fx, "gembig_");
            art.cardWeapon = Frames(ui, "cardw_").FirstOrDefault();
            art.cardPassive = Frames(ui, "cardp_").FirstOrDefault();
            art.cardHeal = Frames(ui, "cardh_").FirstOrDefault();
            var parts = Frames(ui, "cardpart_");
            Sprite Part(int i) => i < parts.Length ? parts[i] : null;
            art.frameWeapon = Part(0); art.framePassive = Part(1); art.frameHeal = Part(2);
            art.pipEmpty = Part(4); art.pipGold = Part(5); art.pipJade = Part(6);
            art.cardSeal = Part(3);
            art.emblems = Frames(ui, "emblem_");
            art.icons3 = Frames(ui, "icon3_");
            var mini = Frames(ui, "minimap_");
            Sprite Mini(int i) => i < mini.Length ? mini[i] : null;
            art.miniFrame = Mini(0); art.miniPlayer = Mini(1); art.miniElite = Mini(2); art.miniBoss = Mini(3); art.miniLight = Mini(4); art.miniGem = Mini(5);
            art.tooltipFrame = Frames(ui, "tooltip_").FirstOrDefault();
            art.resultWin = Frames(ui, "result_0").FirstOrDefault();
            art.resultLose = Frames(ui, "result_1").FirstOrDefault();
            art.boundForest = Frames(props, "boundf_");
            // S8 (핵심 비급 · 조력자) — 그림이 없으면 비어 있고 게임이 있는 그림으로 대신한다
            art.cardCore = Frames(ui, "cardc_").FirstOrDefault();
            var coreParts = Frames(ui, "corepart_");
            art.coreEmblem = coreParts.Length > 0 ? coreParts[0] : null;
            art.coreBadge = coreParts.Length > 1 ? coreParts[1] : null;
            art.helperIcons = Frames(ui, "helpericon_");
            // S14 — 아이콘은 이름 순(10이 2보다 앞)이 아니라 번호 순으로
            art.cardSkill = Frames(ui, "cards_").FirstOrDefault();
            var slotB = Numbered(Frames(ui, "slotframeb_"));
            art.slotSkill = slotB.Length > 0 ? slotB[0] : null;
            art.slotEmpty = slotB.Length > 1 ? slotB[1] : null;
            art.slotStaff = slotB.Length > 2 ? slotB[2] : null;
            var coreWin = Numbered(Frames(ui, "corewin_"));
            art.coreBanner = coreWin.Length > 0 ? coreWin[0] : null;
            art.coreFork = coreWin.Length > 1 ? coreWin[1] : null;
            art.coreIcons = Numbered(Frames(ui, "coreicon_"));
            art.iconLeap = Frames(ui, "iconleap_").FirstOrDefault();
            art.itemRiceball = Frames(fx, "riceball_").FirstOrDefault();
            art.fxSummon = Frames(fx, "summon_");
            art.fxWard = Frames(fx, "ward_");
            // S9
            art.fxBind = Frames(fx, "bind_");
            art.fxSlow = Frames(fx, "slow_");
            art.fxTalismanBlast = Frames(fx, "talblast_");
            art.fxCoreGet = Frames(fx, "coreget_");
            art.titleEmblem = Frames(ui, "titleemblem_").FirstOrDefault();
            art.panelLarge = Frames(ui, "panelbig_").FirstOrDefault();
            art.settingsParts = Frames(ui, "setpart_");
            // S10
            art.storyPanels = Frames(ui, "story_");
            art.gateDormant = Frames(props, "gate_0").FirstOrDefault();
            art.gateActive = Frames(props, "gate_1").FirstOrDefault();
            art.itemGourd = Frames(fx, "drop_0").FirstOrDefault();
            art.itemThunder = Frames(fx, "drop_1").FirstOrDefault();
            art.fxGateSpawn = Frames(fx, "gatespawn_");
            art.fxGourdPull = Frames(fx, "gourdpull_");
            art.fxThunderStrike = Frames(fx, "thunder_");
            var tiger = ImportCharacter($"{ArtRoot}/Helpers", "tiger", HeroHeight);
            var shaman = ImportCharacter($"{ArtRoot}/Helpers", "shaman", HeroHeight);
            art.tigerRun = Frames(tiger, "tiger_run_");
            art.tigerPounce = Frames(tiger, "tiger_pounce_");
            art.shamanDance = Frames(shaman, "shaman_dance_");
            // 결계 셋째 조각(석상)은 벅수 대신 사자 석상이 나와 쓰지 않는다 (S6 로그)
            art.boundWard = Frames(props, "boundw_").Take(2).ToArray();

            art.fontTitle = ImportFont($"{ArtRoot}/Fonts/EastSeaDokdo-Regular.ttf");
            art.fontBody = ImportFont($"{ArtRoot}/Fonts/SongMyung-Regular.ttf");
            art.floorTile = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorPath);
            art.floorTileSize = FloorTileSize;
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return art;
        }

        /// <summary>배경 음악: 메모리에 다 풀지 않고 디스크에서 흘려 읽는다 (곡이 길다).</summary>
        private static void ImportMusic(string dir)
        {
            if (!Directory.Exists(dir)) return;
            AssetDatabase.Refresh();
            foreach (var p in Directory.GetFiles(dir).Select(f => f.Replace('\\', '/')).Where(f => !f.EndsWith(".meta") && !f.EndsWith(".md")))
            {
                if (AssetImporter.GetAtPath(p) is not AudioImporter ai) continue;
                var s = ai.defaultSampleSettings;
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                ai.defaultSampleSettings = s;
                ai.loadInBackground = true;
                ai.SaveAndReimport();
            }
        }

        /// <summary>짙은 변종은 걷기만 새로 그렸다 — 쓰러짐은 원래 요괴 그림을 쓴다.</summary>
        /// <summary>스킬 입자 시트 (Tools/survivor_art/cut_vfx.py 결과, Resources에서 이름으로 읽는다).</summary>
        private static void ImportVfx()
        {
            // 요괴 뼈대 아틀라스 (Tools/survivor_art/cut_mobs.py 결과)
            const string rigs = ArtRoot + "/Resources/MobRig";
            if (Directory.Exists(rigs))
            {
                AssetDatabase.Refresh();
                foreach (var p in Directory.GetFiles(rigs, "*.png").Select(x => x.Replace('\\', '/')))
                {
                    if (AssetImporter.GetAtPath(p) is not TextureImporter ri) continue;
                    ri.textureType = TextureImporterType.Default;
                    ri.alphaIsTransparency = true;
                    ri.mipmapEnabled = true;
                    ri.wrapMode = TextureWrapMode.Clamp;
                    ri.filterMode = FilterMode.Bilinear;
                    ri.textureCompression = TextureImporterCompression.Uncompressed;
                    ri.maxTextureSize = 4096;
                    ri.SaveAndReimport();
                }
            }
            const string dir = ArtRoot + "/Resources/Vfx";
            if (!Directory.Exists(dir)) return;
            foreach (var p in Directory.GetFiles(dir, "*.png").Select(x => x.Replace('\\', '/')))
            {
                if (AssetImporter.GetAtPath(p) is not TextureImporter ti) continue;
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = p.EndsWith("vfx_ink.png") || p.EndsWith("vfx_cloud.png") || p.EndsWith("vfx_solid.png");
                ti.sRGBTexture = ti.alphaIsTransparency;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
        }

        /// <summary>퇴마사 뼈대: 아틀라스 · 부위 정보 · 재질 (Tools/survivor_art/cut_rig.py 결과). 없으면 예전 그림 장면으로 움직인다.</summary>
        private static void ImportRig(SurvivorArt art)
        {
            const string dir = ArtRoot + "/HeroRig";
            string png = dir + "/hero_rig.png", mat = dir + "/HeroRig.mat";
            AssetDatabase.Refresh();
            if (AssetImporter.GetAtPath(png) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Trilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            art.heroRigAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            art.heroRigInfo = AssetDatabase.LoadAssetAtPath<TextAsset>(dir + "/hero_rig.json");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(dir + "/SvRig.shader");
            if (shader == null || art.heroRigAtlas == null) { art.heroRigMaterial = null; return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(mat);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, mat);
            }
            m.shader = shader;
            m.mainTexture = art.heroRigAtlas;
            EditorUtility.SetDirty(m);
            art.heroRigMaterial = m;
        }

        private static SvMobArt Dark(SvMobArt dark, SvMobArt baseMob)
        {
            if (!dark.Has) return dark;
            if (!SurvivorArt.Ok(dark.death)) dark.death = baseMob.death;
            // S9부터 짙은 변종 공격 그림이 따로 있다 — 없을 때만 원래 색 그림을 빌린다
            if (!SurvivorArt.Ok(dark.attack)) dark.attack = baseMob.attack;
            return dark;
        }

        /// <summary>
        /// 글꼴: 동적 글꼴로, 글꼴에 없는 글자(한자·◀▶■)는 Windows 기본 글꼴로 채운다 (바탕 → 맑은 고딕).
        /// </summary>
        private static Font ImportFont(string path)
        {
            AssetDatabase.Refresh();
            if (AssetImporter.GetAtPath(path) is not TrueTypeFontImporter fi) return null;
            fi.fontTextureCase = FontTextureCase.Dynamic;
            fi.includeFontData = true;
            fi.fontNames = new[] { "Batang", "바탕", "Malgun Gothic", "맑은 고딕", "Arial" };
            fi.fontRenderingMode = FontRenderingMode.Smooth;
            fi.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        private static SvMobArt Mob(string folder, string name, float height, float fps)
        {
            var paths = ImportCharacter($"{ArtRoot}/{folder}", name, height);
            return new SvMobArt
            {
                idle = Frames(paths, $"{name}_base_").FirstOrDefault(),
                move = Frames(paths, $"{name}_move_"),
                attack = Frames(paths, $"{name}_attack_"),
                death = Frames(paths, $"{name}_death_"),
                moveFps = fps,
                height = height,
            };
        }

        /// <summary>이펙트·소품 그림. sizeIsOneUnit이면 그림 한 장 = 1유닛(이펙트는 코드에서 크기를 정함), 아니면 200px = 1유닛.</summary>
        private static string[] ImportFlat(string dir, string prefix, Vector2 pivot, bool sizeIsOneUnit)
        {
            var paths = List(dir, prefix);
            foreach (var p in paths)
            {
                if (AssetImporter.GetAtPath(p) is not TextureImporter ti) continue;
                int px = 200;
                if (sizeIsOneUnit)
                {
                    ti.GetSourceTextureWidthAndHeight(out int w, out int h);
                    px = Mathf.Max(w, h);
                }
                SetSprite(ti, pivot, px);
            }
            return paths;
        }

        private static string[] ImportUi(string dir)
        {
            var paths = List(dir, "");
            foreach (var p in paths)
            {
                if (AssetImporter.GetAtPath(p) is not TextureImporter ti) continue;
                // 설명 상자 틀은 늘려 쓴다 — 모서리 장식만 그대로 두는 9칸 자르기
                var border = Path.GetFileName(p).StartsWith("tooltip_") ? new Vector4(48, 48, 48, 48) : Vector4.zero;
                SetSprite(ti, new Vector2(0.5f, 0.5f), 100, mipmaps: false, border);
            }
            return paths;
        }

        private static string[] List(string dir, string prefix)
        {
            AssetDatabase.Refresh();
            return Directory.Exists(dir)
                ? Directory.GetFiles(dir, $"{prefix}*.png").Select(p => p.Replace('\\', '/')).OrderBy(p => p, System.StringComparer.Ordinal).ToArray()
                : new string[0];
        }

        private static void SetSprite(TextureImporter ti, Vector2 pivot, float ppu, bool mipmaps = true, Vector4 border = default)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder = border;
            ti.SetTextureSettings(settings);
            ti.spritePixelsPerUnit = ppu;
            ti.mipmapEnabled = mipmaps;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }

        /// <summary>이름 끝 번호(_0, _1 … _14) 순으로 다시 늘어놓는다.</summary>
        private static Sprite[] Numbered(Sprite[] sprites) =>
            sprites.OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int n) ? n : 0).ToArray();

        private static Sprite[] Frames(string[] paths, string prefix) =>
            paths.Where(p => Path.GetFileName(p).StartsWith(prefix))
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();

        private static void ImportFloor(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti) return;
            ti.textureType = TextureImporterType.Default;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        /// <summary>cut_sprites.py가 만든 프레임을 스프라이트로 가져온다 (몸 높이 = height 유닛, 피벗 = 발).</summary>
        private static string[] ImportCharacter(string dir, string name, float height)
        {
            int bodyPx = 420;
            Vector2 pivot = new(0.5f, 0.02f);
            string metaPath = $"{dir}/{name}_meta.json";
            if (File.Exists(metaPath))
            {
                var meta = JsonUtility.FromJson<HeroMeta>(File.ReadAllText(metaPath));
                if (meta.bodyHeightPx > 0) bodyPx = meta.bodyHeightPx;
                if (meta.pivot != null && meta.pivot.Length == 2) pivot = new Vector2(meta.pivot[0], meta.pivot[1]);
            }
            float ppu = bodyPx / height;

            var paths = Directory.Exists(dir)
                ? Directory.GetFiles(dir, $"{name}_*.png").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray()
                : new string[0];
            AssetDatabase.Refresh();
            foreach (var p in paths)
            {
                if (AssetImporter.GetAtPath(p) is not TextureImporter ti) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(settings);
                ti.spritePixelsPerUnit = ppu;
                ti.mipmapEnabled = true;
                ti.alphaIsTransparency = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return paths;
        }

        [System.Serializable]
        private class HeroMeta
        {
            public int bodyHeightPx;
            public float[] pivot;
        }

        /// <summary>시험판 씬만 넣은 Windows 빌드 (성능 측정용). 프로젝트의 빌드 설정은 바꾸지 않는다.</summary>
        [MenuItem("Tools/ParryRL/뱀서 시험판 빌드 (Windows)")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) Build();
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Survivor/GwimunSurvivor.exe",
                BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log($"[SurvivorSceneBuilder] 빌드 결과: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)}MB");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>
        /// 시험판 macOS 빌드 (팀원용, Intel + Apple Silicon 범용). Unity Hub의 "Mac Build Support (Mono)" 모듈이 있어야 한다.
        /// 아키텍처 설정 형식(UnityEditor.OSXStandalone)은 그 모듈에만 있어서, 모듈이 없는 PC에서도 컴파일되게 리플렉션으로 정한다.
        /// </summary>
        [MenuItem("Tools/ParryRL/뱀서 시험판 빌드 (macOS)")]
        public static void BuildMac()
        {
            if (!File.Exists(ScenePath)) Build();
            var settings = System.Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions");
            var arch = settings?.GetProperty("architecture", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            // -macArch ARM64 | x64 | x64ARM64 (기본 = 범용). 맥 런처는 이 맥 것만 빌드한다 —
            // 범용은 Burst의 llvm-lipo로 둘을 합치는데, 일부 설치에선 그 파일에 실행 권한이 없어 빌드가 실패한다.
            var args = System.Environment.GetCommandLineArgs();
            int ai = System.Array.IndexOf(args, "-macArch");
            string archName = ai >= 0 && ai + 1 < args.Length ? args[ai + 1] : "x64ARM64";
            if (arch != null)
            {
                arch.SetValue(null, System.Enum.Parse(arch.PropertyType, archName));
                Debug.Log($"[SurvivorSceneBuilder] macOS 아키텍처: {arch.GetValue(null)}");
            }
            else Debug.LogWarning("[SurvivorSceneBuilder] macOS 아키텍처 설정을 찾지 못함 — 기본값으로 빌드");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/SurvivorMac/GwimunSurvivor.app",
                BuildTarget.StandaloneOSX, BuildOptions.None);
            Debug.Log($"[SurvivorSceneBuilder] macOS 빌드 결과: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)}MB");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
