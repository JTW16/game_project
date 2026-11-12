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
        private const float BossHeight = 3f;
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
            art.heroHeight = HeroHeight;
            art.changgwi = Mob("Changgwi", "changgwi", 1.45f, 7f);
            art.dokkaebibul = Mob("Dokkaebibul", "dokkaebibul", 0.9f, 9f);
            art.elite = Mob("Elite", "elite", 2.3f, 6f);
            art.changgwiDark = Dark(Mob("ChanggwiDark", "changgwidark", 1.45f, 7f), art.changgwi);
            art.dokkaebibulDark = Dark(Mob("DokkaebibulDark", "dokkaebibuldark", 0.9f, 9f), art.dokkaebibul);
            art.dalgyal = Mob("Dalgyal", "dalgyal", 1.6f, 6f);
            art.smallDokkaebi = Mob("SmallDokkaebi", "smalldokkaebi", 1.0f, 12f);
            art.dalgyalDark = Dark(Mob("DalgyalDark", "dalgyaldark", 1.6f, 6f), art.dalgyal);
            art.smallDokkaebiDark = Dark(Mob("SmallDokkaebiDark", "smalldokkaebidark", 1.0f, 12f), art.smallDokkaebi);
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
            art.fxSummon = Frames(fx, "summon_");
            art.fxWard = Frames(fx, "ward_");
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
        private static SvMobArt Dark(SvMobArt dark, SvMobArt baseMob)
        {
            if (!dark.Has) return dark;
            if (!SurvivorArt.Ok(dark.death)) dark.death = baseMob.death;
            dark.attack = baseMob.attack;
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
    }
}
