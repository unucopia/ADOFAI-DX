using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using ADOFAI;
using HarmonyLib;
using UnityEngine;
using UnityFileDialog;
using UnityModManagerNet;

namespace ADOFAIDX
{

public static class AdofaiDxMod
{
	private struct BreakAttempt
	{
		public int FloorId;

		public int HitIndex;

		public bool Perfect;

		public string Grade;

		public string NoteType;
	}

	private sealed class DXGameplayVisual
	{
		public scrFloor Floor;

		public GameObject Root;

		public LineRenderer Inner;

		public Mesh SurfaceMesh;

		public Renderer SurfaceRenderer;

		public Material SurfaceMaterial;

		public SpriteRenderer SurfaceSpriteRenderer;

		public Material SurfaceSpriteMaterial;

		public Mesh GlowMesh;

		public MeshRenderer GlowRenderer;

		public Mesh MeshGlowSource;

		public MeshRenderer[] MeshGlowRenderers;

		public Material[] MeshGlowMaterials;

		public SpriteRenderer[] MeshGlowSpriteRenderers;

		public Material[] MeshGlowSpriteMaterials;

		public GameObject GameplayTextRoot;

		public List<Vector3> GlowVertices;

		public List<Color> GlowColors;

		public List<int> GlowTriangles;

		public Vector2[] OutlinePolygon;

		public Vector3[] OutlineLinePositions;

		public Vector2[] GlowGridPositions;

		public float[] GlowGridDistances;

		public float OutlineRefreshUntil;

		public float NextOutlineRefreshTime;

		public Mesh SourceMesh;

		public Sprite SourceSprite;

		public int SourceMeshSignature;

		public int SourceColliderSignature;

		public float NextGeometryCheckTime;

		public float CreatedAt;

		public bool GeometryReady;

		public Vector2 LocalCenter;

		public Vector2 HalfExtents;

		public Color OriginalColor;

		public bool Judged;

		public float FreezeStartedAt;

		public Vector3 FreezeOffset;

		public Quaternion FreezeRotation;

		public Vector3 FreezeScale;
	}

	private struct BreakVisual
	{
		public Vector3 WorldPosition;

		public bool Perfect;

		public float StartTime;
	}

	private const string MarkerTag = "ADOFAIDX.DX";

	private const string LegacyDxFlashMarkerTag = "ADOFAIDX.DX.v2";

	private const string LegacyBreakMarkerTag = "ADOFAIDX.Break.v1";

	private const string LegacyMarkerTag = "CPerfect.CP.v1";

	internal const float BreakWindowScale = 1f / 3f;

	private const float BreakMaxXAccuracyBonus = 0.01f;

	private const float BreakAccuracyDisplayEpsilon = 0.0000001f;

	internal const int DefaultDxColorR = 255;

	internal const int DefaultDxColorG = 107;

	internal const int DefaultDxColorB = 13;

	internal static UnityModManager.ModEntry modEntry;

	private static GameObject overlayObject;

	internal static AdofaiDxOverlay overlayComponent;

	private static Harmony harmony;

	private static readonly HashSet<int> markedFloors = new HashSet<int>();

	private static readonly HashSet<int> activeDXVisualFloorIds = new HashSet<int>();

	private static readonly List<int> dxVisualCleanupIds = new List<int>();

	private static readonly float[] DXGlowLayerScales = new float[6] { 1.015f, 1.05f, 1.09f, 1.14f, 1.2f, 1.28f };

	private static readonly float[] DXGlowLayerAlphas = new float[6] { 0.3f, 0.22f, 0.15f, 0.095f, 0.055f, 0.028f };

	private static readonly MaterialPropertyBlock DXGlowPropertyBlock = new MaterialPropertyBlock();

	private static readonly MaterialPropertyBlock DXSurfacePropertyBlock = new MaterialPropertyBlock();

	private static readonly Dictionary<int, Mesh> DXSurfaceMeshCache = new Dictionary<int, Mesh>();

	private static readonly Dictionary<int, Mesh> DXGlowMeshCache = new Dictionary<int, Mesh>();

	private static readonly List<BreakAttempt> breakAttempts = new List<BreakAttempt>();

	private static LevelData trackedLevelData;

	private static LevelData legacyMigrationCheckedLevel;

	private static float nextMarkerRefreshTime;

	private static readonly FieldInfo DetailedResultsResultsField = typeof(DetailedResults).GetField("results", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo DetailedResultsPlayerIndexField = typeof(DetailedResults).GetField("currentPlayerIndex", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo DetailedResultsTextComponentField = typeof(DetailedResults).GetField("textComponent", BindingFlags.Instance | BindingFlags.Public);

	private static readonly MethodInfo DetailedResultsGenerateResultsMethod = typeof(DetailedResults).GetMethod("GenerateResults", BindingFlags.Instance | BindingFlags.NonPublic);

	private static int lastDetailedResultsStateKey = int.MinValue;

	private static GUIStyle badgeStyle;

	private static GUIStyle infoStyle;

	private static GUIStyle noticeStyle;

	private static GUIStyle breakFeedbackStyle;

	private static GUIStyle cAccuracyStyle;

	private static GUIStyle foldoutTriangleStyle;

	private static GUIStyle settingsSectionStyle;

	private static Texture2D roundedPanelTexture;

	private static Texture2D softGlowTexture;

	private static Texture2D ringGlowTexture;

	private static Texture2D spectrumPickerTexture;

	private static Material dxEdgeMaterial;

	private static Material dxGlowMaterial;

	private static Texture2D dxGameplayTextTexture;

	private static Sprite dxGameplayTextSprite;

	private static bool loggedDxVisualInfo;

	private static bool loggedGameplayDxText;

	private static bool screenTransitionActive;

	private static float screenTransitionDeadline;

	private static string noticeText;

	private static float noticeUntil;

	private static bool noticeError;

	private static bool loggedEditorFound;

	private static int lastReportedMarkerCount = -1;

	internal static AdofaiDxSettings settings;

	private static GameObject feedbackObject;

	private static Component feedbackAudioSource;

	private static Type audioSourceType;

	private static Type audioClipType;

	private static object perfectChimeClip;

	private static object perfectClickClip;

	private static object failedChimeClip;

	private static object failedClickClip;

	private static object customPerfectClip;

	private static object customFailedClip;

	internal static string customAudioStatus = "";

	private static bool customPerfectLoading;

	private static bool customFailedLoading;

	private static int activeColorPicker;

	private static readonly Dictionary<int, GameObject> dxMarkerObjects = new Dictionary<int, GameObject>();

	private static readonly Dictionary<int, DXGameplayVisual> dxGameplayVisuals = new Dictionary<int, DXGameplayVisual>();

	private static bool capturingHotkey;

	private static string customImageStatus = "";

	private static Texture2D customPerfectTexture;

	private static Texture2D customFailedTexture;

	private static readonly List<BreakVisual> breakVisuals = new List<BreakVisual>();

	private static readonly List<string> audioPresetFiles = new List<string>();

	private static float nextAudioPresetScanTime;

	private static readonly string[] LanguageNames = new string[10] { "English", "한국어", "Español", "Polski", "Français", "Tiếng Việt", "中文（简体）", "Русский", "日本語", "Deutsch" };

	private static readonly string[] LocalizationKeys = new string[50]
	{
		"Language", "Sound", "Volume", "Built-in style", "Custom audio", "Supports mp3 / wav / ogg(Vorbis) / aiff / aif, max 30 seconds.", "Preview Break", "Preview Miss", "Effect", "Style",
		"Opacity", "Effect size", "Break color", "Miss color", "Break text", "Miss text", "Custom effect", "Supports png / jpg / jpeg, max 10 MB.", "Show realtime", "Enabled",
		"Layout", "DX-Accuracy | Accuracy", "Accuracy | DX-Accuracy", "Y (from top)", "Size", "Center bottom", "Add DX note shortcut", "Current key combination", "Press key...", "Bind key",
		"Modifiers", "Style preview", "Break", "Miss", "Load", "Browse", "DX note marker", "Show in gameplay", "Edge glow", "Glow intensity",
		"Note floating", "DX note color", "Reset", "Palette", "DX notes master switch", "Color adaptive", "Color variation", "Float amount", "Audio presets", "Add preset"
	};

	private static readonly string[] LocalizationData = new string[8] { "언어\u001f사운드\u001f볼륨\u001f내장 스타일\u001f사용자 오디오\u001fmp3 / wav / ogg(Vorbis) / aiff / aif 지원, 최대 30초.\u001fBreak 미리듣기\u001fMiss 미리듣기\u001f효과\u001f스타일\u001f투명도\u001f효과 크기\u001fBreak 색상\u001fMiss 색상\u001fBreak 텍스트\u001fMiss 텍스트\u001f사용자 효과\u001fpng / jpg / jpeg 지원, 최대 10 MB.\u001f실시간 표시\u001f활성화\u001f배치\u001fDX-Accuracy¦정확도\u001f정확도¦DX-Accuracy\u001fY(위 기준)\u001f크기\u001f하단 중앙\u001fDX 노트 추가 단축키\u001f현재 키 조합\u001f키 입력...\u001f키 지정\u001f조합 키\u001f스타일 미리보기\u001fBreak\u001fMiss\u001f불러오기\u001f찾아보기\u001fDX 노트 표시\u001f게임플레이에서 표시\u001f가장자리 발광\u001f발광 강도", "Idioma\u001fSonido\u001fVolumen\u001fEstilo integrado\u001fAudio personalizado\u001fCompatible con mp3 / wav / ogg(Vorbis) / aiff / aif, máx. 30 s.\u001fVista previa Break\u001fVista previa Miss\u001fEfecto\u001fEstilo\u001fOpacidad\u001fTamaño del efecto\u001fColor Break\u001fColor Miss\u001fTexto Break\u001fTexto Miss\u001fEfecto personalizado\u001fCompatible con png / jpg / jpeg, máx. 10 MB.\u001fMostrar en tiempo real\u001fActivado\u001fDiseño\u001fDX-Accuracy¦Precisión\u001fPrecisión¦DX-Accuracy\u001fY (desde arriba)\u001fTamaño\u001fCentro inferior\u001fAtajo para añadir nota DX\u001fCombinación actual\u001fPulsa una tecla...\u001fAsignar tecla\u001fModificadores\u001fVista previa del estilo\u001fBreak\u001fMiss\u001fCargar\u001fExaminar\u001fMarcador de nota DX\u001fMostrar en juego\u001fResplandor del borde\u001fIntensidad del resplandor", "Język\u001fDźwięk\u001fGłośność\u001fWbudowany styl\u001fWłasne audio\u001fObsługa mp3 / wav / ogg(Vorbis) / aiff / aif, maks. 30 s.\u001fPodgląd Break\u001fPodgląd Miss\u001fEfekt\u001fStyl\u001fKrycie\u001fRozmiar efektu\u001fKolor Break\u001fKolor Miss\u001fTekst Break\u001fTekst Miss\u001fWłasny efekt\u001fObsługa png / jpg / jpeg, maks. 10 MB.\u001fPokazuj na żywo\u001fWłączone\u001fUkład\u001fDX-Accuracy¦Dokładność\u001fDokładność¦DX-Accuracy\u001fY (od góry)\u001fRozmiar\u001fDolny środek\u001fSkrót dodawania nuty DX\u001fBieżąca kombinacja\u001fNaciśnij klawisz...\u001fPrzypisz klawisz\u001fModyfikatory\u001fPodgląd stylu\u001fBreak\u001fMiss\u001fWczytaj\u001fPrzeglądaj\u001fZnacznik nuty DX\u001fPokazuj w grze\u001fPoświata krawędzi\u001fIntensywność poświaty", "Langue\u001fSon\u001fVolume\u001fStyle intégré\u001fAudio personnalisé\u001fPrend en charge mp3 / wav / ogg(Vorbis) / aiff / aif, max 30 s.\u001fAperçu Break\u001fAperçu Miss\u001fEffet\u001fStyle\u001fOpacité\u001fTaille de l’effet\u001fCouleur Break\u001fCouleur Miss\u001fTexte Break\u001fTexte Miss\u001fEffet personnalisé\u001fPrend en charge png / jpg / jpeg, max 10 Mo.\u001fAfficher en temps réel\u001fActivé\u001fDisposition\u001fDX-Accuracy¦Précision\u001fPrécision¦DX-Accuracy\u001fY (depuis le haut)\u001fTaille\u001fCentrer en bas\u001fRaccourci ajouter note DX\u001fCombinaison actuelle\u001fAppuyez sur une touche...\u001fAssigner la touche\u001fModificateurs\u001fAperçu du style\u001fBreak\u001fMiss\u001fCharger\u001fParcourir\u001fMarqueur de note DX\u001fAfficher en jeu\u001fLueur du bord\u001fIntensité de la lueur", "Ngôn ngữ\u001fÂm thanh\u001fÂm lượng\u001fKiểu tích hợp\u001fÂm thanh tùy chỉnh\u001fHỗ trợ mp3 / wav / ogg(Vorbis) / aiff / aif, tối đa 30 giây.\u001fNghe thử Break\u001fNghe thử Miss\u001fHiệu ứng\u001fKiểu\u001fĐộ mờ\u001fKích thước hiệu ứng\u001fMàu Break\u001fMàu Miss\u001fChữ Break\u001fChữ Miss\u001fHiệu ứng tùy chỉnh\u001fHỗ trợ png / jpg / jpeg, tối đa 10 MB.\u001fHiển thị trực tiếp\u001fBật\u001fBố cục\u001fDX-Accuracy¦Độ chính xác\u001fĐộ chính xác¦DX-Accuracy\u001fY (từ trên)\u001fKích thước\u001fCăn giữa dưới\u001fPhím tắt thêm nốt DX\u001fTổ hợp hiện tại\u001fNhấn phím...\u001fGán phím\u001fPhím bổ trợ\u001fXem trước kiểu\u001fBreak\u001fMiss\u001fTải\u001fDuyệt\u001fDấu nốt DX\u001fHiển thị khi chơi\u001fPhát sáng viền\u001fCường độ phát sáng", "Язык\u001fЗвук\u001fГромкость\u001fВстроенный стиль\u001fСвоё аудио\u001fПоддержка mp3 / wav / ogg(Vorbis) / aiff / aif, до 30 с.\u001fПрослушать Break\u001fПрослушать Miss\u001fЭффект\u001fСтиль\u001fПрозрачность\u001fРазмер эффекта\u001fЦвет Break\u001fЦвет Miss\u001fТекст Break\u001fТекст Miss\u001fСвой эффект\u001fПоддержка png / jpg / jpeg, до 10 МБ.\u001fПоказывать в реальном времени\u001fВключено\u001fРаскладка\u001fDX-Accuracy¦Точность\u001fТочность¦DX-Accuracy\u001fY (сверху)\u001fРазмер\u001fПо центру снизу\u001fГорячая клавиша добавления DX\u001fТекущая комбинация\u001fНажмите клавишу...\u001fНазначить клавишу\u001fМодификаторы\u001fПредпросмотр стиля\u001fBreak\u001fMiss\u001fЗагрузить\u001fОбзор\u001fМаркер ноты DX\u001fПоказывать в игре\u001fСвечение края\u001fИнтенсивность свечения", "言語\u001fサウンド\u001f音量\u001f内蔵スタイル\u001fカスタム音声\u001fmp3 / wav / ogg(Vorbis) / aiff / aif 対応、最大30秒。\u001fBreakを試聴\u001fMissを試聴\u001fエフェクト\u001fスタイル\u001f透明度\u001fエフェクトサイズ\u001fBreakの色\u001fMissの色\u001fBreakの文字\u001fMissの文字\u001fカスタムエフェクト\u001fpng / jpg / jpeg 対応、最大10 MB。\u001fリアルタイム表示\u001f有効\u001fレイアウト\u001fDX-Accuracy¦精度\u001f精度¦DX-Accuracy\u001fY（上から）\u001fサイズ\u001f下部中央\u001fDXノート追加ショートカット\u001f現在のキー組み合わせ\u001fキーを押してください...\u001fキーを割り当て\u001f修飾キー\u001fスタイルプレビュー\u001fBreak\u001fMiss\u001f読み込み\u001f参照\u001fDXノート表示\u001fプレイ中に表示\u001fエッジ発光\u001f発光強度", "Sprache\u001fSound\u001fLautstärke\u001fIntegrierter Stil\u001fEigenes Audio\u001fUnterstützt mp3 / wav / ogg(Vorbis) / aiff / aif, max. 30 s.\u001fBreak anhören\u001fMiss anhören\u001fEffekt\u001fStil\u001fDeckkraft\u001fEffektgröße\u001fBreak-Farbe\u001fMiss-Farbe\u001fBreak-Text\u001fMiss-Text\u001fEigener Effekt\u001fUnterstützt png / jpg / jpeg, max. 10 MB.\u001fEchtzeit anzeigen\u001fAktiviert\u001fLayout\u001fDX-Accuracy¦Genauigkeit\u001fGenauigkeit¦DX-Accuracy\u001fY (von oben)\u001fGröße\u001fUnten zentriert\u001fDX-Noten-Shortcut\u001fAktuelle Tastenkombination\u001fTaste drücken...\u001fTaste zuweisen\u001fModifikatoren\u001fStilvorschau\u001fBreak\u001fMiss\u001fLaden\u001fDurchsuchen\u001fDX-Notenmarkierung\u001fIm Spiel anzeigen\u001fRandleuchten\u001fLeuchtstärke" };

	public static bool Load(UnityModManager.ModEntry loadedEntry)
	{
		modEntry = loadedEntry;
		modEntry.OnUnload = OnUnload;
		modEntry.OnGUI = DrawSettingsGUI;
		modEntry.OnSaveGUI = SaveSettingsGUI;
		settings = UnityModManager.ModSettings.Load<AdofaiDxSettings>(modEntry) ?? new AdofaiDxSettings();
		MigrateAndFitCAcuracyPosition();
		MigrateEffectStyle();
		MigrateSoundStyle();
		MigrateMergedSoundPresets();
		MigrateVolume();
		MigrateLanguage();
		if (settings.soundVolume < 0f)
		{
			settings.soundVolume = 0.8f;
		}
		if (settings.effectOpacity < 0f)
		{
			settings.effectOpacity = 0.85f;
		}
		settings.dxNoteColorR = Mathf.Clamp(settings.dxNoteColorR, 0, 255);
		settings.dxNoteColorG = Mathf.Clamp(settings.dxNoteColorG, 0, 255);
		settings.dxNoteColorB = Mathf.Clamp(settings.dxNoteColorB, 0, 255);
		settings.dxColorVariation = Mathf.Clamp(settings.dxColorVariation, 1f, 3f);
		settings.gameplayDXFloatAmplitude = Mathf.Clamp(settings.gameplayDXFloatAmplitude, 1f, 3f);
		InitializeFeedback();
		EnsureAudioPresetFolder();
		overlayObject = new GameObject("AdofaiDxOverlay");
		UnityEngine.Object.DontDestroyOnLoad(overlayObject);
		overlayComponent = overlayObject.AddComponent<AdofaiDxOverlay>();
		AutoLoadCustomAudio();
		AutoLoadCustomEffectImages();
		try
		{
			harmony = new Harmony("ADOFAIDX");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			modEntry.Logger.Log("Break judgement patch applied.");
		}
		catch (Exception ex)
		{
			modEntry.Logger.Error("Break judgement patch failed: " + ex);
		}
		modEntry.Logger.Log("ADOFAI DX loaded. Break marker format: ADOFAIDX.DX");
		return true;
	}

	internal static void UpdateFromOverlay()
	{
		RefreshDetailedResultsDisplayIfNeeded();
		if (settings != null && !settings.enableDXNotes)
		{
			DisableDXRuntimeState();
			return;
		}
		if (IsScreenTransitionActive())
		{
			HideDXForScreenTransition();
			return;
		}
		MaintainCAcuracyScreenReference();
		if (Time.unscaledTime >= nextMarkerRefreshTime)
		{
			nextMarkerRefreshTime = Time.unscaledTime + 0.2f;
			MigrateLegacyFlashMarkers(scnEditor.instance);
			RefreshMarkerCache();
			PruneBreakAttempts();
			SyncDxMarkerObjects();
		}
		UpdateDXGameplayVisuals();
		scnEditor instance = scnEditor.instance;
		if ((UnityEngine.Object)(object)instance == null)
		{
			loggedEditorFound = false;
			return;
		}
		if (!loggedEditorFound)
		{
			loggedEditorFound = true;
			modEntry.Logger.Log("Level editor detected.");
		}
		if (IsToggleShortcutDown())
		{
			ToggleMarkerOnSelectedFloor(instance);
		}
	}

	private static void RefreshDetailedResultsDisplayIfNeeded()
	{
		if (settings == null)
		{
			return;
		}
		scrController controller = scrController.instance;
		DetailedResults detailedResults = (((UnityEngine.Object)(object)controller == null) ? null : controller.detailedResults);
		object textComponent = ((detailedResults == null) ? null : DetailedResultsTextComponentField?.GetValue(detailedResults));
		if (detailedResults == null || textComponent == null || !detailedResults.gameObject.activeInHierarchy)
		{
			lastDetailedResultsStateKey = int.MinValue;
			return;
		}
		int stateKey = (settings.enableDXNotes ? 1 : 0) | (settings.mergeDXAccuracy ? 2 : 0) | (settings.language << 2) | (Persistence.showXAccuracy ? 256 : 0);
		if (stateKey == lastDetailedResultsStateKey)
		{
			return;
		}
		string[] results = DetailedResultsResultsField?.GetValue(detailedResults) as string[];
		scrPlayerManager playerManager = scrPlayerManager.instance;
		scrPlayer[] players = ((playerManager == null) ? null : playerManager.players);
		if (results == null || players == null || players.Length == 0)
		{
			return;
		}
		int playerIndex = ((DetailedResultsPlayerIndexField == null) ? 0 : Convert.ToInt32(DetailedResultsPlayerIndexField.GetValue(detailedResults)));
		playerIndex = Mathf.Clamp(playerIndex, 0, players.Length - 1);
		scrPlayer player = players[playerIndex];
		if ((UnityEngine.Object)(object)player == null || player.marginTracker == null)
		{
			return;
		}
		if (DetailedResultsGenerateResultsMethod == null)
		{
			return;
		}
		string resultText;
		try
		{
			resultText = DetailedResultsGenerateResultsMethod.Invoke(detailedResults, new object[1] { player.marginTracker }) as string;
		}
		catch (Exception ex)
		{
			modEntry.Logger.Error("Failed to refresh detailed results: " + ex);
			return;
		}
		if (resultText == null)
		{
			return;
		}
		System.Reflection.PropertyInfo textProperty = textComponent.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
		if (textProperty == null)
		{
			return;
		}
		results[playerIndex] = resultText;
		textProperty.SetValue(textComponent, resultText, null);
		lastDetailedResultsStateKey = stateKey;
		modEntry.Logger.Log("Refreshed detailed results after settings changed (mod=" + settings.enableDXNotes + ", merge=" + settings.mergeDXAccuracy + ", player=" + playerIndex + ").");
	}

	internal static void DrawOverlay()
	{
		scnEditor instance = scnEditor.instance;
		if (Event.current == null || (settings != null && !settings.enableDXNotes) || IsScreenTransitionActive())
		{
			return;
		}
		Matrix4x4 matrix = GUI.matrix;
		int depth = GUI.depth;
		Color color = GUI.color;
		try
		{
			GUI.matrix = Matrix4x4.identity;
			GUI.depth = -1000;
			GUI.color = Color.white;
			EnsureStyles();
			if (!IsGameplayActive() && (UnityEngine.Object)(object)instance != null)
			{
				DrawEditorControl(instance);
				if (markedFloors.Count > 0)
				{
					DrawEditorInfo();
				}
			}
			if (IsGameplayActive())
			{
				DrawGameplayInfo();
			}
			DrawNotice();
			DrawBreakVisuals();
		}
		finally
		{
			GUI.matrix = matrix;
			GUI.depth = depth;
			GUI.color = color;
		}
	}

	private static bool IsScreenTransitionActive()
	{
		if (!screenTransitionActive)
		{
			return false;
		}
		if (Time.unscaledTime > screenTransitionDeadline)
		{
			screenTransitionActive = false;
			return false;
		}
		return true;
	}

	internal static void BeginScreenTransition()
	{
		screenTransitionActive = true;
		screenTransitionDeadline = Time.unscaledTime + 20f;
		HideDXForScreenTransition();
	}

	internal static void EndScreenTransition()
	{
		screenTransitionActive = false;
		screenTransitionDeadline = 0f;
	}

	private static void HideDXForScreenTransition()
	{
		DestroyDXGameplayVisuals(restoreColors: true);
		DestroyDxMarkerObjects();
		breakVisuals.Clear();
		activeColorPicker = 0;
	}

	private static bool OnUnload(UnityModManager.ModEntry entry)
	{
		settings?.Save(entry);
		if (overlayObject != null)
		{
			UnityEngine.Object.Destroy(overlayObject);
		}
		if (feedbackObject != null)
		{
			UnityEngine.Object.Destroy(feedbackObject);
		}
		DestroyDXGameplayVisuals(restoreColors: true);
		DestroyDxMarkerObjects();
		harmony?.UnpatchAll("ADOFAIDX");
		overlayObject = null;
		overlayComponent = null;
		feedbackObject = null;
		feedbackAudioSource = null;
		harmony = null;
		trackedLevelData = null;
		legacyMigrationCheckedLevel = null;
		breakAttempts.Clear();
		markedFloors.Clear();
		badgeStyle = null;
		infoStyle = null;
		noticeStyle = null;
		breakFeedbackStyle = null;
		cAccuracyStyle = null;
		foldoutTriangleStyle = null;
		settingsSectionStyle = null;
		if (roundedPanelTexture != null)
		{
			UnityEngine.Object.Destroy(roundedPanelTexture);
		}
		if (softGlowTexture != null)
		{
			UnityEngine.Object.Destroy(softGlowTexture);
		}
		if (ringGlowTexture != null)
		{
			UnityEngine.Object.Destroy(ringGlowTexture);
		}
		if (spectrumPickerTexture != null)
		{
			UnityEngine.Object.Destroy(spectrumPickerTexture);
		}
		if (dxEdgeMaterial != null)
		{
			UnityEngine.Object.Destroy(dxEdgeMaterial);
		}
		if (dxGlowMaterial != null)
		{
			UnityEngine.Object.Destroy(dxGlowMaterial);
		}
		roundedPanelTexture = null;
		softGlowTexture = null;
		ringGlowTexture = null;
		spectrumPickerTexture = null;
		dxEdgeMaterial = null;
		dxGlowMaterial = null;
		loggedDxVisualInfo = false;
		loggedGameplayDxText = false;
		screenTransitionActive = false;
		screenTransitionDeadline = 0f;
		noticeText = null;
		if (customPerfectTexture != null)
		{
			UnityEngine.Object.Destroy(customPerfectTexture);
		}
		if (customFailedTexture != null)
		{
			UnityEngine.Object.Destroy(customFailedTexture);
		}
		customPerfectTexture = null;
		customFailedTexture = null;
		customPerfectClip = null;
		customFailedClip = null;
		customAudioStatus = "";
		breakVisuals.Clear();
		return true;
	}

	private static bool IsToggleShortcutDown()
	{
		bool flag = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
		bool flag2 = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		bool flag3 = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
		KeyCode hotkeyKeyCode = (KeyCode)settings.hotkeyKeyCode;
		if (hotkeyKeyCode != KeyCode.None && flag == settings.hotkeyCtrl && flag2 == settings.hotkeyShift && flag3 == settings.hotkeyAlt)
		{
			return Input.GetKeyDown(hotkeyKeyCode);
		}
		return false;
	}

	private static string GetShortcutDisplay()
	{
		List<string> list = new List<string>();
		if (settings.hotkeyCtrl)
		{
			list.Add("Ctrl");
		}
		if (settings.hotkeyShift)
		{
			list.Add("Shift");
		}
		if (settings.hotkeyAlt)
		{
			list.Add("Alt");
		}
		KeyCode hotkeyKeyCode = (KeyCode)settings.hotkeyKeyCode;
		list.Add(hotkeyKeyCode.ToString());
		return string.Join("+", list);
	}

	internal static bool IsGameplayActive()
	{
		scnEditor instance = scnEditor.instance;
		if ((UnityEngine.Object)(object)instance != null)
		{
			return instance.playMode;
		}
		scrController instance2 = scrController.instance;
		if ((UnityEngine.Object)(object)instance2 == null || !instance2.gameworld)
		{
			return false;
		}
		scrConductor instance3 = scrConductor.instance;
		if ((UnityEngine.Object)(object)instance3 == null || !instance3.hasSongStarted)
		{
			return false;
		}
		scrPlayer firstPlayer = GetFirstPlayer();
		if ((UnityEngine.Object)(object)firstPlayer != null)
		{
			return firstPlayer.responsive;
		}
		return false;
	}

	private static scrPlayer GetFirstPlayer()
	{
		scrPlayerManager instance = scrPlayerManager.instance;
		if ((UnityEngine.Object)(object)instance == null || instance.players == null)
		{
			return null;
		}
		return instance.players.FirstOrDefault((scrPlayer p) => (UnityEngine.Object)(object)p != null);
	}

	private static void ShowNotice(string message, bool error = false)
	{
		noticeText = message;
		noticeError = error;
		noticeUntil = Time.unscaledTime + 2.5f;
	}

	private static void DrawNotice()
	{
		if (!string.IsNullOrEmpty(noticeText) && !(Time.unscaledTime > noticeUntil))
		{
			EnsureVisualTextures();
			float num = Mathf.Clamp((float)Screen.width / 1920f, 0.75f, 1.5f);
			noticeStyle.fontSize = Mathf.RoundToInt(18f * num);
			Vector2 vector = noticeStyle.CalcSize(new GUIContent(noticeText));
			float num2 = Mathf.Min((float)Screen.width - 40f * num, Mathf.Max(300f * num, vector.x + 64f * num));
			float height = 48f * num;
			float num3 = Mathf.Clamp01((noticeUntil - Time.unscaledTime) / 0.25f);
			Rect position = new Rect(((float)Screen.width - num2) * 0.5f + 4f * num, 76f * num, num2, height);
			Rect position2 = new Rect(((float)Screen.width - num2) * 0.5f, 72f * num, num2, height);
			GUI.color = new Color(0f, 0f, 0f, 0.35f * num3);
			GUI.DrawTexture(position, roundedPanelTexture, ScaleMode.StretchToFill, alphaBlend: true);
			GUI.color = new Color(0.035f, 0.045f, 0.065f, 0.94f * num3);
			GUI.DrawTexture(position2, roundedPanelTexture, ScaleMode.StretchToFill, alphaBlend: true);
			GUI.color = (noticeError ? new Color(1f, 0.28f, 0.22f, num3) : new Color(0.2f, 1f, 0.65f, num3));
			GUI.DrawTexture(new Rect(position2.x + 6f * num, position2.y + 8f * num, 5f * num, position2.height - 16f * num), Texture2D.whiteTexture);
			GUI.color = new Color(0f, 0f, 0f, 0.8f * num3);
			GUI.Label(new Rect(position2.x + 1.5f * num, position2.y + 1.5f * num, position2.width, position2.height), noticeText, noticeStyle);
			GUI.color = new Color(1f, 1f, 1f, num3);
			GUI.Label(position2, noticeText, noticeStyle);
			GUI.color = Color.white;
		}
	}

	private static void MigrateLanguage()
	{
		if (settings != null && settings.languageVersion <= 0)
		{
			settings.language = ((settings.language != 1) ? 6 : 0);
			settings.languageVersion = 1;
		}
	}

	private static string L(string chinese, string english)
	{
		return Localized(chinese, english);
	}

	internal static string Localized(string chinese, string english)
	{
		int num = ((settings != null) ? settings.language : 0);
		switch (num)
		{
		case 0:
			return english;
		case 6:
			return chinese;
		default:
		{
			if (english == "Note floating")
			{
				switch (num)
				{
				case 1:
					return "노트 부유";
				case 2:
					return "Flotación de nota";
				case 3:
					return "Lewitacja nuty";
				case 4:
					return "Flottement de la note";
				case 5:
					return "Hiệu ứng nổi";
				case 7:
					return "Парение ноты";
				case 8:
					return "ノートの浮遊";
				case 9:
					return "Notenschweben";
				}
			}
			if (english == "DX note color")
			{
				switch (num)
				{
				case 1:
					return "DX 노트 색상";
				case 2:
					return "Color de nota DX";
				case 3:
					return "Kolor nuty DX";
				case 4:
					return "Couleur de la note DX";
				case 5:
					return "Màu nốt DX";
				case 7:
					return "Цвет ноты DX";
				case 8:
					return "DXノートの色";
				case 9:
					return "DX-Notenfarbe";
				}
			}
			if (english == "Reset")
			{
				switch (num)
				{
				case 1:
					return "초기화";
				case 2:
					return "Restablecer";
				case 3:
					return "Resetuj";
				case 4:
					return "Réinitialiser";
				case 5:
					return "Đặt lại";
				case 7:
					return "Сброс";
				case 8:
					return "リセット";
				case 9:
					return "Zurücksetzen";
				}
			}
			if (english == "Palette")
			{
				switch (num)
				{
				case 1:
					return "팔레트";
				case 2:
					return "Paleta";
				case 3:
					return "Paleta";
				case 4:
					return "Palette";
				case 5:
					return "Bảng màu";
				case 7:
					return "Палитра";
				case 8:
					return "パレット";
				case 9:
					return "Farbpalette";
				}
			}
			if (english == "DX notes master switch")
			{
				switch (num)
				{
				case 1:
					return "DX 노트 전체 스위치";
				case 2:
					return "Interruptor general de notas DX";
				case 3:
					return "Główny przełącznik nut DX";
				case 4:
					return "Interrupteur général des notes DX";
				case 5:
					return "Công tắc tổng nốt DX";
				case 7:
					return "Общий переключатель нот DX";
				case 8:
					return "DXノート全体スイッチ";
				case 9:
					return "DX-Noten-Hauptschalter";
				}
			}
			if (english == "Color adaptive")
			{
				switch (num)
				{
				case 1:
					return "노트 색상 자동 적용";
				case 2:
					return "Color automático";
				case 3:
					return "Dopasowanie koloru";
				case 4:
					return "Couleur adaptative";
				case 5:
					return "Màu tự động";
				case 7:
					return "Адаптивный цвет";
				case 8:
					return "色を自動適用";
				case 9:
					return "Farbe automatisch übernehmen";
				}
			}
			if (english == "Color variation")
			{
				switch (num)
				{
				case 1:
					return "색상 변화 강도";
				case 2:
					return "Variación de color";
				case 3:
					return "Zmienność koloru";
				case 4:
					return "Variation de couleur";
				case 5:
					return "Mức biến đổi màu";
				case 7:
					return "Амплитуда цвета";
				case 8:
					return "色の変化量";
				case 9:
					return "Farbvariation";
				}
			}
			if (english == "Float amount")
			{
				switch (num)
				{
				case 1:
					return "노트 부유 강도";
				case 2:
					return "Amplitud de flotación";
				case 3:
					return "Amplituda unoszenia nuty";
				case 4:
					return "Amplitude de flottement";
				case 5:
					return "Biên độ nổi";
				case 7:
					return "Амплитуда парения";
				case 8:
					return "ノート浮遊量";
				case 9:
					return "Notenschwebe-Stärke";
				}
			}
			if (english == "Merge DX accuracy")
			{
				switch (num)
				{
				case 1:
					return "DX 정확도 합치기";
				case 2:
					return "Combinar precisión DX";
				case 3:
					return "Scal dokładność DX";
				case 4:
					return "Fusionner la précision DX";
				case 5:
					return "Gộp độ chính xác DX";
				case 7:
					return "Объединять точность DX";
				case 8:
					return "DX精度を合算";
				case 9:
					return "DX-Genauigkeit zusammenführen";
				}
			}
			if (english == "Mod master switch")
			{
				switch (num)
				{
				case 1:
					return "모드 전체 스위치";
				case 2:
					return "Interruptor general del mod";
				case 3:
					return "Główny przełącznik moda";
				case 4:
					return "Interrupteur général du mod";
				case 5:
					return "Công tắc tổng của mod";
				case 7:
					return "Общий переключатель мода";
				case 8:
					return "MOD全体スイッチ";
				case 9:
					return "Mod-Hauptschalter";
				}
			}
			if (english == "Audio presets")
			{
				switch (num)
				{
				case 1:
					return "오디오 프리셋";
				case 2:
					return "Preajustes de audio";
				case 3:
					return "Presety audio";
				case 4:
					return "Préréglages audio";
				case 5:
					return "Âm thanh đặt trước";
				case 7:
					return "Аудиопресеты";
				case 8:
					return "オーディオプリセット";
				case 9:
					return "Audio-Voreinstellungen";
				}
			}
			if (english == "Add preset")
			{
				switch (num)
				{
				case 1:
					return "프리셋 추가";
				case 2:
					return "Añadir preajuste";
				case 3:
					return "Dodaj preset";
				case 4:
					return "Ajouter un préréglage";
				case 5:
					return "Thêm đặt trước";
				case 7:
					return "Добавить пресет";
				case 8:
					return "プリセットを追加";
				case 9:
					return "Voreinstellung hinzufügen";
				}
			}
			int num2 = ((num < 6) ? (num - 1) : (num - 2));
			if (num2 < 0 || num2 >= LocalizationData.Length)
			{
				return english;
			}
			int num3 = Array.IndexOf(LocalizationKeys, english);
			if (num3 < 0)
			{
				return english;
			}
			string[] array = LocalizationData[num2].Split(new char[1] { '\u001f' });
			if (num3 >= array.Length || string.IsNullOrEmpty(array[num3]))
			{
				return english;
			}
			return array[num3];
		}
		}
	}

	private static void MigrateVolume()
	{
		if (settings != null && !settings.volumeMerged)
		{
			settings.soundVolume = Mathf.Clamp01(Mathf.Max(settings.perfectSoundVolume, settings.failedSoundVolume));
			settings.volumeMerged = true;
		}
	}

	private static void MigrateSoundStyle()
	{
		if (settings == null || settings.soundStyleVersion >= 2)
		{
			return;
		}
		if (settings.soundStyleVersion == 1)
		{
			if (settings.soundStyle == 4)
			{
				settings.soundStyle = 2;
			}
			else if (settings.soundStyle == 2 || settings.soundStyle == 3)
			{
				settings.soundStyle = 0;
			}
		}
		settings.soundStyleVersion = 2;
	}

	private static void MigrateMergedSoundPresets()
	{
		if (settings != null && settings.mergedSoundPresetVersion < 1)
		{
			int num = Mathf.Clamp(settings.soundStyle, 0, 2);
			settings.perfectBuiltinSound = num;
			settings.failedBuiltinSound = num;
			settings.mergedSoundPresetVersion = 1;
		}
	}

	private static void MigrateEffectStyle()
	{
		if (settings != null && settings.effectStyleVersion <= 0)
		{
			if (settings.effectStyle == 1)
			{
				settings.effectStyle = 2;
			}
			else if (settings.effectStyle == 2)
			{
				settings.effectStyle = 4;
			}
			else
			{
				settings.effectStyle = 0;
			}
			settings.effectStyleVersion = 1;
		}
	}

	private static void MigrateAndFitCAcuracyPosition()
	{
		if (settings == null)
		{
			return;
		}
		if (!settings.cAccuracyPositionMigrated)
		{
			if (settings.cAccuracyX <= 1f && settings.cAccuracyY <= 1f)
			{
				settings.cAccuracyX *= Screen.width;
				settings.cAccuracyY *= Screen.height;
			}
			settings.cAccuracyReferenceWidth = Mathf.Max(1, Screen.width);
			settings.cAccuracyReferenceHeight = Mathf.Max(1, Screen.height);
			settings.cAccuracyPositionMigrated = true;
		}
		else
		{
			MaintainCAcuracyScreenReference();
		}
	}

	private static void MaintainCAcuracyScreenReference()
	{
		if (settings != null)
		{
			int cAccuracyReferenceWidth = settings.cAccuracyReferenceWidth;
			int cAccuracyReferenceHeight = settings.cAccuracyReferenceHeight;
			if (cAccuracyReferenceWidth <= 0 || cAccuracyReferenceHeight <= 0)
			{
				settings.cAccuracyReferenceWidth = Mathf.Max(1, Screen.width);
				settings.cAccuracyReferenceHeight = Mathf.Max(1, Screen.height);
			}
			else if (cAccuracyReferenceWidth != Screen.width || cAccuracyReferenceHeight != Screen.height)
			{
				settings.cAccuracyX *= (float)Screen.width / (float)cAccuracyReferenceWidth;
				settings.cAccuracyY *= (float)Screen.height / (float)cAccuracyReferenceHeight;
				settings.cAccuracyReferenceWidth = Mathf.Max(1, Screen.width);
				settings.cAccuracyReferenceHeight = Mathf.Max(1, Screen.height);
			}
		}
	}

	private static bool DrawFoldout(bool expanded, string title)
	{
		if (foldoutTriangleStyle == null)
		{
			foldoutTriangleStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontSize = 18,
				fontStyle = FontStyle.Bold,
				normal = 
				{
					textColor = new Color(1f, 0.68f, 0.25f)
				}
			};
		}
		if (settingsSectionStyle == null)
		{
			settingsSectionStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 14,
				fontStyle = FontStyle.Bold,
				normal = 
				{
					textColor = new Color(1f, 0.82f, 0.55f)
				}
			};
		}
		GUILayout.BeginHorizontal();
		if (GUILayout.Button(expanded ? "▾" : "▸", foldoutTriangleStyle, GUILayout.Width(24f), GUILayout.Height(22f)))
		{
			expanded = !expanded;
		}
		GUILayout.Label(title, settingsSectionStyle, GUILayout.Width(300f));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		return expanded;
	}

	private static void DisableDXRuntimeState()
	{
		DestroyDXGameplayVisuals(restoreColors: true);
		DestroyDxMarkerObjects();
		breakVisuals.Clear();
		if (!IsDetailedResultsVisible())
		{
			breakAttempts.Clear();
			markedFloors.Clear();
		}
		lastReportedMarkerCount = -1;
		activeColorPicker = 0;
	}

	private static bool IsDetailedResultsVisible()
	{
		scrController controller = scrController.instance;
		DetailedResults detailedResults = (((UnityEngine.Object)(object)controller == null) ? null : controller.detailedResults);
		return detailedResults != null && detailedResults.gameObject.activeInHierarchy;
	}

	private static void ResetCAcuracyPosition()
	{
		if (settings != null)
		{
			float num = Mathf.Clamp(settings.cAccuracyScale, 0.5f, 3f);
			Vector2 vector = new GUIStyle(GUI.skin.label)
			{
				fontSize = Mathf.RoundToInt(20f * num)
			}.CalcSize(new GUIContent("DX-Accuracy | 100.00%"));
			vector.x += 28f * num;
			vector.y = Mathf.Max(vector.y + 12f * num, 34f * num);
			settings.cAccuracyX = Mathf.Max(0f, ((float)Screen.width - vector.x) * 0.5f);
			settings.cAccuracyY = Mathf.Max(0f, (float)Screen.height - vector.y - 24f * num);
		}
	}

	private static void DrawLanguageSelector()
	{
		GUIStyle button = GUI.skin.button;
		for (int i = 0; i < LanguageNames.Length; i++)
		{
			string text = LanguageNames[i];
			float width = button.CalcSize(new GUIContent(text)).x + 4f;
			Color backgroundColor = GUI.backgroundColor;
			if (settings.language == i)
			{
				GUI.backgroundColor = new Color(0.28f, 0.62f, 0.95f, 1f);
			}
			if (GUILayout.Button(text, GUILayout.Width(width), GUILayout.Height(22f)))
			{
				settings.language = i;
			}
			GUI.backgroundColor = backgroundColor;
		}
	}

	private static void DrawSettingsGUI(UnityModManager.ModEntry entry)
	{
		if (settings == null)
		{
			settings = new AdofaiDxSettings();
		}
		if (capturingHotkey && Event.current != null && Event.current.type == EventType.KeyDown)
		{
			KeyCode keyCode = Event.current.keyCode;
			if (keyCode != KeyCode.None && keyCode != KeyCode.LeftControl && keyCode != KeyCode.RightControl && keyCode != KeyCode.LeftShift && keyCode != KeyCode.RightShift && keyCode != KeyCode.LeftAlt && keyCode != KeyCode.RightAlt)
			{
				settings.hotkeyKeyCode = (int)keyCode;
				capturingHotkey = false;
				Event.current.Use();
			}
		}
		GUILayout.BeginHorizontal();
		GUILayout.Label(L("MOD 总开关", "Mod master switch"), GUILayout.Width(220f));
		settings.enableDXNotes = GUILayout.Toggle(settings.enableDXNotes, L("启用", "Enabled"), GUILayout.Width(120f));
		GUILayout.EndHorizontal();
		if (!settings.enableDXNotes)
		{
			capturingHotkey = false;
			DisableDXRuntimeState();
			return;
		}
		GUILayout.BeginHorizontal();
		GUILayout.Label(L("合并 DX 精准度", "Merge DX accuracy"), GUILayout.Width(180f));
		settings.mergeDXAccuracy = GUILayout.Toggle(settings.mergeDXAccuracy, L("启用", "Enabled"), GUILayout.Width(100f));
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal();
		GUILayout.Label(L("语言", "Language"), GUILayout.Width(70f));
		DrawLanguageSelector();
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		settings.sectionSound = DrawFoldout(settings.sectionSound, L("音效", "Sound"));
		if (settings.sectionSound)
		{
			if (!IsMergedSoundMuted(perfect: true) || !IsMergedSoundMuted(perfect: false))
			{
				settings.soundVolume = DrawPercentRow(L("音量", "Volume"), settings.soundVolume);
			}
			DrawMergedSoundPresetSection();
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(L("试听 break", "Preview Break"), GUILayout.Width(150f)))
			{
				PlayBreakSound(perfect: true);
			}
			if (GUILayout.Button(L("试听 miss", "Preview Miss"), GUILayout.Width(150f)))
			{
				PlayBreakSound(perfect: false);
			}
			GUILayout.EndHorizontal();
		}
		settings.sectionEffect = DrawFoldout(settings.sectionEffect, L("特效", "Effect"));
		if (settings.sectionEffect)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(L("样式", "Style"), GUILayout.Width(150f));
			settings.effectStyle = GUILayout.Toolbar(settings.effectStyle, new string[4] { "Flash", "Ring", "Text", "Off" }, GUILayout.Width(360f));
			GUILayout.EndHorizontal();
			if (settings.effectStyle != 3)
			{
				settings.effectOpacity = DrawPercentRow(L("透明度", "Opacity"), settings.effectOpacity);
				settings.effectScale = DrawSizeRow(L("特效大小", "Effect size"), settings.effectScale);
				DrawEffectColorRow(L("break 颜色", "Break color"), ref settings.perfectEffectColor, 1);
				DrawEffectColorRow(L("miss 颜色", "Miss color"), ref settings.failedEffectColor, 2);
				GUILayout.BeginHorizontal();
				GUILayout.Label(L("break 文字", "Break text"), GUILayout.Width(150f));
				settings.perfectEffectText = GUILayout.TextField(settings.perfectEffectText ?? "", GUILayout.Width(240f));
				GUILayout.EndHorizontal();
				GUILayout.BeginHorizontal();
				GUILayout.Label(L("miss 文字", "Miss text"), GUILayout.Width(150f));
				settings.failedEffectText = GUILayout.TextField(settings.failedEffectText ?? "", GUILayout.Width(240f));
				GUILayout.EndHorizontal();
				DrawEffectStylePreview();
				settings.sectionCustomImage = DrawFoldout(settings.sectionCustomImage, L("自定义特效", "Custom effect"));
				if (settings.sectionCustomImage)
				{
					GUILayout.Label(L("支持 png / jpg / jpeg，最大 10 MB。", "Supports png / jpg / jpeg, max 10 MB."), GUILayout.Width(500f));
					DrawCustomEffectImageRow(perfect: true);
					DrawCustomEffectImageRow(perfect: false);
					if (!string.IsNullOrEmpty(customImageStatus) && (settings.useCustomPerfectImage || settings.useCustomFailedImage))
					{
						GUILayout.Label(customImageStatus);
					}
				}
			}
		}
		settings.sectionMarker = DrawFoldout(settings.sectionMarker, L("DX 音符标识", "DX note marker"));
		if (settings.sectionMarker)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(L("游玩中显示", "Show in gameplay"), GUILayout.Width(180f));
			settings.showGameplayDXMarker = GUILayout.Toggle(settings.showGameplayDXMarker, L("启用", "Enabled"), GUILayout.Width(100f));
			GUILayout.EndHorizontal();
			if (settings.showGameplayDXMarker)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(L("颜色自适应", "Color adaptive"), GUILayout.Width(180f));
				settings.gameplayDXColorAdaptive = GUILayout.Toggle(settings.gameplayDXColorAdaptive, L("启用", "Enabled"), GUILayout.Width(100f));
				GUILayout.EndHorizontal();
				settings.dxColorVariation = DrawMultiplierRow(L("颜色浮动幅度", "Color variation"), settings.dxColorVariation);
				if (!settings.gameplayDXColorAdaptive)
				{
					DrawDXNoteColorRow();
				}
				else
				{
					activeColorPicker = 0;
				}
				GUILayout.BeginHorizontal();
				GUILayout.Label(L("音符浮动", "Note floating"), GUILayout.Width(180f));
				settings.gameplayDXFloat = GUILayout.Toggle(settings.gameplayDXFloat, L("启用", "Enabled"), GUILayout.Width(100f));
				GUILayout.EndHorizontal();
				if (settings.gameplayDXFloat)
				{
					settings.gameplayDXFloatAmplitude = DrawMultiplierRow(L("漂浮幅度", "Float amount"), settings.gameplayDXFloatAmplitude);
				}
				GUILayout.BeginHorizontal();
				GUILayout.Label(L("边缘发光", "Edge glow"), GUILayout.Width(180f));
				settings.gameplayDXGlow = GUILayout.Toggle(settings.gameplayDXGlow, L("启用", "Enabled"), GUILayout.Width(100f));
				GUILayout.EndHorizontal();
				if (settings.gameplayDXGlow)
				{
					settings.gameplayDXGlowIntensity = DrawPercentRow(L("发光强度", "Glow intensity"), settings.gameplayDXGlowIntensity);
				}
			}
			else
			{
				activeColorPicker = 0;
			}
		}
		settings.sectionCAcuracy = DrawFoldout(settings.sectionCAcuracy, "DX-Accuracy UI");
		if (settings.sectionCAcuracy)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(L("实时显示", "Show realtime"), GUILayout.Width(150f));
			settings.showCAcuracy = GUILayout.Toggle(settings.showCAcuracy, L("启用", "Enabled"), GUILayout.Width(100f));
			GUILayout.EndHorizontal();
			if (settings.showCAcuracy)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(L("排列", "Layout"), GUILayout.Width(150f));
				settings.cAccuracyLayout = GUILayout.Toolbar(settings.cAccuracyLayout, new string[2]
				{
					L("DX-Accuracy | 准度", "DX-Accuracy | Accuracy"),
					L("准度 | DX-Accuracy", "Accuracy | DX-Accuracy")
				}, GUILayout.Width(360f));
				GUILayout.EndHorizontal();
				settings.cAccuracyX = DrawPixelRow("X", settings.cAccuracyX, Screen.width);
				settings.cAccuracyY = DrawPixelRow(L("Y", "Y"), settings.cAccuracyY, Screen.height);
				settings.cAccuracyScale = DrawSizeRow(L("大小", "Size"), settings.cAccuracyScale);
				GUILayout.BeginHorizontal();
				GUILayout.Space(150f);
				if (GUILayout.Button(L("居中底部", "Center bottom"), GUILayout.Width(130f)))
				{
					ResetCAcuracyPosition();
				}
				GUILayout.EndHorizontal();
			}
		}
		settings.sectionShortcut = DrawFoldout(settings.sectionShortcut, L("添加 DX 音符快捷键", "Add DX note shortcut"));
		if (settings.sectionShortcut)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(L("当前按键组合", "Current key combination"), GUILayout.Width(150f));
			GUILayout.Label(GetShortcutDisplay(), GUILayout.Width(180f));
			if (GUILayout.Button(capturingHotkey ? L("请按键...", "Press key...") : L("绑定按键", "Bind key"), GUILayout.Width(130f)))
			{
				capturingHotkey = true;
			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.Label(L("组合键", "Modifiers"), GUILayout.Width(150f));
			settings.hotkeyCtrl = GUILayout.Toggle(settings.hotkeyCtrl, "Ctrl", GUILayout.Width(75f));
			settings.hotkeyShift = GUILayout.Toggle(settings.hotkeyShift, "Shift", GUILayout.Width(75f));
			settings.hotkeyAlt = GUILayout.Toggle(settings.hotkeyAlt, "Alt", GUILayout.Width(70f));
			GUILayout.EndHorizontal();
		}
	}

	private static float DrawPercentRow(string label, float value)
	{
		int num = Mathf.RoundToInt(value * 100f);
		GUILayout.BeginHorizontal();
		GUILayout.Label(label, GUILayout.Width(150f));
		num = Mathf.RoundToInt(GUILayout.HorizontalSlider(num, 0f, 100f, GUILayout.Width(260f)));
		if (int.TryParse(GUILayout.TextField(num.ToString(), GUILayout.Width(58f)), out var result))
		{
			num = Mathf.Clamp(result, 0, 100);
		}
		GUILayout.Label("%", GUILayout.Width(20f));
		GUILayout.EndHorizontal();
		return (float)num / 100f;
	}

	private static float DrawPixelRow(string label, float value, float maximum)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(label, GUILayout.Width(150f));
		value = GUILayout.HorizontalSlider(value, 0f, maximum, GUILayout.Width(260f));
		if (float.TryParse(GUILayout.TextField(Mathf.RoundToInt(value).ToString(), GUILayout.Width(72f)), out var result))
		{
			value = Mathf.Clamp(result, 0f, maximum);
		}
		GUILayout.Label("px", GUILayout.Width(24f));
		GUILayout.EndHorizontal();
		return value;
	}

	private static float DrawSizeRow(string label, float value)
	{
		int num = Mathf.RoundToInt(value * 100f);
		GUILayout.BeginHorizontal();
		GUILayout.Label(label, GUILayout.Width(150f));
		num = Mathf.RoundToInt(GUILayout.HorizontalSlider(num, 50f, 300f, GUILayout.Width(260f)));
		if (int.TryParse(GUILayout.TextField(num.ToString(), GUILayout.Width(58f)), out var result))
		{
			num = Mathf.Clamp(result, 50, 300);
		}
		GUILayout.Label("%", GUILayout.Width(20f));
		GUILayout.EndHorizontal();
		return (float)num / 100f;
	}

	private static float DrawMultiplierRow(string label, float value)
	{
		int num = Mathf.RoundToInt(value * 100f);
		GUILayout.BeginHorizontal();
		GUILayout.Label(label, GUILayout.Width(180f));
		num = Mathf.RoundToInt(GUILayout.HorizontalSlider(num, 100f, 300f, GUILayout.Width(240f)));
		if (int.TryParse(GUILayout.TextField(num.ToString(), GUILayout.Width(58f)), out var result))
		{
			num = Mathf.Clamp(result, 100, 300);
		}
		GUILayout.Label("%", GUILayout.Width(20f));
		GUILayout.EndHorizontal();
		return (float)num / 100f;
	}

	private static void DrawDXNoteColorRow()
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(L("DX 音符颜色", "DX note color"), GUILayout.Width(180f));
		GUILayout.Label("R", GUILayout.Width(18f));
		if (int.TryParse(GUILayout.TextField(settings.dxNoteColorR.ToString(), GUILayout.Width(52f)), out var result))
		{
			settings.dxNoteColorR = Mathf.Clamp(result, 0, 255);
		}
		GUILayout.Label("G", GUILayout.Width(18f));
		if (int.TryParse(GUILayout.TextField(settings.dxNoteColorG.ToString(), GUILayout.Width(52f)), out var result2))
		{
			settings.dxNoteColorG = Mathf.Clamp(result2, 0, 255);
		}
		GUILayout.Label("B", GUILayout.Width(18f));
		if (int.TryParse(GUILayout.TextField(settings.dxNoteColorB.ToString(), GUILayout.Width(52f)), out var result3))
		{
			settings.dxNoteColorB = Mathf.Clamp(result3, 0, 255);
		}
		if (GUILayout.Button(L("重置", "Reset"), GUILayout.Width(70f)))
		{
			settings.dxNoteColorR = 255;
			settings.dxNoteColorG = 107;
			settings.dxNoteColorB = 13;
		}
		if (GUILayout.Button(L("调色盘", "Palette"), GUILayout.Width(86f)))
		{
			activeColorPicker = ((activeColorPicker != 3) ? 3 : 0);
		}
		GUILayout.EndHorizontal();
		if (activeColorPicker == 3)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Space(180f);
			Color color = DrawSpectrumPicker(GetDXNoteBaseColor(), 380f, 120f);
			GUILayout.EndHorizontal();
			settings.dxNoteColorR = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
			settings.dxNoteColorG = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
			settings.dxNoteColorB = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
		}
	}

	private static void DrawEffectColorRow(string label, ref string colorText, int target)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(label, GUILayout.Width(150f));
		colorText = GUILayout.TextField(colorText ?? "", GUILayout.Width(110f));
		if (DrawExactColorButton(TryParseEffectColor(colorText, out var color) ? color : Color.white, 30f, 18f))
		{
			activeColorPicker = ((activeColorPicker != target) ? target : 0);
		}
		GUILayout.EndHorizontal();
		if (activeColorPicker == target)
		{
			DrawColorPalette(ref colorText);
		}
	}

	private static Color DrawSpectrumPicker(Color current, float width, float height)
	{
		EnsureSpectrumPickerTexture();
		Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
		GUI.color = Color.white;
		GUI.DrawTexture(rect, spectrumPickerTexture, ScaleMode.StretchToFill, alphaBlend: true);
		Event current2 = Event.current;
		if (current2 != null && current2.type == EventType.MouseDown && current2.button == 0 && rect.Contains(current2.mousePosition))
		{
			float h = Mathf.Clamp01((current2.mousePosition.x - rect.x) / rect.width);
			float num = Mathf.Clamp01((current2.mousePosition.y - rect.y) / rect.height);
			float s;
			float v;
			if (num <= 0.5f)
			{
				s = Mathf.Lerp(0.04f, 1f, num / 0.5f);
				v = 1f;
			}
			else
			{
				s = 1f;
				v = Mathf.Lerp(1f, 0.08f, (num - 0.5f) / 0.5f);
			}
			current = Color.HSVToRGB(h, s, v);
			current.a = 1f;
			current2.Use();
		}
		DrawSpectrumSelection(rect, current);
		GUI.color = Color.white;
		return current;
	}

	private static void DrawSpectrumSelection(Rect rect, Color current)
	{
		Color.RGBToHSV(current, out float H, out float S, out float V);
		float num = ((!(V >= 0.95f)) ? (0.5f + Mathf.InverseLerp(1f, 0.08f, V) * 0.5f) : (Mathf.InverseLerp(0.04f, 1f, S) * 0.5f));
		float num2 = rect.x + H * rect.width;
		float num3 = rect.y + num * rect.height;
		Rect rect2 = new Rect(num2 - 7f, num3 - 7f, 14f, 14f);
		Rect rect3 = new Rect(num2 - 5f, num3 - 5f, 10f, 10f);
		DrawRectOutline(rect2, new Color(0f, 0f, 0f, 0.9f), 2f);
		DrawRectOutline(rect3, new Color(1f, 1f, 1f, 0.95f), 1.5f);
	}

	private static void DrawRectOutline(Rect rect, Color color, float thickness)
	{
		GUI.color = color;
		GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
	}

	private static void EnsureSpectrumPickerTexture()
	{
		if (spectrumPickerTexture != null)
		{
			return;
		}
		Color32[] array = new Color32[92160];
		for (int i = 0; i < 180; i++)
		{
			float num = 1f - (float)i / 179f;
			float s;
			float v;
			if (num <= 0.5f)
			{
				s = Mathf.Lerp(0.04f, 1f, num / 0.5f);
				v = 1f;
			}
			else
			{
				s = 1f;
				v = Mathf.Lerp(1f, 0.08f, (num - 0.5f) / 0.5f);
			}
			for (int j = 0; j < 512; j++)
			{
				Color color = Color.HSVToRGB((float)j / 511f, s, v);
				array[i * 512 + j] = color;
			}
		}
		spectrumPickerTexture = new Texture2D(512, 180, TextureFormat.RGBA32, mipChain: false);
		spectrumPickerTexture.name = "ADOFAIDX_SpectrumPicker";
		spectrumPickerTexture.filterMode = FilterMode.Bilinear;
		spectrumPickerTexture.wrapMode = TextureWrapMode.Clamp;
		spectrumPickerTexture.SetPixels32(array);
		spectrumPickerTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
	}

	private static bool DrawExactColorButton(Color color, float width, float height)
	{
		Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
		bool result = GUI.Button(rect, GUIContent.none, GUIStyle.none);
		Color color2 = GUI.color;
		GUI.color = color;
		GUI.DrawTexture(rect, Texture2D.whiteTexture);
		GUI.color = new Color(1f, 1f, 1f, 0.35f);
		GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 1f), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 1f, rect.height), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), Texture2D.whiteTexture);
		GUI.color = color2;
		return result;
	}

	private static void DrawColorPalette(ref string colorText)
	{
		Color current = (TryParseEffectColor(colorText, out var color) ? color : Color.white);
		GUILayout.BeginHorizontal();
		GUILayout.Space(150f);
		current = DrawSpectrumPicker(current, 380f, 120f);
		GUILayout.EndHorizontal();
		colorText = "#" + ColorUtility.ToHtmlStringRGB(current);
		GUILayout.BeginHorizontal();
		GUILayout.Space(150f);
		string[] array = new string[12]
		{
			"#33FFA6", "#00E5FF", "#4D7CFF", "#BA68FF", "#FF4FA3", "#FF4438", "#FFB300", "#FFFFFF", "#8BC34A", "#00BCD4",
			"#9C27B0", "#FF9800"
		};
		for (int i = 0; i < array.Length; i++)
		{
			if (i == 6)
			{
				GUILayout.EndHorizontal();
				GUILayout.BeginHorizontal();
				GUILayout.Space(150f);
			}
			Color color2 = Color.white;
			ColorUtility.TryParseHtmlString(array[i], out color2);
			if (DrawExactColorButton(color2, 24f, 20f))
			{
				colorText = array[i];
			}
		}
		GUILayout.EndHorizontal();
		Color color4;
		Color color3 = (TryParseEffectColor(colorText, out color4) ? color4 : Color.white);
		color3.r = DrawColorChannel("R", color3.r * 255f) / 255f;
		color3.g = DrawColorChannel("G", color3.g * 255f) / 255f;
		color3.b = DrawColorChannel("B", color3.b * 255f) / 255f;
		colorText = "#" + ColorUtility.ToHtmlStringRGB(color3);
	}

	private static float DrawColorChannel(string label, float value)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Space(150f);
		GUILayout.Label(label, GUILayout.Width(20f));
		value = GUILayout.HorizontalSlider(value, 0f, 255f, GUILayout.Width(260f));
		if (float.TryParse(GUILayout.TextField(Mathf.RoundToInt(value).ToString(), GUILayout.Width(54f)), out var result))
		{
			value = Mathf.Clamp(result, 0f, 255f);
		}
		GUILayout.EndHorizontal();
		return value;
	}

	private static bool TryParseEffectColor(string value, out Color color)
	{
		if (!string.IsNullOrWhiteSpace(value) && ColorUtility.TryParseHtmlString(value, out color))
		{
			return true;
		}
		color = Color.white;
		return false;
	}

	private static void DrawEffectStylePreview()
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(L("样式预览", "Style preview"), GUILayout.Width(150f));
		Rect rect = GUILayoutUtility.GetRect(210f, 52f, GUILayout.Width(210f));
		Rect rect2 = GUILayoutUtility.GetRect(210f, 52f, GUILayout.Width(210f));
		DrawSingleEffectPreview(rect, perfect: true);
		DrawSingleEffectPreview(rect2, perfect: false);
		GUILayout.EndHorizontal();
	}

	private static void DrawSingleEffectPreview(Rect preview, bool perfect)
	{
		GUI.Box(preview, GUIContent.none);
		Color color2;
		Color color3;
		Color color = ((!perfect) ? (TryParseEffectColor(settings.failedEffectColor, out color2) ? color2 : new Color(1f, 0.24f, 0.2f)) : (TryParseEffectColor(settings.perfectEffectColor, out color3) ? color3 : new Color(0.2f, 1f, 0.65f)));
		color.a = Mathf.Clamp01(settings.effectOpacity);
		DrawEffectShape(new Rect(preview.center.x - 15f, preview.yMin + 6f, 30f, 28f), settings.effectStyle, color, perfect ? GetEffectText(perfect: true) : GetEffectText(perfect: false), 1f, drawLabel: false);
		GUIStyle style = new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.MiddleCenter,
			fontStyle = FontStyle.Bold,
			fontSize = 10,
			normal = 
			{
				textColor = color
			},
			clipping = TextClipping.Clip
		};
		GUI.color = Color.white;
		GUI.Label(new Rect(preview.xMin + 2f, preview.yMax - 17f, preview.width - 4f, 15f), perfect ? GetEffectText(perfect: true) : GetEffectText(perfect: false), style);
		GUI.color = Color.white;
	}

	private static string GetEffectText(bool perfect)
	{
		string result = (perfect ? "BREAK!" : "BREAK MISS");
		string text = (perfect ? settings.perfectEffectText : settings.failedEffectText);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return result;
	}

	private static void DrawCustomEffectImageRow(bool perfect)
	{
		GUILayout.BeginHorizontal();
		bool value = (perfect ? settings.useCustomPerfectImage : settings.useCustomFailedImage);
		value = GUILayout.Toggle(value, perfect ? L("break", "Break") : L("miss", "Miss"), GUILayout.Width(80f));
		if (perfect)
		{
			settings.useCustomPerfectImage = value;
		}
		else
		{
			settings.useCustomFailedImage = value;
		}
		if (value)
		{
			string text = (perfect ? settings.customPerfectImagePath : settings.customFailedImagePath);
			text = GUILayout.TextField(text ?? "", GUILayout.Width(250f));
			if (perfect)
			{
				settings.customPerfectImagePath = text;
			}
			else
			{
				settings.customFailedImagePath = text;
			}
			if (GUILayout.Button(L("加载", "Load"), GUILayout.Width(70f)))
			{
				LoadCustomEffectImage(perfect);
			}
			if (GUILayout.Button(L("浏览", "Browse"), GUILayout.Width(74f)))
			{
				string text2 = FileBrowser.PickFile(modEntry.Path, "Images", new string[3] { "png", "jpg", "jpeg" }, perfect ? L("选择 break 特效图片", "Select Break effect") : L("选择 miss 特效图片", "Select Miss effect"));
				if (!string.IsNullOrWhiteSpace(text2))
				{
					if (perfect)
					{
						settings.customPerfectImagePath = text2;
						settings.useCustomPerfectImage = true;
					}
					else
					{
						settings.customFailedImagePath = text2;
						settings.useCustomFailedImage = true;
					}
					LoadCustomEffectImage(perfect);
				}
			}
		}
		GUILayout.EndHorizontal();
	}

	private static void DrawCustomAudioRow(bool perfect)
	{
		GUILayout.BeginHorizontal();
		bool value = (perfect ? settings.useCustomPerfectSound : settings.useCustomFailedSound);
		value = GUILayout.Toggle(value, perfect ? L("break", "Break") : L("miss", "Miss"), GUILayout.Width(80f));
		if (perfect)
		{
			settings.useCustomPerfectSound = value;
		}
		else
		{
			settings.useCustomFailedSound = value;
		}
		if (value)
		{
			string text = (perfect ? settings.customPerfectSoundPath : settings.customFailedSoundPath);
			text = GUILayout.TextField(text ?? "", GUILayout.Width(250f));
			if (perfect)
			{
				settings.customPerfectSoundPath = text;
			}
			else
			{
				settings.customFailedSoundPath = text;
			}
			if (GUILayout.Button(L("加载", "Load"), GUILayout.Width(64f)))
			{
				LoadCustomAudio(perfect);
			}
			if (GUILayout.Button(L("浏览", "Browse"), GUILayout.Width(72f)))
			{
				string text2 = FileBrowser.PickFile(modEntry.Path, "Audio", new string[5] { "mp3", "wav", "ogg", "aiff", "aif" }, perfect ? L("选择 break 音效", "Select Break sound") : L("选择 miss 音效", "Select Miss sound"));
				if (!string.IsNullOrWhiteSpace(text2))
				{
					if (perfect)
					{
						settings.customPerfectSoundPath = text2;
						settings.useCustomPerfectSound = true;
					}
					else
					{
						settings.customFailedSoundPath = text2;
						settings.useCustomFailedSound = true;
					}
					LoadCustomAudio(perfect);
				}
			}
		}
		GUILayout.EndHorizontal();
	}

	private static void SaveSettingsGUI(UnityModManager.ModEntry entry)
	{
		settings?.Save(entry);
	}

	private static string GetAudioPresetFolder()
	{
		return Path.Combine(modEntry.Path, "AudioPresets");
	}

	private static void EnsureAudioPresetFolder()
	{
		try
		{
			string audioPresetFolder = GetAudioPresetFolder();
			Directory.CreateDirectory(audioPresetFolder);
			string[] array = new string[2] { "ice.wav", "break.wav" };
			foreach (string path in array)
			{
				string text = Path.Combine(modEntry.Path, path);
				string text2 = Path.Combine(audioPresetFolder, path);
				if (File.Exists(text) && !File.Exists(text2))
				{
					File.Copy(text, text2);
				}
			}
			RefreshAudioPresetFiles(force: true);
		}
		catch (Exception ex)
		{
			modEntry.Logger.Error("Failed to prepare AudioPresets folder: " + ex);
		}
	}

	private static void RefreshAudioPresetFiles(bool force = false)
	{
		if (!force && Time.unscaledTime < nextAudioPresetScanTime)
		{
			return;
		}
		nextAudioPresetScanTime = Time.unscaledTime + 2f;
		audioPresetFiles.Clear();
		try
		{
			string audioPresetFolder = GetAudioPresetFolder();
			Directory.CreateDirectory(audioPresetFolder);
			string[] files = Directory.GetFiles(audioPresetFolder, "*", SearchOption.TopDirectoryOnly);
			foreach (string path in files)
			{
				switch (Path.GetExtension(path).ToLowerInvariant())
				{
				case ".mp3":
				case ".wav":
				case ".ogg":
				case ".aiff":
				case ".aif":
					audioPresetFiles.Add(Path.GetFileName(path));
					break;
				}
			}
			audioPresetFiles.Sort(StringComparer.OrdinalIgnoreCase);
		}
		catch (Exception ex)
		{
			modEntry.Logger.Error("Failed to scan AudioPresets folder: " + ex);
		}
	}

	private static int GetAudioPresetIndex(bool perfect)
	{
		string text = (perfect ? settings.customPerfectSoundPath : settings.customFailedSoundPath);
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0;
		}
		string text2 = text.Replace('/', '\\');
		string value = "AudioPresets\\";
		if (!text2.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}
		string fileName = Path.GetFileName(text2);
		int num = audioPresetFiles.FindIndex((string name) => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase));
		if (num >= 0)
		{
			return num + 1;
		}
		return 0;
	}

	private static void ApplyAudioPreset(bool perfect, int index)
	{
		RefreshAudioPresetFiles(force: true);
		int num = audioPresetFiles.Count + 3;
		index = (index % num + num) % num;
		if (index < 3)
		{
			if (perfect)
			{
				settings.perfectBuiltinSound = index;
				settings.customPerfectSoundPath = "";
				settings.useCustomPerfectSound = false;
			}
			else
			{
				settings.failedBuiltinSound = index;
				settings.customFailedSoundPath = "";
				settings.useCustomFailedSound = false;
			}
			return;
		}
		string text = Path.Combine("AudioPresets", audioPresetFiles[index - 3]);
		if (perfect)
		{
			settings.perfectBuiltinSound = 2;
			settings.customPerfectSoundPath = text;
			settings.useCustomPerfectSound = true;
		}
		else
		{
			settings.failedBuiltinSound = 2;
			settings.customFailedSoundPath = text;
			settings.useCustomFailedSound = true;
		}
		LoadCustomAudio(perfect);
	}

	private static void DrawMergedSoundPresetSection()
	{
		RefreshAudioPresetFiles();
		GUILayout.BeginHorizontal();
		GUILayout.Label(L("音频预设", "Audio presets"), GUILayout.Width(150f));
		if (GUILayout.Button(L("添加预设", "Add preset"), GUILayout.Width(120f)))
		{
			OpenAudioPresetFolder();
		}
		GUILayout.EndHorizontal();
		DrawAudioPresetSelectionRow(perfect: true);
		DrawAudioPresetSelectionRow(perfect: false);
	}

	private static bool IsMergedSoundMuted(bool perfect)
	{
		bool num = (perfect ? settings.useCustomPerfectSound : settings.useCustomFailedSound);
		string value = (perfect ? settings.customPerfectSoundPath : settings.customFailedSoundPath);
		if (num && !string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		return Mathf.Clamp(perfect ? settings.perfectBuiltinSound : settings.failedBuiltinSound, 0, 2) == 2;
	}

	private static int GetMergedSoundSelection(bool perfect)
	{
		bool num = (perfect ? settings.useCustomPerfectSound : settings.useCustomFailedSound);
		int audioPresetIndex = GetAudioPresetIndex(perfect);
		if (num && audioPresetIndex > 0)
		{
			return audioPresetIndex + 2;
		}
		return Mathf.Clamp(perfect ? settings.perfectBuiltinSound : settings.failedBuiltinSound, 0, 2);
	}

	private static void DrawAudioPresetSelectionRow(bool perfect)
	{
		string[] array = new string[audioPresetFiles.Count + 3];
		array[0] = "Chime";
		array[1] = "Click";
		array[2] = "Off";
		for (int i = 0; i < audioPresetFiles.Count; i++)
		{
			array[i + 3] = Path.GetFileNameWithoutExtension(audioPresetFiles[i]);
		}
		int mergedSoundSelection = GetMergedSoundSelection(perfect);
		GUILayout.BeginHorizontal();
		GUILayout.Label(perfect ? L("Break", "Break") : L("Miss", "Miss"), GUILayout.Width(150f));
		int num = Mathf.Min(5, array.Length);
		int num2 = GUILayout.SelectionGrid(mergedSoundSelection, array, num, GUILayout.Width((float)num * 100f));
		GUILayout.EndHorizontal();
		if (num2 != mergedSoundSelection)
		{
			ApplyAudioPreset(perfect, num2);
		}
	}

	private static void OpenAudioPresetFolder()
	{
		try
		{
			string audioPresetFolder = GetAudioPresetFolder();
			Directory.CreateDirectory(audioPresetFolder);
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "\"" + audioPresetFolder + "\"",
				UseShellExecute = true
			});
			RefreshAudioPresetFiles(force: true);
		}
		catch (Exception ex)
		{
			modEntry.Logger.Error("Failed to open AudioPresets folder: " + ex);
		}
	}

	private static void AutoLoadCustomAudio()
	{
		if (settings.useCustomPerfectSound && !string.IsNullOrWhiteSpace(settings.customPerfectSoundPath))
		{
			LoadCustomAudio(perfect: true);
		}
		if (settings.useCustomFailedSound && !string.IsNullOrWhiteSpace(settings.customFailedSoundPath))
		{
			LoadCustomAudio(perfect: false);
		}
	}

	private static void AutoLoadCustomEffectImages()
	{
		if (settings.useCustomPerfectImage && !string.IsNullOrWhiteSpace(settings.customPerfectImagePath))
		{
			LoadCustomEffectImage(perfect: true);
		}
		if (settings.useCustomFailedImage && !string.IsNullOrWhiteSpace(settings.customFailedImagePath))
		{
			LoadCustomEffectImage(perfect: false);
		}
	}

	private static void LoadCustomEffectImage(bool perfect)
	{
		string text = (perfect ? settings.customPerfectImagePath : settings.customFailedImagePath);
		if (string.IsNullOrWhiteSpace(text))
		{
			customImageStatus = L("特效图片路径为空", "Effect image path is empty");
			return;
		}
		if (!Path.IsPathRooted(text))
		{
			text = Path.Combine(modEntry.Path, text);
		}
		if (!File.Exists(text))
		{
			customImageStatus = L("特效图片不存在: " + text, "Effect image does not exist: " + text);
			return;
		}
		string text2 = Path.GetExtension(text).ToLowerInvariant();
		if (text2 != ".png" && text2 != ".jpg" && text2 != ".jpeg")
		{
			customImageStatus = L("特效图片格式必须是 png/jpg/jpeg", "Effect image must be png/jpg/jpeg");
			return;
		}
		try
		{
			byte[] array = File.ReadAllBytes(text);
			if (array.Length > 10485760)
			{
				customImageStatus = L("特效图片超过 10 MB", "Effect image exceeds 10 MB");
				return;
			}
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			MethodInfo methodInfo = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")?.GetMethod("LoadImage", new Type[3]
			{
				typeof(Texture2D),
				typeof(byte[]),
				typeof(bool)
			});
			if (!(methodInfo != null) || !(bool)methodInfo.Invoke(null, new object[3] { texture2D, array, false }))
			{
				UnityEngine.Object.Destroy(texture2D);
				customImageStatus = L("特效图片解码失败", "Effect image decode failed");
				return;
			}
			Texture2D texture2D2 = (perfect ? customPerfectTexture : customFailedTexture);
			if (texture2D2 != null)
			{
				UnityEngine.Object.Destroy(texture2D2);
			}
			if (perfect)
			{
				customPerfectTexture = texture2D;
				settings.customPerfectImagePath = text;
			}
			else
			{
				customFailedTexture = texture2D;
				settings.customFailedImagePath = text;
			}
			customImageStatus = L("特效图片已加载: " + text, "Effect image loaded: " + text);
		}
		catch (Exception ex)
		{
			customImageStatus = L("特效图片加载失败: " + ex.Message, "Effect image load failed: " + ex.Message);
		}
	}

	internal static void LoadCustomAudio(bool perfect)
	{
		string text = (perfect ? settings.customPerfectSoundPath : settings.customFailedSoundPath);
		if (string.IsNullOrWhiteSpace(text))
		{
			customAudioStatus = (perfect ? L("break 音效路径为空", "Break audio path is empty") : L("miss 音效路径为空", "Miss audio path is empty"));
			return;
		}
		if (!Path.IsPathRooted(text))
		{
			text = Path.Combine(modEntry.Path, text);
		}
		if (!File.Exists(text))
		{
			customAudioStatus = L("自定义音效文件不存在: " + text, "Custom audio file does not exist: " + text);
			return;
		}
		if (Path.GetExtension(text).ToLowerInvariant() == ".ogg" && !OggFileContainsVorbisHeader(text))
		{
			customAudioStatus = L("OGG 仅支持 Vorbis 编码；该文件可能是 OGG-FLAC 或 OGG-Opus。", "OGG must use Vorbis encoding; OGG-FLAC/Opus is unsupported.");
			return;
		}
		if (overlayComponent == null)
		{
			customAudioStatus = L("加载器尚未初始化", "Audio loader is unavailable");
			return;
		}
		if (perfect)
		{
			customPerfectLoading = true;
		}
		else
		{
			customFailedLoading = true;
		}
		customAudioStatus = L("正在加载自定义音效...", "Loading custom audio...");
		overlayComponent.LoadCustomAudio(text, perfect);
	}

	private static bool OggFileContainsVorbisHeader(string path)
	{
		try
		{
			using (FileStream fileStream = File.OpenRead(path))
			{
				int num = (int)Math.Min(65536L, fileStream.Length);
				byte[] array = new byte[num];
				fileStream.Read(array, 0, num);
				return Encoding.ASCII.GetString(array).Contains("vorbis");
			}
		}
		catch
		{
			return false;
		}
	}

	internal static void SetCustomAudioClip(bool perfect, object clip, string message)
	{
		if (perfect)
		{
			customPerfectClip = clip;
			customPerfectLoading = false;
		}
		else
		{
			customFailedClip = clip;
			customFailedLoading = false;
		}
		customAudioStatus = message;
		modEntry.Logger.Log(message);
	}

	internal static object GetCustomAudioClip(bool perfect)
	{
		if (!perfect)
		{
			return customFailedClip;
		}
		return customPerfectClip;
	}

	internal static bool CustomAudioLoading(bool perfect)
	{
		if (!perfect)
		{
			return customFailedLoading;
		}
		return customPerfectLoading;
	}

	private static void InitializeFeedback()
	{
		if (settings == null)
		{
			return;
		}
		audioSourceType = audioSourceType ?? Type.GetType("UnityEngine.AudioSource, UnityEngine.AudioModule");
		audioClipType = audioClipType ?? Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule");
		if (audioSourceType == null || audioClipType == null)
		{
			modEntry.Logger.Error("ADOFAI DX: Unity audio types were not found.");
			return;
		}
		if (feedbackObject == null)
		{
			feedbackObject = new GameObject("ADOFAIDX_Feedback");
			UnityEngine.Object.DontDestroyOnLoad(feedbackObject);
			feedbackAudioSource = feedbackObject.AddComponent(audioSourceType);
			SetMember(feedbackAudioSource, "playOnAwake", false);
			SetMember(feedbackAudioSource, "spatialBlend", 0f);
			SetMember(feedbackAudioSource, "loop", false);
		}
		perfectChimeClip = perfectChimeClip ?? CreateTone("ADOFAIDX_BreakPerfect_Chime", 1046.5f, 0.16f, 3.6f);
		perfectClickClip = perfectClickClip ?? CreateTone("ADOFAIDX_BreakPerfect_Click", 1760f, 0.055f, 7.5f);
		failedChimeClip = failedChimeClip ?? CreateTone("ADOFAIDX_BreakFailed_Chime", 329.6f, 0.18f, 3.2f);
		failedClickClip = failedClickClip ?? CreateTone("ADOFAIDX_BreakFailed_Click", 146.8f, 0.08f, 6.5f);
	}

	private static object CreateTone(string name, float frequency, float duration, float decay)
	{
		int num = Mathf.Max(1, Mathf.RoundToInt(44100f * duration));
		float[] array = new float[num];
		for (int i = 0; i < num; i++)
		{
			float num2 = (float)i / 44100f;
			float num3 = (float)i / (float)num;
			float num4 = Mathf.Pow(1f - num3, decay);
			array[i] = Mathf.Sin((float)Math.PI * 2f * frequency * num2) * num4 * 0.28f;
		}
		MethodInfo method = audioClipType.GetMethod("Create", new Type[5]
		{
			typeof(string),
			typeof(int),
			typeof(int),
			typeof(int),
			typeof(bool)
		});
		if (method == null)
		{
			modEntry.Logger.Error("ADOFAI DX: AudioClip.Create was not found.");
			return null;
		}
		object obj = method.Invoke(null, new object[5] { name, num, 1, 44100, false });
		MethodInfo method2 = audioClipType.GetMethod("SetData", new Type[2]
		{
			typeof(float[]),
			typeof(int)
		});
		if (method2 == null)
		{
			modEntry.Logger.Error("ADOFAI DX: AudioClip.SetData(float[], int) not found.");
			return null;
		}
		method2.Invoke(obj, new object[2] { array, 0 });
		return obj;
	}

	private static void SetMember(object target, string memberName, object value)
	{
		Type type = target.GetType();
		System.Reflection.PropertyInfo property = type.GetProperty(memberName);
		if (property != null)
		{
			property.SetValue(target, value, null);
		}
		else
		{
			type.GetField(memberName)?.SetValue(target, value);
		}
	}

	private static void TriggerBreakFeedback(scrFloor floor, bool perfect)
	{
		PlayBreakSound(perfect);
		if (settings != null && settings.effectStyle != 3 && !(settings.effectOpacity <= 0f) && !((UnityEngine.Object)(object)floor == null))
		{
			if (breakVisuals.Count > 32)
			{
				breakVisuals.RemoveAt(0);
			}
			breakVisuals.Add(new BreakVisual
			{
				WorldPosition = ((Component)(object)floor).transform.position,
				Perfect = perfect,
				StartTime = Time.unscaledTime
			});
		}
	}

	private static void PlayBreakSound(bool perfect)
	{
		if (settings == null)
		{
			return;
		}
		float num = Mathf.Clamp01(settings.soundVolume);
		bool flag = (perfect ? settings.useCustomPerfectSound : settings.useCustomFailedSound);
		int num2 = Mathf.Clamp(perfect ? settings.perfectBuiltinSound : settings.failedBuiltinSound, 0, 2);
		if (!flag && num2 == 2)
		{
			return;
		}
		object customAudioClip = GetCustomAudioClip(perfect);
		if (flag && customAudioClip == null && !CustomAudioLoading(perfect) && !string.IsNullOrWhiteSpace(perfect ? settings.customPerfectSoundPath : settings.customFailedSoundPath))
		{
			LoadCustomAudio(perfect);
		}
		scrSfx instance = scrSfx.instance;
		if ((UnityEngine.Object)(object)instance != null)
		{
			if (flag && customAudioClip != null)
			{
				PlayGameAudioClip(instance, customAudioClip, num);
			}
			else if (num2 != 2)
			{
				SfxSound sfxSound = ((!perfect) ? ((num2 != 1) ? SfxSound.PowerDown : SfxSound.MenuDecrement) : ((num2 == 1) ? SfxSound.MenuIncrement : SfxSound.PurePerfect));
				typeof(scrSfx).GetMethod("PlaySfx", new Type[5]
				{
					typeof(SfxSound),
					typeof(MixerGroup),
					typeof(float),
					typeof(float),
					typeof(float)
				})?.Invoke(instance, new object[5]
				{
					sfxSound,
					MixerGroup.ConductorSfx,
					num,
					1f,
					0f
				});
			}
			return;
		}
		InitializeFeedback();
		if (feedbackAudioSource == null || audioClipType == null || num2 == 2)
		{
			return;
		}
		object obj = ((!perfect) ? ((num2 == 1) ? failedClickClip : failedChimeClip) : ((num2 == 1) ? perfectClickClip : perfectChimeClip));
		float num3 = Mathf.Clamp01(perfect ? settings.perfectSoundVolume : settings.failedSoundVolume);
		if (obj == null || num3 <= 0f)
		{
			return;
		}
		object obj2 = feedbackAudioSource;
		scrConductor instance2 = scrConductor.instance;
		if ((UnityEngine.Object)(object)instance2 != null)
		{
			object value = ((object)instance2).GetType().GetField("song").GetValue(instance2);
			if (value != null)
			{
				obj2 = value;
			}
		}
		float num4 = Mathf.Max(0.15f, num3);
		Vector3 vector = ((Camera.main != null) ? Camera.main.transform.position : Vector3.zero);
		MethodInfo method = audioSourceType.GetMethod("PlayClipAtPoint", new Type[3]
		{
			audioClipType,
			typeof(Vector3),
			typeof(float)
		});
		if (method != null)
		{
			method.Invoke(null, new object[3] { obj, vector, num4 });
		}
		else
		{
			obj2.GetType().GetMethod("PlayOneShot", new Type[2]
			{
				audioClipType,
				typeof(float)
			})?.Invoke(obj2, new object[2] { obj, num4 });
		}
	}

	private static void PlayGameAudioClip(scrSfx sfx, object clip, float volume)
	{
		audioSourceType = audioSourceType ?? Type.GetType("UnityEngine.AudioSource, UnityEngine.AudioModule");
		audioClipType = audioClipType ?? Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule");
		if ((UnityEngine.Object)(object)sfx != null && audioClipType != null)
		{
			object obj = typeof(scrSfx).GetField("sfxSrc", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(sfx);
			MethodInfo methodInfo = obj?.GetType().GetMethod("PlayOneShot", new Type[2]
			{
				audioClipType,
				typeof(float)
			});
			if (methodInfo != null)
			{
				methodInfo.Invoke(obj, new object[2] { clip, volume });
				return;
			}
		}
		if (audioSourceType != null && audioClipType != null)
		{
			Vector3 vector = ((Camera.main != null) ? Camera.main.transform.position : Vector3.zero);
			MethodInfo method = audioSourceType.GetMethod("PlayClipAtPoint", new Type[3]
			{
				audioClipType,
				typeof(Vector3),
				typeof(float)
			});
			if (method != null)
			{
				method.Invoke(null, new object[3] { clip, vector, volume });
				return;
			}
		}
		typeof(scrSfx).GetMethod("PlaySfx", new Type[5]
		{
			audioClipType,
			typeof(MixerGroup),
			typeof(float),
			typeof(float),
			typeof(float)
		})?.Invoke(sfx, new object[5]
		{
			clip,
			MixerGroup.ConductorSfx,
			volume,
			1f,
			0f
		});
	}

	private static void DrawBreakVisuals()
	{
		if (breakVisuals.Count == 0 || settings == null || !settings.enableDXNotes || settings.effectStyle == 3 || settings.effectOpacity <= 0f)
		{
			return;
		}
		Camera main = Camera.main;
		scrController instance = scrController.instance;
		if (main == null || (UnityEngine.Object)(object)instance == null)
		{
			return;
		}
		for (int num2 = breakVisuals.Count - 1; num2 >= 0; num2--)
		{
			BreakVisual breakVisual = breakVisuals[num2];
			float num3 = Time.unscaledTime - breakVisual.StartTime;
			if (num3 >= 0.38f)
			{
				breakVisuals.RemoveAt(num2);
			}
			else
			{
				Vector3 effectWorldPosition = breakVisual.WorldPosition + Vector3.up * (0.35f * instance.tileSize);
				Vector3 vector = main.WorldToScreenPoint(effectWorldPosition);
				if (!(vector.z <= 0f))
				{
					float num4 = num3 / 0.38f;
					float a = settings.effectOpacity * (1f - num4);
					Color color2;
					Color color3;
					Color color = ((!breakVisual.Perfect) ? (TryParseEffectColor(settings.failedEffectColor, out color2) ? color2 : new Color(1f, 0.24f, 0.2f)) : (TryParseEffectColor(settings.perfectEffectColor, out color3) ? color3 : new Color(0.2f, 1f, 0.65f)));
					color.a = a;
					float x = vector.x;
					float num5 = (float)Screen.height - vector.y;
					float num6 = Mathf.Clamp(settings.effectScale, 0.5f, 3f);
					float tileScreenSize = GetWorldDistanceInPixels(main, effectWorldPosition, main.transform.right, instance.tileSize);
					float num7 = Mathf.Clamp(tileScreenSize * (0.58f + 0.38f * num4) * num6, 16f, (float)Screen.height * 0.5f);
					Rect rect = new Rect(x - num7 * 0.5f, num5 - num7 * 0.5f, num7, num7);
					Texture2D texture2D = ((!breakVisual.Perfect) ? (settings.useCustomFailedImage ? customFailedTexture : null) : (settings.useCustomPerfectImage ? customPerfectTexture : null));
					if (texture2D != null)
					{
						GUI.color = new Color(1f, 1f, 1f, a);
						GUI.DrawTexture(rect, texture2D, ScaleMode.ScaleToFit, alphaBlend: true);
						DrawEffectShape(rect, 2, color, GetEffectText(breakVisual.Perfect), num4, drawLabel: true);
					}
					else
					{
						DrawEffectShape(rect, settings.effectStyle, color, GetEffectText(breakVisual.Perfect), num4, drawLabel: true);
					}
					GUI.color = Color.white;
				}
			}
		}
	}

	private static float GetWorldDistanceInPixels(Camera camera, Vector3 worldPosition, Vector3 worldDirection, float worldDistance)
	{
		if (camera == null || worldDirection.sqrMagnitude < 0.000001f)
		{
			return 0f;
		}
		Vector3 start = camera.WorldToScreenPoint(worldPosition);
		Vector3 end = camera.WorldToScreenPoint(worldPosition + worldDirection.normalized * Mathf.Max(0.0001f, worldDistance));
		if (start.z <= 0f || end.z <= 0f)
		{
			return 0f;
		}
		return Vector2.Distance(start, end);
	}

	private static void DrawEffectShape(Rect area, int style, Color color, string label, float progress, bool drawLabel)
	{
		if (style != 3)
		{
			EnsureVisualTextures();
			GUI.color = color;
			switch (style)
			{
			case 0:
				GUI.DrawTexture(area, softGlowTexture, ScaleMode.StretchToFill, alphaBlend: true);
				break;
			case 1:
				GUI.DrawTexture(area, ringGlowTexture, ScaleMode.StretchToFill, alphaBlend: true);
				break;
			}
			if (drawLabel)
			{
				GUIStyle gUIStyle = breakFeedbackStyle ?? GUI.skin.label;
				float areaScale = Mathf.Clamp(area.height / 58f, 0.45f, 6f);
				int fontSize = gUIStyle.fontSize;
				gUIStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(18f * Mathf.Clamp(progress + 0.7f, 0.7f, 1.7f) * areaScale));
				Rect position = new Rect(area.center.x - 110f * areaScale, area.yMin - 28f * areaScale, 220f * areaScale, 30f * areaScale);
				GUI.color = new Color(0f, 0f, 0f, color.a * 0.85f);
				GUI.Label(new Rect(position.x + 1.5f, position.y + 1.5f, position.width, position.height), label, gUIStyle);
				GUI.color = new Color(color.r, color.g, color.b, color.a);
				GUI.Label(position, label, gUIStyle);
				gUIStyle.fontSize = fontSize;
			}
			GUI.color = Color.white;
		}
	}

	private static void UpdateDXGameplayVisuals()
	{
		bool gameplayActive = IsGameplayActive();
		if ((!gameplayActive && !IsEditorEditing()) || settings == null || !settings.enableDXNotes || !settings.showGameplayDXMarker)
		{
			DestroyDXGameplayVisuals(restoreColors: true);
			activeDXVisualFloorIds.Clear();
			return;
		}
		scrLevelMaker instance = scrLevelMaker.instance;
		scrController instance2 = scrController.instance;
		if ((UnityEngine.Object)(object)instance == null || instance.listFloors == null || (UnityEngine.Object)(object)instance2 == null)
		{
			DestroyDXGameplayVisuals(restoreColors: true);
			activeDXVisualFloorIds.Clear();
			return;
		}
		float unscaledTime = Time.unscaledTime;
		float intensity = (settings.gameplayDXGlow ? Mathf.Clamp01(settings.gameplayDXGlowIntensity) : 0f);
		activeDXVisualFloorIds.Clear();
		foreach (scrFloor floor in instance.listFloors)
		{
			if ((UnityEngine.Object)(object)floor == null || !markedFloors.Contains(floor.seqID))
			{
				continue;
			}
			DXGameplayVisual value;
			bool hasVisual = dxGameplayVisuals.TryGetValue(floor.seqID, out value);
			if (hasVisual && (UnityEngine.Object)(object)value.Floor != (UnityEngine.Object)(object)floor)
			{
				DestroyDXGameplayVisual(floor.seqID, value, restoreColor: true);
				value = null;
				hasVisual = false;
			}
			if (!hasVisual)
			{
				value = CreateDXGameplayVisual(floor, instance2.tileSize);
				dxGameplayVisuals[floor.seqID] = value;
			}
			activeDXVisualFloorIds.Add(floor.seqID);
			Renderer renderer = (((UnityEngine.Object)(object)floor.floorRenderer == null) ? null : floor.floorRenderer.renderer);
			float opacity = Mathf.Clamp01(floor.opacity);
			bool sourceActive = renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.gameObject.activeSelf && opacity > 0.001f;
			if (value.Root.activeSelf != sourceActive)
			{
				value.Root.SetActive(sourceActive);
			}
			bool judged = gameplayActive && (floor.seqID < instance2.currentFloorID || breakAttempts.Any((BreakAttempt attempt) => attempt.FloorId == floor.seqID));
			UpdateDXFloatingMotion(value, unscaledTime, instance2.tileSize, judged, settings.gameplayDXFloat);
			if (!sourceActive || !renderer.isVisible)
			{
				continue;
			}
			if (DXSourceGeometryChanged(value, unscaledTime))
			{
				InvalidateDXMeshCache(value.SourceMesh);
				DestroyDXGameplayVisual(floor.seqID, value, restoreColor: true);
				value = CreateDXGameplayVisual(floor, instance2.tileSize);
				dxGameplayVisuals[floor.seqID] = value;
			}
			if (unscaledTime <= value.OutlineRefreshUntil && unscaledTime >= value.NextOutlineRefreshTime)
			{
				ResolveDXOutline(renderer, value, instance2.tileSize);
				value.NextOutlineRefreshTime = unscaledTime + 0.1f;
			}
			Color baseColor = GetDXNoteBaseColor();
			if (settings.gameplayDXColorAdaptive && (UnityEngine.Object)(object)floor.floorRenderer != null)
			{
				Color color = floor.floorRenderer.color;
				color.a = 1f;
				baseColor = color;
			}
			float phase = (float)floor.seqID * 0.73f;
			float wave = 0.5f + 0.5f * Mathf.Sin(unscaledTime * 1.8f + phase);
			float flow = 0.5f + 0.5f * Mathf.Sin(unscaledTime * 2.65f + phase * 1.37f);
			Color animated = DXAnimatedColor(baseColor, Mathf.Clamp01(wave * 0.72f + flow * 0.28f));
			animated.a = opacity;
			Color surfaceColor = Color.Lerp(animated, Color.white, 0.12f);
			surfaceColor.a = animated.a;
			SetDXSurfaceColor(value, surfaceColor);
			if (value.SurfaceRenderer != null)
			{
				value.SurfaceRenderer.enabled = true;
			}
			if (value.SurfaceSpriteRenderer != null)
			{
				value.SurfaceSpriteRenderer.enabled = true;
			}
			bool visible = value.GeometryReady;
			value.Inner.enabled = false;
			if (value.GlowRenderer != null)
			{
				value.GlowRenderer.enabled = false;
			}
			UpdateDXMeshGlowLayers(value, baseColor, intensity, opacity, unscaledTime, visible);
		}
		dxVisualCleanupIds.Clear();
		foreach (int key in dxGameplayVisuals.Keys)
		{
			if (!activeDXVisualFloorIds.Contains(key))
			{
				dxVisualCleanupIds.Add(key);
			}
		}
		foreach (int floorId in dxVisualCleanupIds)
		{
			DestroyDXGameplayVisual(floorId, dxGameplayVisuals[floorId], restoreColor: true);
		}
	}

	private static bool IsEditorEditing()
	{
		scnEditor editor = scnEditor.instance;
		return (UnityEngine.Object)(object)editor != null && !editor.playMode;
	}

	private static Mesh GetOrCreateDXSurfaceMesh(Mesh sourceMesh)
	{
		int key = sourceMesh.GetInstanceID();
		if (!DXSurfaceMeshCache.TryGetValue(key, out Mesh mesh) || mesh == null)
		{
			mesh = UnityEngine.Object.Instantiate(sourceMesh);
			mesh.name = "ADOFAIDX_DX_Gradient_Cached_" + key;
			ApplyDXMeshVertexColors(mesh);
			DXSurfaceMeshCache[key] = mesh;
		}
		return mesh;
	}

	private static Mesh GetOrCreateDXGlowMesh(Mesh sourceMesh)
	{
		int key = sourceMesh.GetInstanceID();
		if (!DXGlowMeshCache.TryGetValue(key, out Mesh mesh) || mesh == null)
		{
			mesh = UnityEngine.Object.Instantiate(sourceMesh);
			Color[] colors = new Color[mesh.vertexCount];
			for (int i = 0; i < colors.Length; i++)
			{
				colors[i] = Color.white;
			}
			mesh.colors = colors;
			mesh.name = "ADOFAIDX_DX_Glow_Cached_" + key;
			DXGlowMeshCache[key] = mesh;
		}
		return mesh;
	}

	private static void ClearDXMeshCaches()
	{
		foreach (Mesh mesh in DXSurfaceMeshCache.Values)
		{
			if (mesh != null)
			{
				UnityEngine.Object.Destroy(mesh);
			}
		}
		foreach (Mesh mesh in DXGlowMeshCache.Values)
		{
			if (mesh != null)
			{
				UnityEngine.Object.Destroy(mesh);
			}
		}
		DXSurfaceMeshCache.Clear();
		DXGlowMeshCache.Clear();
	}

	private static void InvalidateDXMeshCache(Mesh sourceMesh)
	{
		if (sourceMesh == null)
		{
			return;
		}
		int key = sourceMesh.GetInstanceID();
		if (DXSurfaceMeshCache.TryGetValue(key, out Mesh surfaceMesh))
		{
			if (surfaceMesh != null)
			{
				UnityEngine.Object.Destroy(surfaceMesh);
			}
			DXSurfaceMeshCache.Remove(key);
		}
		if (DXGlowMeshCache.TryGetValue(key, out Mesh glowMesh))
		{
			if (glowMesh != null)
			{
				UnityEngine.Object.Destroy(glowMesh);
			}
			DXGlowMeshCache.Remove(key);
		}
	}

	private static DXGameplayVisual CreateDXGameplayVisual(scrFloor floor, float tileSize)
	{
		EnsureDXMaterials();
		Renderer renderer = floor.floorRenderer.renderer;
		Transform parent = ((renderer != null) ? renderer.transform : ((Component)(object)floor).transform);
		GameObject gameObject = new GameObject("ADOFAIDX_DX_Visual_" + floor.seqID);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.transform.localScale = Vector3.one;
		DXGameplayVisual dXGameplayVisual = new DXGameplayVisual
		{
			Floor = floor,
			Root = gameObject,
			OriginalColor = floor.floorRenderer.color,
			GlowVertices = new List<Vector3>(768),
			GlowColors = new List<Color>(768),
			GlowTriangles = new List<int>(1536)
		};
		int num = ((renderer != null) ? renderer.sortingLayerID : 0);
		int num2 = ((renderer != null) ? renderer.sortingOrder : floor.floorRenderer.renderer.sortingOrder);
		MeshFilter meshFilter = ((renderer != null) ? renderer.GetComponent<MeshFilter>() : null);
		MeshRenderer meshRenderer = renderer as MeshRenderer;
		SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
		if (meshFilter != null && meshFilter.sharedMesh != null && meshRenderer != null)
		{
			Mesh mesh = GetOrCreateDXSurfaceMesh(meshFilter.sharedMesh);
			GameObject gameObject2 = new GameObject("GradientSurface");
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			gameObject2.transform.localPosition = new Vector3(0f, 0f, -0.015f);
			gameObject2.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer meshRenderer2 = gameObject2.AddComponent<MeshRenderer>();
			Material[] sharedMaterials = meshRenderer.sharedMaterials;
			meshRenderer2.sharedMaterials = sharedMaterials;
			meshRenderer2.sortingLayerID = num;
			meshRenderer2.sortingOrder = num2 + 1;
			dXGameplayVisual.SurfaceMesh = mesh;
			dXGameplayVisual.SurfaceRenderer = meshRenderer2;
			dXGameplayVisual.SurfaceMaterial = ((sharedMaterials.Length != 0) ? sharedMaterials[0] : null);
			dXGameplayVisual.MeshGlowSource = GetOrCreateDXGlowMesh(meshFilter.sharedMesh);
			ComputeDXMeshBounds(meshFilter.sharedMesh, tileSize, out dXGameplayVisual.LocalCenter, out dXGameplayVisual.HalfExtents);
			if (!loggedDxVisualInfo)
			{
				loggedDxVisualInfo = true;
				string text = ((sharedMaterials.Length != 0 && sharedMaterials[0] != null && sharedMaterials[0].shader != null) ? sharedMaterials[0].shader.name : "none");
				modEntry.Logger.Log("DX visual mesh: vertices=" + meshFilter.sharedMesh.vertexCount + ", shader=" + text + ", halfExtents=" + dXGameplayVisual.HalfExtents);
			}
		}
		else if (spriteRenderer != null && spriteRenderer.sprite != null)
		{
			GameObject gameObject3 = new GameObject("GradientSpriteSurface");
			gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
			gameObject3.transform.localPosition = new Vector3(0f, 0f, -0.015f);
			SpriteRenderer spriteRenderer2 = gameObject3.AddComponent<SpriteRenderer>();
			spriteRenderer2.sprite = spriteRenderer.sprite;
			Material sharedMaterial = spriteRenderer.sharedMaterial;
			Material surfaceSpriteMaterial = (spriteRenderer2.sharedMaterial = sharedMaterial);
			spriteRenderer2.sortingLayerID = num;
			spriteRenderer2.sortingOrder = num2 + 1;
			dXGameplayVisual.SurfaceSpriteRenderer = spriteRenderer2;
			dXGameplayVisual.SurfaceSpriteMaterial = surfaceSpriteMaterial;
			dXGameplayVisual.SourceSprite = spriteRenderer.sprite;
			Bounds bounds = spriteRenderer.sprite.bounds;
			dXGameplayVisual.LocalCenter = new Vector2(bounds.center.x, bounds.center.y);
			dXGameplayVisual.HalfExtents = new Vector2(bounds.extents.x, bounds.extents.y);
			if (!loggedDxVisualInfo)
			{
				loggedDxVisualInfo = true;
				modEntry.Logger.Log("DX visual sprite: " + spriteRenderer.sprite.name + ", halfExtents=" + dXGameplayVisual.HalfExtents.ToString() + ".");
			}
		}
		else
		{
			float num3 = Mathf.Max(0.1f, tileSize);
			dXGameplayVisual.LocalCenter = Vector2.zero;
			dXGameplayVisual.HalfExtents = new Vector2(num3 * 0.52f, num3 * 0.34f);
			if (!loggedDxVisualInfo)
			{
				loggedDxVisualInfo = true;
				modEntry.Logger.Log("DX visual fallback bounds: " + dXGameplayVisual.HalfExtents);
			}
		}
		ResolveDXOutline(renderer, dXGameplayVisual, tileSize);
		dXGameplayVisual.OutlineRefreshUntil = Time.unscaledTime + 3f;
		dXGameplayVisual.NextOutlineRefreshTime = Time.unscaledTime + 0.1f;
		GameObject gameObject4 = new GameObject("ProceduralEdgeGlow");
		gameObject4.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject4.transform.localPosition = new Vector3(0f, 0f, -0.04f);
		MeshFilter meshFilter2 = gameObject4.AddComponent<MeshFilter>();
		dXGameplayVisual.GlowMesh = new Mesh();
		dXGameplayVisual.GlowMesh.name = "ADOFAIDX_DX_Glow_" + floor.seqID;
		dXGameplayVisual.GlowMesh.MarkDynamic();
		meshFilter2.sharedMesh = dXGameplayVisual.GlowMesh;
		dXGameplayVisual.GlowRenderer = gameObject4.AddComponent<MeshRenderer>();
		dXGameplayVisual.GlowRenderer.sharedMaterial = dxGlowMaterial;
		dXGameplayVisual.GlowRenderer.sortingLayerID = num;
		dXGameplayVisual.GlowRenderer.sortingOrder = num2 + 5;
		GameObject gameObject5 = new GameObject("HotEdge");
		gameObject5.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject5.transform.localPosition = new Vector3(0f, 0f, -0.06f);
		LineRenderer lineRenderer = gameObject5.AddComponent<LineRenderer>();
		lineRenderer.useWorldSpace = false;
		lineRenderer.loop = true;
		lineRenderer.positionCount = ((dXGameplayVisual.OutlinePolygon != null && dXGameplayVisual.OutlinePolygon.Length >= 3) ? dXGameplayVisual.OutlinePolygon.Length : 4);
		lineRenderer.numCapVertices = 2;
		lineRenderer.numCornerVertices = 4;
		lineRenderer.textureMode = LineTextureMode.Stretch;
		lineRenderer.sharedMaterial = dxEdgeMaterial;
		lineRenderer.sortingLayerID = num;
		lineRenderer.sortingOrder = num2 + 6;
		dXGameplayVisual.Inner = lineRenderer;
		CreateDXMeshGlowLayers(dXGameplayVisual, num, num2);
		dXGameplayVisual.CreatedAt = Time.unscaledTime;
		dXGameplayVisual.GeometryReady = false;
		dXGameplayVisual.SourceColliderSignature = ComputeDXColliderSignature(renderer);
		if (meshFilter != null && meshFilter.sharedMesh != null)
		{
			dXGameplayVisual.SourceMesh = meshFilter.sharedMesh;
			dXGameplayVisual.SourceMeshSignature = ComputeDXMeshSignature(dXGameplayVisual.SourceMesh);
		}
		dXGameplayVisual.NextGeometryCheckTime = Time.unscaledTime + 0.1f;
		return dXGameplayVisual;
	}

	private static void CreateDXMeshGlowLayers(DXGameplayVisual visual, int sortingLayerId, int sortingOrder)
	{
		if (dxGlowMaterial == null)
		{
			return;
		}
		float[] array = DXGlowLayerScales;
		Vector3 vector = new Vector3(visual.LocalCenter.x, visual.LocalCenter.y, 0f);
		if (visual.MeshGlowSource != null)
		{
			visual.MeshGlowRenderers = new MeshRenderer[array.Length];
			visual.MeshGlowMaterials = new Material[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				float num = array[i];
				GameObject gameObject = new GameObject("MeshGlow_" + i);
				gameObject.transform.SetParent(visual.Root.transform, worldPositionStays: false);
				gameObject.transform.localPosition = vector * (1f - num) + new Vector3(0f, 0f, -0.03f - (float)i * 0.002f);
				gameObject.transform.localRotation = Quaternion.identity;
				gameObject.transform.localScale = new Vector3(num, num, 1f);
				gameObject.AddComponent<MeshFilter>().sharedMesh = visual.MeshGlowSource;
				MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
				meshRenderer.sharedMaterial = dxGlowMaterial;
				meshRenderer.sortingLayerID = sortingLayerId;
				meshRenderer.sortingOrder = sortingOrder - 1;
				visual.MeshGlowRenderers[i] = meshRenderer;
				visual.MeshGlowMaterials[i] = dxGlowMaterial;
			}
		}
		else if (!(visual.SurfaceSpriteRenderer == null) && !(visual.SurfaceSpriteRenderer.sprite == null))
		{
			visual.MeshGlowSpriteRenderers = new SpriteRenderer[array.Length];
			visual.MeshGlowSpriteMaterials = new Material[array.Length];
			for (int j = 0; j < array.Length; j++)
			{
				float num2 = array[j];
				GameObject gameObject2 = new GameObject("SpriteGlow_" + j);
				gameObject2.transform.SetParent(visual.Root.transform, worldPositionStays: false);
				gameObject2.transform.localPosition = vector * (1f - num2) + new Vector3(0f, 0f, -0.03f - (float)j * 0.002f);
				gameObject2.transform.localRotation = Quaternion.identity;
				gameObject2.transform.localScale = new Vector3(num2, num2, 1f);
				SpriteRenderer spriteRenderer = gameObject2.AddComponent<SpriteRenderer>();
				spriteRenderer.sprite = visual.SurfaceSpriteRenderer.sprite;
				spriteRenderer.sharedMaterial = dxGlowMaterial;
				spriteRenderer.sortingLayerID = sortingLayerId;
				spriteRenderer.sortingOrder = sortingOrder - 1;
				visual.MeshGlowSpriteRenderers[j] = spriteRenderer;
				visual.MeshGlowSpriteMaterials[j] = dxGlowMaterial;
			}
		}
	}

	private static void UpdateDXMeshGlowLayers(DXGameplayVisual visual, Color baseColor, float intensity, float opacity, float time, bool visible)
	{
		if (visual.MeshGlowSpriteRenderers != null)
		{
			UpdateDXSpriteGlowLayers(visual, baseColor, intensity, opacity, time, visible);
		}
		else
		{
			if (visual.MeshGlowRenderers == null || visual.MeshGlowMaterials == null)
			{
				return;
			}
			float[] array = DXGlowLayerAlphas;
			float num = 0.92f + 0.08f * Mathf.Sin(time * 2.1f + (float)visual.Floor.seqID * 0.73f);
			for (int i = 0; i < visual.MeshGlowRenderers.Length; i++)
			{
				MeshRenderer meshRenderer = visual.MeshGlowRenderers[i];
				Material material = visual.MeshGlowMaterials[i];
				if (meshRenderer == null || material == null)
				{
					continue;
				}
				meshRenderer.enabled = visible && intensity > 0.001f && opacity > 0.001f;
				if (meshRenderer.enabled)
				{
					float amount = (float)i / (float)Mathf.Max(1, visual.MeshGlowRenderers.Length - 1);
					Color value = DXAnimatedColor(baseColor, amount);
					value.a = Mathf.Clamp01(array[i] * intensity * opacity * num);
					DXGlowPropertyBlock.Clear();
					if (material.HasProperty("_TintColor"))
					{
						DXGlowPropertyBlock.SetColor("_TintColor", value);
					}
					if (material.HasProperty("_Color"))
					{
						DXGlowPropertyBlock.SetColor("_Color", value);
					}
					meshRenderer.SetPropertyBlock(DXGlowPropertyBlock);
				}
			}
		}
	}

	private static void UpdateDXSpriteGlowLayers(DXGameplayVisual visual, Color baseColor, float intensity, float opacity, float time, bool visible)
	{
		float[] array = DXGlowLayerAlphas;
		float num = 0.92f + 0.08f * Mathf.Sin(time * 2.1f + (float)visual.Floor.seqID * 0.73f);
		for (int i = 0; i < visual.MeshGlowSpriteRenderers.Length; i++)
		{
			SpriteRenderer spriteRenderer = visual.MeshGlowSpriteRenderers[i];
			if (!(spriteRenderer == null))
			{
				spriteRenderer.enabled = visible && intensity > 0.001f && opacity > 0.001f;
				if (spriteRenderer.enabled)
				{
					float amount = (float)i / (float)Mathf.Max(1, visual.MeshGlowSpriteRenderers.Length - 1);
					Color color = DXAnimatedColor(baseColor, amount);
					color.a = Mathf.Clamp01(array[i] * intensity * opacity * num * 1.8f);
					spriteRenderer.color = color;
				}
			}
		}
	}

	private static bool DXSourceGeometryChanged(DXGameplayVisual visual, float time)
	{
		if (visual == null || (UnityEngine.Object)(object)visual.Floor == null || (UnityEngine.Object)(object)visual.Floor.floorRenderer == null || time < visual.NextGeometryCheckTime)
		{
			return false;
		}
		visual.NextGeometryCheckTime = time + 0.5f;
		Renderer renderer = visual.Floor.floorRenderer.renderer;
		if (renderer == null)
		{
			return false;
		}
		MeshFilter component = renderer.GetComponent<MeshFilter>();
		Mesh mesh = ((component == null) ? null : component.sharedMesh);
		int num = ComputeDXMeshSignature(mesh);
		int num2 = ComputeDXColliderSignature(renderer);
		SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
		Sprite sprite = ((spriteRenderer == null) ? null : spriteRenderer.sprite);
		bool num3 = (object)mesh != visual.SourceMesh || num != visual.SourceMeshSignature;
		bool flag = num2 != visual.SourceColliderSignature;
		bool flag2 = (object)sprite != visual.SourceSprite;
		bool flag3 = num3 || flag || flag2;
		if (flag3)
		{
			modEntry.Logger.Log("DX visual geometry changed; rebuilding floor " + visual.Floor.seqID + " (mesh=" + ((visual.SourceMesh == null) ? "null" : visual.SourceMesh.GetInstanceID().ToString()) + " -> " + ((mesh == null) ? "null" : mesh.GetInstanceID().ToString()) + ", signature " + visual.SourceMeshSignature + " -> " + num + ", collider " + visual.SourceColliderSignature + " -> " + num2 + ", spriteChanged=" + flag2 + ").");
		}
		else if (!visual.GeometryReady && time - visual.CreatedAt >= 0.6f)
		{
			visual.GeometryReady = true;
		}
		return flag3;
	}

	private static int ComputeDXMeshSignature(Mesh mesh)
	{
		if (mesh == null)
		{
			return 0;
		}
		Vector3[] vertices = mesh.vertices;
		int num = 17;
		num = num * 31 + mesh.vertexCount;
		if (vertices != null)
		{
			for (int i = 0; i < vertices.Length; i++)
			{
				Vector3 vector = vertices[i];
				num = num * 31 + Mathf.RoundToInt(vector.x * 10000f);
				num = num * 31 + Mathf.RoundToInt(vector.y * 10000f);
				num = num * 31 + Mathf.RoundToInt(vector.z * 10000f);
			}
		}
		return num;
	}

	private static int ComputeDXColliderSignature(Renderer renderer)
	{
		if (renderer == null)
		{
			return 0;
		}
		PolygonCollider2D polygonCollider2D = renderer.GetComponent<PolygonCollider2D>();
		if (polygonCollider2D == null)
		{
			polygonCollider2D = renderer.GetComponentInChildren<PolygonCollider2D>(includeInactive: true);
		}
		if (polygonCollider2D == null)
		{
			return 0;
		}
		int num = 23;
		num = num * 31 + polygonCollider2D.pathCount;
		for (int i = 0; i < polygonCollider2D.pathCount; i++)
		{
			Vector2[] path = polygonCollider2D.GetPath(i);
			if (path != null)
			{
				num = num * 31 + path.Length;
				Vector2[] array = path;
				for (int j = 0; j < array.Length; j++)
				{
					Vector2 vector = array[j];
					num = num * 31 + Mathf.RoundToInt(vector.x * 10000f);
					num = num * 31 + Mathf.RoundToInt(vector.y * 10000f);
				}
			}
		}
		return num;
	}

	private static void ResolveDXOutline(Renderer sourceRenderer, DXGameplayVisual visual, float tileSize)
	{
		Vector2[] array = null;
		bool exactBoundary = false;
		if (sourceRenderer != null)
		{
			MeshFilter component = sourceRenderer.GetComponent<MeshFilter>();
			if (component != null && component.sharedMesh != null)
			{
				array = BuildDXMeshOutline(component.sharedMesh, out exactBoundary);
			}
			PolygonCollider2D polygonCollider2D = sourceRenderer.GetComponent<PolygonCollider2D>();
			if (polygonCollider2D == null)
			{
				polygonCollider2D = sourceRenderer.GetComponentInChildren<PolygonCollider2D>(includeInactive: true);
			}
			if (polygonCollider2D != null && polygonCollider2D.pathCount > 0)
			{
				Vector2[] array2 = null;
				float num = -1f;
				for (int i = 0; i < polygonCollider2D.pathCount; i++)
				{
					Vector2[] path = polygonCollider2D.GetPath(i);
					float num2 = Mathf.Abs(PolygonArea(path));
					if (path != null && path.Length >= 3 && num2 > num)
					{
						num = num2;
						array2 = path;
					}
				}
				if (array2 != null && (array == null || !exactBoundary || array2.Length > array.Length + 1 || Mathf.Abs(PolygonArea(array2)) > Mathf.Abs(PolygonArea(array)) * 1.08f))
				{
					array = array2;
					exactBoundary = true;
				}
			}
		}
		if (array == null || array.Length < 3)
		{
			float num3 = Mathf.Max(0.002f, tileSize * 0.002f);
			float x = visual.LocalCenter.x - Mathf.Max(0.02f, visual.HalfExtents.x + num3);
			float x2 = visual.LocalCenter.x + Mathf.Max(0.02f, visual.HalfExtents.x + num3);
			float y = visual.LocalCenter.y - Mathf.Max(0.02f, visual.HalfExtents.y + num3);
			float y2 = visual.LocalCenter.y + Mathf.Max(0.02f, visual.HalfExtents.y + num3);
			array = new Vector2[4]
			{
				new Vector2(x, y),
				new Vector2(x, y2),
				new Vector2(x2, y2),
				new Vector2(x2, y)
			};
		}
		if (!PolygonsApproximatelyEqual(visual.OutlinePolygon, array))
		{
			visual.OutlinePolygon = array;
			visual.GlowGridPositions = null;
			visual.GlowGridDistances = null;
			visual.OutlineLinePositions = array.Select((Vector2 point) => new Vector3(point.x, point.y, 0f)).ToArray();
		}
	}

	private static Vector2[] BuildDXMeshOutline(Mesh mesh, out bool exactBoundary)
	{
		exactBoundary = false;
		Vector3[] vertices = mesh.vertices;
		Vector2[] uv = mesh.uv;
		int[] triangles = mesh.triangles;
		if (vertices == null || vertices.Length == 0)
		{
			return null;
		}
		bool flag = uv != null && uv.Length == vertices.Length;
		List<Vector2> list = new List<Vector2>(vertices.Length);
		bool[] array = new bool[vertices.Length];
		for (int i = 0; i < vertices.Length; i++)
		{
			if (flag && uv[i].y > 1.02f)
			{
				continue;
			}
			array[i] = true;
			Vector2 vector = new Vector2(vertices[i].x, vertices[i].y);
			bool flag2 = false;
			for (int j = 0; j < list.Count; j++)
			{
				if (Vector2.SqrMagnitude(list[j] - vector) < 1E-07f)
				{
					flag2 = true;
					break;
				}
			}
			if (!flag2)
			{
				list.Add(vector);
			}
		}
		if (triangles != null && triangles.Length >= 3)
		{
			Vector2[] array2 = BuildMeshBoundaryOutline(vertices, triangles, array);
			if (array2 != null && array2.Length >= 3)
			{
				exactBoundary = true;
				return array2;
			}
		}
		return BuildConvexHull(list);
	}

	private static Vector2[] BuildMeshBoundaryOutline(Vector3[] vertices, int[] triangles, bool[] bodyVertices)
	{
		Dictionary<long, int> dictionary = new Dictionary<long, int>();
		List<Vector2> weldedPoints = new List<Vector2>(vertices.Length);
		int[] array = new int[vertices.Length];
		for (int i = 0; i < vertices.Length; i++)
		{
			int num = Mathf.RoundToInt(vertices[i].x * 10000f);
			int num2 = Mathf.RoundToInt(vertices[i].y * 10000f);
			long key = ((long)num << 32) | (uint)num2;
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = (dictionary[key] = weldedPoints.Count);
				weldedPoints.Add(new Vector2(vertices[i].x, vertices[i].y));
			}
			array[i] = value;
		}
		Dictionary<long, int> dictionary2 = new Dictionary<long, int>();
		for (int j = 0; j + 2 < triangles.Length; j += 3)
		{
			int num3 = triangles[j];
			int num4 = triangles[j + 1];
			int num5 = triangles[j + 2];
			if (num3 >= 0 && num4 >= 0 && num5 >= 0 && num3 < vertices.Length && num4 < vertices.Length && num5 < vertices.Length && bodyVertices[num3] && bodyVertices[num4] && bodyVertices[num5])
			{
				AddEdgeCount(dictionary2, array[num3], array[num4]);
				AddEdgeCount(dictionary2, array[num4], array[num5]);
				AddEdgeCount(dictionary2, array[num5], array[num3]);
			}
		}
		HashSet<long> hashSet = new HashSet<long>(from pair in dictionary2
			where pair.Value == 1
			select pair.Key);
		if (hashSet.Count < 3)
		{
			return null;
		}
		Dictionary<int, List<int>> dictionary3 = new Dictionary<int, List<int>>();
		foreach (long item2 in hashSet)
		{
			int num6 = (int)(item2 >> 32);
			int num7 = (int)(item2 & 0xFFFFFFFFu);
			GetOrAddNeighbors(dictionary3, num6).Add(num7);
			GetOrAddNeighbors(dictionary3, num7).Add(num6);
		}
		Vector2[] result = null;
		float num8 = -1f;
		while (hashSet.Count > 0)
		{
			long num9 = hashSet.First();
			hashSet.Remove(num9);
			int num10 = (int)(num9 >> 32);
			int num11 = num10;
			int num12 = (int)(num9 & 0xFFFFFFFFu);
			List<int> list = new List<int> { num10 };
			for (int num13 = 0; num13 <= dictionary2.Count; num13++)
			{
				if (num12 == num10)
				{
					break;
				}
				list.Add(num12);
				if (!dictionary3.TryGetValue(num12, out var value2))
				{
					list.Clear();
					break;
				}
				int num14 = -1;
				foreach (int item3 in value2)
				{
					if (item3 != num11)
					{
						long item = MakeEdgeKey(num12, item3);
						if (hashSet.Contains(item))
						{
							num14 = item3;
							hashSet.Remove(item);
							break;
						}
					}
				}
				num11 = num12;
				num12 = num14;
				if (num12 < 0)
				{
					list.Clear();
					break;
				}
			}
			if (num12 == num10 && list.Count >= 3)
			{
				Vector2[] array2 = list.Select((int index) => weldedPoints[index]).ToArray();
				float num15 = Mathf.Abs(PolygonArea(array2));
				if (num15 > num8)
				{
					num8 = num15;
					result = array2;
				}
			}
		}
		return result;
	}

	private static long MakeEdgeKey(int a, int b)
	{
		int num = Mathf.Min(a, b);
		int num2 = Mathf.Max(a, b);
		return ((long)num << 32) | (uint)num2;
	}

	private static void AddEdgeCount(Dictionary<long, int> edgeCounts, int a, int b)
	{
		if (a != b)
		{
			long key = MakeEdgeKey(a, b);
			edgeCounts.TryGetValue(key, out var value);
			edgeCounts[key] = value + 1;
		}
	}

	private static List<int> GetOrAddNeighbors(Dictionary<int, List<int>> adjacency, int vertex)
	{
		if (!adjacency.TryGetValue(vertex, out var value))
		{
			value = (adjacency[vertex] = new List<int>(2));
		}
		return value;
	}

	private static Vector2[] BuildConvexHull(List<Vector2> points)
	{
		if (points == null || points.Count < 3)
		{
			return null;
		}
		points.Sort((Vector2 a, Vector2 b) => (a.x != b.x) ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
		List<Vector2> list = new List<Vector2>(points.Count * 2);
		for (int num = 0; num < points.Count; num++)
		{
			while (list.Count >= 2 && CrossProduct(list[list.Count - 2], list[list.Count - 1], points[num]) <= 0f)
			{
				list.RemoveAt(list.Count - 1);
			}
			list.Add(points[num]);
		}
		int num2 = list.Count + 1;
		for (int num3 = points.Count - 2; num3 >= 0; num3--)
		{
			while (list.Count >= num2 && CrossProduct(list[list.Count - 2], list[list.Count - 1], points[num3]) <= 0f)
			{
				list.RemoveAt(list.Count - 1);
			}
			list.Add(points[num3]);
		}
		if (list.Count > 1)
		{
			list.RemoveAt(list.Count - 1);
		}
		if (list.Count < 3)
		{
			return null;
		}
		return list.ToArray();
	}

	private static float CrossProduct(Vector2 a, Vector2 b, Vector2 c)
	{
		return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
	}

	private static bool PolygonsApproximatelyEqual(Vector2[] first, Vector2[] second)
	{
		if (first == null || second == null || first.Length != second.Length)
		{
			return false;
		}
		for (int i = 0; i < first.Length; i++)
		{
			if (Vector2.SqrMagnitude(first[i] - second[i]) > 1E-06f)
			{
				return false;
			}
		}
		return true;
	}

	private static float PolygonArea(Vector2[] polygon)
	{
		if (polygon == null || polygon.Length < 3)
		{
			return 0f;
		}
		float num = 0f;
		for (int i = 0; i < polygon.Length; i++)
		{
			Vector2 vector = polygon[i];
			Vector2 vector2 = polygon[(i + 1) % polygon.Length];
			num += vector.x * vector2.y - vector2.x * vector.y;
		}
		return num * 0.5f;
	}

	private static void SetDXLineRectangle(DXGameplayVisual visual, float tileSize)
	{
		if (!(visual.Inner == null) && visual.OutlineLinePositions != null && visual.OutlineLinePositions.Length >= 3)
		{
			if (visual.Inner.positionCount != visual.OutlineLinePositions.Length)
			{
				visual.Inner.positionCount = visual.OutlineLinePositions.Length;
			}
			visual.Inner.SetPositions(visual.OutlineLinePositions);
		}
	}

	private static void CreateGameplayDxText(DXGameplayVisual visual)
	{
		if (visual != null && !(visual.Root == null))
		{
			EnsureGameplayDxSprite();
			int sortingLayerID = 0;
			SortingLayer[] layers = SortingLayer.layers;
			if (layers != null && layers.Length != 0)
			{
				sortingLayerID = layers[layers.Length - 1].id;
			}
			Vector3 position = new Vector3(visual.LocalCenter.x, visual.LocalCenter.y, 0f);
			Vector3 vector = visual.Root.transform.TransformPoint(position);
			visual.GameplayTextRoot = new GameObject("ADOFAIDX_GameplayText_" + visual.Floor.seqID);
			UpdateGameplayDxTextPosition(visual);
			GameObject gameObject = new GameObject("DX_Sprite");
			gameObject.transform.SetParent(visual.GameplayTextRoot.transform, worldPositionStays: false);
			SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = dxGameplayTextSprite;
			spriteRenderer.color = Color.white;
			spriteRenderer.sortingLayerID = sortingLayerID;
			spriteRenderer.sortingOrder = 30002;
			if (!loggedGameplayDxText)
			{
				loggedGameplayDxText = true;
				modEntry.Logger.Log("Gameplay DX sprite created: floor=" + visual.Floor.seqID + ", layer=" + sortingLayerID + ", order=" + 30002 + ", center=" + vector.ToString() + ", cameraZ=" + ((Camera.main != null) ? Camera.main.transform.position.z : 0f));
			}
		}
	}

	private static void UpdateGameplayDxTextPosition(DXGameplayVisual visual)
	{
		if (visual == null || visual.Root == null || visual.GameplayTextRoot == null)
		{
			return;
		}
		Vector3 position = new Vector3(visual.LocalCenter.x, visual.LocalCenter.y, 0f);
		Vector3 vector = visual.Root.transform.TransformPoint(position);
		Camera main = Camera.main;
		if (main != null)
		{
			Vector3 vector2 = main.transform.position - vector;
			if (vector2.sqrMagnitude > 0.0001f)
			{
				vector += vector2.normalized * 0.35f;
			}
			visual.GameplayTextRoot.transform.rotation = Quaternion.LookRotation(vector - main.transform.position, main.transform.up);
		}
		else
		{
			vector.z -= 0.35f;
			visual.GameplayTextRoot.transform.rotation = Quaternion.identity;
		}
		visual.GameplayTextRoot.transform.position = vector;
		float num = Mathf.Clamp(visual.HalfExtents.x * 0.56f, 0.12f, 2f);
		visual.GameplayTextRoot.transform.localScale = new Vector3(num, num, 1f);
	}

	private static void EnsureGameplayDxSprite()
	{
		if (!(dxGameplayTextSprite != null))
		{
			Color32[] array = new Color32[32768];
			DrawDXGlyph(array, 256, 128, new Vector2(3f, 3f), new Color32(0, 0, 0, 150), 12f);
			DrawDXGlyph(array, 256, 128, Vector2.zero, new Color32(16, 10, 4, byte.MaxValue), 9.5f);
			DrawDXGlyph(array, 256, 128, Vector2.zero, new Color32(byte.MaxValue, 198, 61, byte.MaxValue), 5.2f);
			dxGameplayTextTexture = new Texture2D(256, 128, TextureFormat.RGBA32, mipChain: false);
			dxGameplayTextTexture.name = "ADOFAIDX_DX_Text";
			dxGameplayTextTexture.filterMode = FilterMode.Bilinear;
			dxGameplayTextTexture.wrapMode = TextureWrapMode.Clamp;
			dxGameplayTextTexture.SetPixels32(array);
			dxGameplayTextTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			dxGameplayTextSprite = Sprite.Create(dxGameplayTextTexture, new Rect(0f, 0f, 256f, 128f), new Vector2(0.5f, 0.5f), 128f);
			dxGameplayTextSprite.name = "ADOFAIDX_DX_Text";
		}
	}

	private static void DrawDXGlyph(Color32[] pixels, int width, int height, Vector2 offset, Color32 color, float halfWidth)
	{
		Vector2[] array = new Vector2[8]
		{
			new Vector2(46f, 22f),
			new Vector2(108f, 22f),
			new Vector2(138f, 38f),
			new Vector2(149f, 64f),
			new Vector2(138f, 90f),
			new Vector2(108f, 106f),
			new Vector2(46f, 106f),
			new Vector2(46f, 22f)
		};
		for (int i = 1; i < array.Length; i++)
		{
			DrawDXStroke(pixels, width, height, array[i - 1] + offset, array[i] + offset, halfWidth, color);
		}
		DrawDXStroke(pixels, width, height, new Vector2(166f, 23f) + offset, new Vector2(231f, 105f) + offset, halfWidth, color);
		DrawDXStroke(pixels, width, height, new Vector2(231f, 23f) + offset, new Vector2(166f, 105f) + offset, halfWidth, color);
	}

	private static void DrawDXStroke(Color32[] pixels, int width, int height, Vector2 start, Vector2 end, float halfWidth, Color32 color)
	{
		int num = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(start.x, end.x) - halfWidth - 2f), 0, width - 1);
		int num2 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(start.x, end.x) + halfWidth + 2f), 0, width - 1);
		int num3 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(start.y, end.y) - halfWidth - 2f), 0, height - 1);
		int num4 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(start.y, end.y) + halfWidth + 2f), 0, height - 1);
		Vector2 vector = end - start;
		float num5 = Mathf.Max(0.0001f, vector.sqrMagnitude);
		for (int i = num3; i <= num4; i++)
		{
			for (int j = num; j <= num2; j++)
			{
				Vector2 vector2 = new Vector2((float)j + 0.5f, (float)i + 0.5f);
				float num6 = Mathf.Clamp01(Vector2.Dot(vector2 - start, vector) / num5);
				Vector2 b = start + vector * num6;
				float num7 = Vector2.Distance(vector2, b);
				float num8 = Mathf.Clamp01(halfWidth + 1f - num7);
				if (!(num8 <= 0f))
				{
					BlendDXTextPixel(pixels, i * width + j, color, num8);
				}
			}
		}
	}

	private static void BlendDXTextPixel(Color32[] pixels, int index, Color32 source, float coverage)
	{
		Color32 color = pixels[index];
		float num = (float)(int)source.a / 255f * Mathf.Clamp01(coverage);
		float num2 = (float)(int)color.a / 255f;
		float num3 = num + num2 * (1f - num);
		if (num3 <= 0.0001f)
		{
			pixels[index] = new Color32(0, 0, 0, 0);
			return;
		}
		float num4 = num2 * (1f - num);
		pixels[index] = new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(((float)(int)source.r * num + (float)(int)color.r * num4) / num3), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(((float)(int)source.g * num + (float)(int)color.g * num4) / num3), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(((float)(int)source.b * num + (float)(int)color.b * num4) / num3), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(num3 * 255f), 0, 255));
	}

	private static void UpdateDXFloatingMotion(DXGameplayVisual visual, float time, float tileSize, bool judged, bool floatingEnabled)
	{
		if (visual == null || visual.Root == null)
		{
			return;
		}
		Transform transform = visual.Root.transform;
		if (!floatingEnabled)
		{
			visual.Judged = judged;
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;
			transform.localScale = Vector3.one;
			return;
		}
		float num = (float)visual.Floor.seqID * 0.91f;
		if (!judged)
		{
			visual.Judged = false;
			float num2 = Mathf.Clamp((settings == null) ? 1f : settings.gameplayDXFloatAmplitude, 1f, 3f);
			float num3 = Mathf.Sin(time * 1.15f + num) * tileSize * 0.022f + Mathf.Sin(time * 0.57f + num * 1.9f) * tileSize * 0.005f;
			float num4 = Mathf.Sin(time * 1.62f + num * 1.41f) * tileSize * 0.034f + Mathf.Sin(time * 0.71f + num) * tileSize * 0.007f;
			float num5 = Mathf.Sin(time * 1.27f + num) * 0.75f;
			float num6 = 1f + Mathf.Sin(time * 1.08f + num * 2.1f) * 0.006f;
			num3 *= num2;
			num4 *= num2;
			num5 *= num2;
			num6 = 1f + (num6 - 1f) * num2;
			transform.localPosition = new Vector3(num3, num4, 0f);
			transform.localRotation = Quaternion.Euler(0f, 0f, num5);
			transform.localScale = new Vector3(num6, num6, 1f);
		}
		else
		{
			if (!visual.Judged)
			{
				visual.Judged = true;
				visual.FreezeStartedAt = time;
				visual.FreezeOffset = transform.localPosition;
				visual.FreezeRotation = transform.localRotation;
				visual.FreezeScale = transform.localScale;
			}
			float num7 = Mathf.Clamp01((time - visual.FreezeStartedAt) / 0.16f);
			float t = num7 * num7 * (3f - 2f * num7);
			transform.localPosition = Vector3.Lerp(visual.FreezeOffset, Vector3.zero, t);
			transform.localRotation = Quaternion.Slerp(visual.FreezeRotation, Quaternion.identity, t);
			transform.localScale = Vector3.Lerp(visual.FreezeScale, Vector3.one, t);
		}
	}

	private static void EnsureDXMaterials()
	{
		if (dxEdgeMaterial == null)
		{
			Shader shader = Shader.Find("Sprites/Default");
			if (shader == null)
			{
				shader = Shader.Find("Unlit/Transparent");
			}
			if (shader == null)
			{
				shader = Shader.Find("Unlit/Color");
			}
			if (shader != null)
			{
				dxEdgeMaterial = new Material(shader);
				dxEdgeMaterial.hideFlags = HideFlags.HideAndDontSave;
				dxEdgeMaterial.mainTexture = Texture2D.whiteTexture;
				if (dxEdgeMaterial.HasProperty("_Color"))
				{
					dxEdgeMaterial.SetColor("_Color", Color.white);
				}
				dxEdgeMaterial.renderQueue = 3000;
			}
		}
		if (!(dxGlowMaterial == null))
		{
			return;
		}
		Shader shader2 = Shader.Find("Legacy Shaders/Particles/Additive");
		if (shader2 == null)
		{
			shader2 = Shader.Find("Mobile/Particles/Additive");
		}
		if (shader2 == null)
		{
			shader2 = Shader.Find("Particles/Additive");
		}
		if (shader2 == null)
		{
			shader2 = Shader.Find("Sprites/Default");
		}
		if (shader2 != null)
		{
			dxGlowMaterial = new Material(shader2);
			dxGlowMaterial.hideFlags = HideFlags.HideAndDontSave;
			dxGlowMaterial.mainTexture = Texture2D.whiteTexture;
			if (dxGlowMaterial.HasProperty("_TintColor"))
			{
				dxGlowMaterial.SetColor("_TintColor", Color.white);
			}
			if (dxGlowMaterial.HasProperty("_Color"))
			{
				dxGlowMaterial.SetColor("_Color", Color.white);
			}
			if (dxGlowMaterial.HasProperty("_Cull"))
			{
				dxGlowMaterial.SetInt("_Cull", 0);
			}
			dxGlowMaterial.renderQueue = 3000;
			modEntry.Logger.Log("DX glow shader: " + shader2.name);
		}
	}

	private static Color GetDXNoteBaseColor()
	{
		return new Color((float)Mathf.Clamp(settings.dxNoteColorR, 0, 255) / 255f, (float)Mathf.Clamp(settings.dxNoteColorG, 0, 255) / 255f, (float)Mathf.Clamp(settings.dxNoteColorB, 0, 255) / 255f, 1f);
	}

	private static Color DXAnimatedColor(Color baseColor, float amount)
	{
		Color.RGBToHSV(baseColor, out float H, out float S, out float V);
		float num = Mathf.Clamp((settings == null) ? 1f : settings.dxColorVariation, 1f, 3f);
		float h = Mathf.Repeat(H + Mathf.Lerp(0.018f, -0.012f, amount) * num, 1f);
		float s = Mathf.Clamp01(S + Mathf.Lerp(-0.08f, 0.08f, amount) * num);
		float v = Mathf.Clamp01(V + Mathf.Lerp(0.16f, -0.13f, amount) * num);
		Color result = Color.HSVToRGB(h, s, v);
		result.a = 1f;
		return result;
	}

	private static void SetDXSurfaceColor(DXGameplayVisual visual, Color color)
	{
		if (visual.SurfaceSpriteRenderer != null)
		{
			visual.SurfaceSpriteRenderer.color = color;
		}
		else if (!(visual.SurfaceMaterial == null))
		{
			DXSurfacePropertyBlock.Clear();
			int num = scrFloor.ShaderProperty_Color;
			if (num == 0)
			{
				num = Shader.PropertyToID("_Color");
			}
			if (visual.SurfaceMaterial.HasProperty(num))
			{
				DXSurfacePropertyBlock.SetColor(num, color);
			}
			int num2 = scrFloor.ShaderProperty_Alpha;
			if (num2 == 0)
			{
				num2 = Shader.PropertyToID("_Alpha");
			}
			if (visual.SurfaceMaterial.HasProperty(num2) && (UnityEngine.Object)(object)visual.Floor != null)
			{
				DXSurfacePropertyBlock.SetFloat(num2, Mathf.Clamp01(visual.Floor.opacity));
			}
			if (visual.SurfaceRenderer != null)
			{
				visual.SurfaceRenderer.SetPropertyBlock(DXSurfacePropertyBlock);
			}
		}
	}

	private static void ApplyDXMeshVertexColors(Mesh mesh)
	{
		Vector3[] vertices = mesh.vertices;
		Vector2[] uv = mesh.uv;
		if (vertices == null || vertices.Length == 0)
		{
			return;
		}
		bool flag = uv != null && uv.Length == vertices.Length;
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		float num4 = float.MinValue;
		int num5 = 0;
		for (int i = 0; i < vertices.Length; i++)
		{
			if (!flag || !(uv[i].y > 1.02f))
			{
				Vector3 vector = vertices[i];
				num = Mathf.Min(num, vector.x);
				num2 = Mathf.Min(num2, vector.y);
				num3 = Mathf.Max(num3, vector.x);
				num4 = Mathf.Max(num4, vector.y);
				num5++;
			}
		}
		if (num5 == 0)
		{
			Bounds bounds = mesh.bounds;
			num = bounds.min.x;
			num2 = bounds.min.y;
			num3 = bounds.max.x;
			num4 = bounds.max.y;
			flag = false;
		}
		float num6 = Mathf.Max(0.0001f, (num3 - num) * 0.5f);
		float num7 = Mathf.Max(0.0001f, (num4 - num2) * 0.5f);
		float num8 = (num + num3) * 0.5f;
		float num9 = (num2 + num4) * 0.5f;
		Color[] array = new Color[vertices.Length];
		for (int j = 0; j < vertices.Length; j++)
		{
			if (flag && uv[j].y > 1.02f)
			{
				array[j] = Color.white;
				continue;
			}
			float num10 = Mathf.Clamp((vertices[j].x - num8) / num6, -1f, 1f);
			float num11 = Mathf.Clamp((vertices[j].y - num9) / num7, -1f, 1f);
			float num12 = Mathf.Clamp01(Mathf.Sqrt(num10 * num10 * 0.72f + num11 * num11 * 0.72f));
			Color a = new Color(1f, 1f, 1f, 1f);
			Color color = new Color(0.96f, 0.96f, 0.96f, 1f);
			Color b = new Color(0.7f, 0.7f, 0.7f, 1f);
			Color color2 = ((num12 < 0.58f) ? Color.Lerp(a, color, num12 / 0.58f) : Color.Lerp(color, b, (num12 - 0.58f) / 0.42f));
			float num13 = Mathf.Clamp01(1f - Mathf.Abs(num10 * 0.82f - num11 * 0.34f));
			num13 *= num13 * 0.08f;
			color2 = new Color(Mathf.Clamp01(color2.r + num13), Mathf.Clamp01(color2.g + num13), Mathf.Clamp01(color2.b + num13), 1f);
			array[j] = color2;
		}
		mesh.colors = array;
	}

	private static void ComputeDXMeshBounds(Mesh mesh, float tileSize, out Vector2 center, out Vector2 halfExtents)
	{
		Vector3[] vertices = mesh.vertices;
		Vector2[] uv = mesh.uv;
		bool flag = uv != null && vertices != null && uv.Length == vertices.Length;
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		float num4 = float.MinValue;
		int num5 = 0;
		if (vertices != null)
		{
			for (int i = 0; i < vertices.Length; i++)
			{
				if (!flag || !(uv[i].y > 1.02f))
				{
					num = Mathf.Min(num, vertices[i].x);
					num2 = Mathf.Min(num2, vertices[i].y);
					num3 = Mathf.Max(num3, vertices[i].x);
					num4 = Mathf.Max(num4, vertices[i].y);
					num5++;
				}
			}
		}
		if (num5 == 0)
		{
			Bounds bounds = mesh.bounds;
			num = bounds.min.x;
			num2 = bounds.min.y;
			num3 = bounds.max.x;
			num4 = bounds.max.y;
		}
		float num6 = Mathf.Max(0.1f, tileSize);
		center = new Vector2((num + num3) * 0.5f, (num2 + num4) * 0.5f);
		halfExtents = new Vector2(Mathf.Clamp((num3 - num) * 0.5f, num6 * 0.2f, num6 * 0.8f), Mathf.Clamp((num4 - num2) * 0.5f, num6 * 0.12f, num6 * 0.65f));
	}

	private static void BuildDXProceduralGlow(DXGameplayVisual visual, float time, float intensity, float tileSize, Color noteBaseColor, float opacity)
	{
		if (visual.GlowMesh == null || visual.GlowRenderer == null)
		{
			return;
		}
		if (intensity <= 0.001f)
		{
			visual.GlowRenderer.enabled = false;
			return;
		}
		visual.GlowRenderer.enabled = true;
		if (visual.GlowGridPositions == null)
		{
			InitializeDXGlowGeometry(visual, tileSize);
		}
		if (visual.GlowGridPositions == null)
		{
			return;
		}
		float num = Mathf.Max(0.1f, tileSize);
		float num2 = 0.96f + 0.04f * Mathf.Sin(time * 1.65f + (float)visual.Floor.seqID * 0.71f);
		float num3 = num * (0.18f + 0.24f * intensity) * num2;
		float num4 = 0.92f + 0.08f * Mathf.Sin(time * 1.9f + (float)visual.Floor.seqID * 0.57f);
		Color.RGBToHSV(noteBaseColor, out float H, out float S, out float V);
		Color a = Color.HSVToRGB(Mathf.Repeat(H + 0.012f, 1f), Mathf.Clamp01(S * 0.45f), Mathf.Clamp01(V + 0.2f));
		Color color = DXAnimatedColor(noteBaseColor, 0.52f);
		Color b = Color.HSVToRGB(Mathf.Repeat(H - 0.01f, 1f), Mathf.Clamp01(S + 0.04f), Mathf.Clamp01(V - 0.18f));
		visual.GlowColors.Clear();
		for (int i = 0; i < visual.GlowGridPositions.Length; i++)
		{
			float num5 = visual.GlowGridDistances[i];
			Color item = new Color(0f, 0f, 0f, 0f);
			if (num5 >= 0f)
			{
				float num6 = Mathf.Clamp01(num5 / num3);
				float f = 1f - num6;
				float num7 = Mathf.Pow(f, 1.8f) * 0.16f;
				float num8 = Mathf.Pow(f, 4.2f) * 0.46f;
				float a2 = Mathf.Clamp01(intensity * (num7 + num8) * num4 * Mathf.Clamp01(opacity));
				item = ((num6 < 0.46f) ? Color.Lerp(a, color, num6 / 0.46f) : Color.Lerp(color, b, (num6 - 0.46f) / 0.54f));
				item.a = a2;
			}
			visual.GlowColors.Add(item);
		}
		visual.GlowMesh.SetColors(visual.GlowColors);
	}

	private static void InitializeDXGlowGeometry(DXGameplayVisual visual, float tileSize)
	{
		Vector2[] outlinePolygon = visual.OutlinePolygon;
		if (outlinePolygon == null || outlinePolygon.Length < 3)
		{
			return;
		}
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		float num4 = float.MinValue;
		Vector2[] array = outlinePolygon;
		for (int i = 0; i < array.Length; i++)
		{
			Vector2 vector = array[i];
			num = Mathf.Min(num, vector.x);
			num2 = Mathf.Min(num2, vector.y);
			num3 = Mathf.Max(num3, vector.x);
			num4 = Mathf.Max(num4, vector.y);
		}
		float num5 = Mathf.Max(0.1f, tileSize) * 0.48f;
		num -= num5;
		num2 -= num5;
		num3 += num5;
		num4 += num5;
		int num6 = 41;
		int num7 = num6 * 33;
		visual.GlowGridPositions = new Vector2[num7];
		visual.GlowGridDistances = new float[num7];
		visual.GlowVertices.Clear();
		visual.GlowColors.Clear();
		visual.GlowTriangles.Clear();
		int num8 = 0;
		for (int j = 0; j <= 32; j++)
		{
			float t = (float)j / 32f;
			float y = Mathf.Lerp(num2, num4, t);
			for (int k = 0; k <= 40; k++)
			{
				float t2 = (float)k / 40f;
				Vector2 vector2 = new Vector2(Mathf.Lerp(num, num3, t2), y);
				bool flag = IsPointInsidePolygon(vector2, outlinePolygon);
				visual.GlowGridPositions[num8] = vector2;
				visual.GlowGridDistances[num8] = (flag ? (-1f) : DistanceToPolygonEdges(vector2, outlinePolygon));
				visual.GlowVertices.Add(vector2);
				visual.GlowColors.Add(new Color(0f, 0f, 0f, 0f));
				num8++;
			}
		}
		for (int l = 0; l < 32; l++)
		{
			for (int m = 0; m < 40; m++)
			{
				int num9 = l * num6 + m;
				int num10 = num9 + 1;
				int num11 = num9 + num6;
				int num12 = num11 + 1;
				AddDXGlowTriangle(visual, num9, num10, num12);
				AddDXGlowTriangle(visual, num9, num12, num11);
				AddDXGlowTriangle(visual, num9, num12, num10);
				AddDXGlowTriangle(visual, num9, num11, num12);
			}
		}
		visual.GlowMesh.SetVertices(visual.GlowVertices);
		visual.GlowMesh.SetColors(visual.GlowColors);
		visual.GlowMesh.SetTriangles(visual.GlowTriangles, 0);
		visual.GlowMesh.RecalculateBounds();
	}

	private static float DistanceToPolygonEdges(Vector2 point, Vector2[] polygon)
	{
		float num = float.MaxValue;
		for (int i = 0; i < polygon.Length; i++)
		{
			Vector2 vector = polygon[i];
			Vector2 vector2 = polygon[(i + 1) % polygon.Length] - vector;
			float num2 = Mathf.Max(1E-06f, vector2.sqrMagnitude);
			float num3 = Mathf.Clamp01(Vector2.Dot(point - vector, vector2) / num2);
			Vector2 b = vector + vector2 * num3;
			num = Mathf.Min(num, Vector2.Distance(point, b));
		}
		return num;
	}

	private static bool IsPointInsidePolygon(Vector2 point, Vector2[] polygon)
	{
		bool flag = false;
		int num = 0;
		int num2 = polygon.Length - 1;
		while (num < polygon.Length)
		{
			Vector2 vector = polygon[num];
			Vector2 vector2 = polygon[num2];
			if (vector.y > point.y != vector2.y > point.y && point.x < (vector2.x - vector.x) * (point.y - vector.y) / (vector2.y - vector.y) + vector.x)
			{
				flag = !flag;
			}
			num2 = num++;
		}
		return flag;
	}

	private static void AddDXGlowTriangle(DXGameplayVisual visual, int a, int b, int c)
	{
		visual.GlowTriangles.Add(a);
		visual.GlowTriangles.Add(b);
		visual.GlowTriangles.Add(c);
	}

	private static void DestroyDXGameplayVisual(int floorId, DXGameplayVisual visual, bool restoreColor)
	{
		if (visual == null)
		{
			dxGameplayVisuals.Remove(floorId);
			return;
		}
		if (visual.GlowMesh != null)
		{
			UnityEngine.Object.Destroy(visual.GlowMesh);
		}
		if (visual.GameplayTextRoot != null)
		{
			UnityEngine.Object.Destroy(visual.GameplayTextRoot);
		}
		if (visual.Root != null)
		{
			UnityEngine.Object.Destroy(visual.Root);
		}
		dxGameplayVisuals.Remove(floorId);
	}

	private static void DestroyDXGameplayVisuals(bool restoreColors)
	{
		foreach (int item in dxGameplayVisuals.Keys.ToList())
		{
			DestroyDXGameplayVisual(item, dxGameplayVisuals[item], restoreColors);
		}
		dxGameplayVisuals.Clear();
		ClearDXMeshCaches();
	}

	private static void SyncDxMarkerObjects()
	{
		if (settings == null || !settings.enableDXNotes)
		{
			DestroyDxMarkerObjects();
			return;
		}
		scnEditor instance = scnEditor.instance;
		bool flag = (UnityEngine.Object)(object)instance != null && !instance.playMode;
		if (!flag)
		{
			DestroyDxMarkerObjects();
			return;
		}
		scrLevelMaker instance2 = scrLevelMaker.instance;
		if ((UnityEngine.Object)(object)instance2 == null || instance2.listFloors == null)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (scrFloor listFloor in instance2.listFloors)
		{
			if ((UnityEngine.Object)(object)listFloor == null || listFloor.seqID <= 0 || !markedFloors.Contains(listFloor.seqID))
			{
				continue;
			}
			hashSet.Add(listFloor.seqID);
			if (!dxMarkerObjects.TryGetValue(listFloor.seqID, out var value) || value == null)
			{
				value = CreateDxMarker(listFloor);
				dxMarkerObjects[listFloor.seqID] = value;
			}
			if (flag || !dxGameplayVisuals.TryGetValue(listFloor.seqID, out var value2) || !(value2.Root != null))
			{
				value.transform.position = ((Component)(object)listFloor).transform.position + new Vector3(0f, 0f, -0.08f);
			}
			if ((UnityEngine.Object)(object)listFloor.floorRenderer != null)
			{
				int sortingLayerID = listFloor.floorRenderer.renderer.sortingLayerID;
				int sortingOrder = listFloor.floorRenderer.renderer.sortingOrder;
				MeshRenderer[] componentsInChildren = value.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
				foreach (MeshRenderer obj in componentsInChildren)
				{
					obj.sortingLayerID = sortingLayerID;
					obj.sortingOrder = ((obj.gameObject.name == "DX_Main") ? (sortingOrder + 22) : (sortingOrder + 21));
				}
			}
		}
		foreach (int item in dxMarkerObjects.Keys.ToList())
		{
			if (!hashSet.Contains(item))
			{
				GameObject gameObject = dxMarkerObjects[item];
				if (gameObject != null)
				{
					UnityEngine.Object.Destroy(gameObject);
				}
				dxMarkerObjects.Remove(item);
			}
		}
	}

	private static GameObject CreateDxMarker(scrFloor floor)
	{
		GameObject gameObject = new GameObject("ADOFAIDX_DX_" + floor.seqID);
		gameObject.transform.position = ((Component)(object)floor).transform.position + new Vector3(0f, 0f, -0.08f);
		Font builtinResource = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		if (builtinResource == null)
		{
			builtinResource = Resources.GetBuiltinResource<Font>("Arial.ttf");
		}
		Color color = new Color(0.03f, 0.04f, 0.06f, 0.92f);
		Color color2 = new Color(1f, 0.67f, 0.18f, 1f);
		float num = 0.009f;
		int sortingLayerID = floor.floorRenderer.renderer.sortingLayerID;
		int sortingOrder = floor.floorRenderer.renderer.sortingOrder;
		Vector2[] array = new Vector2[5]
		{
			new Vector2(0f - num, 0f),
			new Vector2(num, 0f),
			new Vector2(0f, 0f - num),
			new Vector2(0f, num),
			new Vector2(num, 0f - num)
		};
		for (int i = 0; i < array.Length; i++)
		{
			Vector2 vector = array[i];
			CreateDxTextChild(gameObject.transform, "DX_Outline", color, new Vector3(vector.x, vector.y, 0f), builtinResource, sortingLayerID, sortingOrder + 21);
		}
		CreateDxTextChild(gameObject.transform, "DX_Main", color2, Vector3.zero, builtinResource, sortingLayerID, sortingOrder + 22);
		return gameObject;
	}

	private static void CreateDxTextChild(Transform parent, string name, Color color, Vector3 localPosition, Font font, int sortingLayerId, int sortingOrder)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.transform.localPosition = localPosition;
		TextMesh textMesh = gameObject.AddComponent<TextMesh>();
		textMesh.text = "DX";
		textMesh.anchor = TextAnchor.MiddleCenter;
		textMesh.alignment = TextAlignment.Center;
		textMesh.fontSize = 72;
		textMesh.characterSize = 0.072f;
		textMesh.fontStyle = FontStyle.Bold;
		textMesh.color = color;
		textMesh.richText = false;
		if (font != null)
		{
			textMesh.font = font;
			MeshRenderer component = gameObject.GetComponent<MeshRenderer>();
			if (component != null)
			{
				component.sharedMaterial = font.material;
				component.sortingLayerID = sortingLayerId;
				component.sortingOrder = sortingOrder;
			}
		}
	}

	private static void DestroyDxMarkerObjects()
	{
		foreach (GameObject value in dxMarkerObjects.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value);
			}
		}
		dxMarkerObjects.Clear();
	}

	private static void ToggleMarkerOnSelectedFloor(scnEditor editor)
	{
		if (settings == null || !settings.enableDXNotes)
		{
			return;
		}
		List<scrFloor> list = (from f in editor.selectedFloors?.Where((scrFloor f) => (UnityEngine.Object)(object)f != null)
			group f by f.seqID into g
			select g.First()).ToList() ?? new List<scrFloor>();
		if (list.Count == 0)
		{
			ShowNotice(L("DX 标记：请先选择至少一个音符", "DX: select at least one note"));
			return;
		}
		Dictionary<scrFloor, List<LevelEvent>> markersByFloor = list.ToDictionary((scrFloor f) => f, (scrFloor f) => GetMarkersOnFloor(f.seqID));
		if (list.All((scrFloor f) => markersByFloor[f].Count > 0))
		{
			int num = 0;
			foreach (KeyValuePair<scrFloor, List<LevelEvent>> item in markersByFloor)
			{
				foreach (LevelEvent item2 in item.Value)
				{
					editor.RemoveEvent(item2, skipDecorationUpdate: true);
					num++;
				}
			}
			editor.ApplyEventsToFloors();
			editor.DecideInspectorTabsAtSelected();
			scrFloor scrFloor = list.FirstOrDefault();
			if ((UnityEngine.Object)(object)scrFloor != null)
			{
				editor.ShowEventIndicators(scrFloor);
			}
			RefreshMarkerCache();
			ShowNotice(L("已移除 " + list.Count + " 个音符的 DX 标记", "Removed DX markers from " + list.Count + " note(s)"));
			modEntry.Logger.Log("Break markers removed from " + list.Count + " floor(s), " + num + " event(s).");
			return;
		}
		List<scrFloor> list2 = list.Where((scrFloor f) => f.seqID > 0 && markersByFloor[f].Count == 0).ToList();
		bool flag = list.Any((scrFloor f) => f.seqID <= 0);
		if (list2.Count == 0)
		{
			ShowNotice(flag ? L("DX 添加失败：初始音符不能添加 DX 标记", "DX cannot be added to the initial floor") : L("这些音符已经有 DX 标记", "These notes already have DX markers"));
			return;
		}
		foreach (scrFloor item3 in list2)
		{
			LevelEvent levelEvent = new LevelEvent(item3.seqID, LevelEventType.EditorComment);
			ConfigureMarker(levelEvent);
			editor.events.Add(levelEvent);
		}
		editor.ApplyEventsToFloors();
		editor.DecideInspectorTabsAtSelected();
		editor.ShowEventIndicators(list2[0]);
		RefreshMarkerCache();
		string text = L("已为 " + list2.Count + " 个音符添加 DX 标记", "Added DX markers to " + list2.Count + " note(s)");
		if (flag)
		{
			text += L("（已跳过初始音符）", " (initial floor skipped)");
		}
		ShowNotice(text);
		modEntry.Logger.Log("Break markers added to " + list2.Count + " floor(s).");
	}

	private static void ConfigureMarker(LevelEvent marker)
	{
		marker["comment"] = "ADOFAIDX.DX";
	}

	private static void MigrateLegacyFlashMarkers(scnEditor editor)
	{
		if ((UnityEngine.Object)(object)editor == null || editor.playMode || editor.events == null)
		{
			return;
		}
		scnGame instance = scnGame.instance;
		LevelData levelData = (((UnityEngine.Object)(object)instance == null) ? null : instance.levelData);
		if (levelData != null && legacyMigrationCheckedLevel == levelData)
		{
			return;
		}
		List<LevelEvent> list = editor.events.Where(IsLegacyFlashMarker).ToList();
		if (levelData != null)
		{
			legacyMigrationCheckedLevel = levelData;
		}
		if (list.Count == 0)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (LevelEvent item in list)
		{
			if (item.floor > 0)
			{
				hashSet.Add(item.floor);
			}
			editor.RemoveEvent(item, skipDecorationUpdate: true);
		}
		foreach (int floorId in hashSet)
		{
			if (!editor.events.Any((LevelEvent levelEvent2) => levelEvent2 != null && levelEvent2.active && levelEvent2.floor == floorId && levelEvent2.eventType == LevelEventType.EditorComment && IsMarker(levelEvent2)))
			{
				LevelEvent levelEvent = new LevelEvent(floorId, LevelEventType.EditorComment);
				ConfigureMarker(levelEvent);
				editor.events.Add(levelEvent);
			}
		}
		editor.ApplyEventsToFloors();
		RefreshMarkerCache();
		ShowNotice(L("旧版闪光 DX 标记已转换，请保存谱面", "Legacy flash DX markers converted. Save the level."));
		modEntry.Logger.Log("Migrated " + list.Count + " legacy flash marker event(s) on " + hashSet.Count + " floor(s).");
	}

	private static List<LevelEvent> GetMarkersOnFloor(int floorId)
	{
		scnEditor instance = scnEditor.instance;
		if ((UnityEngine.Object)(object)instance == null)
		{
			return new List<LevelEvent>();
		}
		return instance.GetFloorEvents(floorId, LevelEventType.EditorComment).Concat(instance.GetFloorEvents(floorId, LevelEventType.Flash)).Where(IsMarker)
			.ToList();
	}

	private static bool IsMarker(LevelEvent levelEvent)
	{
		if (levelEvent == null || !levelEvent.active)
		{
			return false;
		}
		if (levelEvent.eventType == LevelEventType.EditorComment)
		{
			return string.Equals(levelEvent["comment"] as string, "ADOFAIDX.DX", StringComparison.Ordinal);
		}
		if (levelEvent.eventType != LevelEventType.Flash)
		{
			return false;
		}
		return IsLegacyFlashMarker(levelEvent);
	}

	private static bool IsLegacyFlashMarker(LevelEvent levelEvent)
	{
		if (levelEvent == null || !levelEvent.active || levelEvent.eventType != LevelEventType.Flash)
		{
			return false;
		}
		string a = levelEvent["eventTag"] as string;
		if (!string.Equals(a, "ADOFAIDX.DX.v2", StringComparison.Ordinal) && !string.Equals(a, "ADOFAIDX.Break.v1", StringComparison.Ordinal))
		{
			return string.Equals(a, "CPerfect.CP.v1", StringComparison.Ordinal);
		}
		return true;
	}

	private static void RefreshMarkerCache()
	{
		scnGame instance = scnGame.instance;
		LevelData levelData = (((UnityEngine.Object)(object)instance == null) ? null : instance.levelData);
		IEnumerable<LevelEvent> enumerable = null;
		if (levelData != null && levelData.levelEvents != null)
		{
			enumerable = levelData.levelEvents;
		}
		else
		{
			scnEditor instance2 = scnEditor.instance;
			if ((UnityEngine.Object)(object)instance2 != null && instance2.events != null)
			{
				enumerable = instance2.events;
			}
		}
		if (enumerable == null)
		{
			return;
		}
		if (trackedLevelData != levelData)
		{
			trackedLevelData = levelData;
			breakAttempts.Clear();
			lastReportedMarkerCount = -1;
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (LevelEvent item in enumerable)
		{
			if (IsMarker(item) && item.floor > 0)
			{
				hashSet.Add(item.floor);
			}
		}
		markedFloors.Clear();
		markedFloors.UnionWith(hashSet);
		if (lastReportedMarkerCount != markedFloors.Count)
		{
			lastReportedMarkerCount = markedFloors.Count;
			modEntry.Logger.Log("DX markers in current level: " + markedFloors.Count + ".");
		}
	}

	internal static bool IsBreakFloor(int floorId)
	{
		if (settings == null || !settings.enableDXNotes || floorId <= 0)
		{
			return false;
		}
		scnGame instance = scnGame.instance;
		if ((UnityEngine.Object)(object)instance == null || instance.levelData == null || instance.levelData.levelEvents == null)
		{
			return false;
		}
		foreach (LevelEvent levelEvent in instance.levelData.levelEvents)
		{
			if (levelEvent.floor == floorId && IsMarker(levelEvent))
			{
				return true;
			}
		}
		return false;
	}

	internal static void RecordBreakHit(scrFloor floor, bool perfect, HitMargin grade, string noteType)
	{
		if (settings == null || !settings.enableDXNotes || (UnityEngine.Object)(object)floor == null)
		{
			return;
		}
		scrMarginTracker playerZeroTracker = GetPlayerZeroTracker();
		if (playerZeroTracker == null || playerZeroTracker.hitMargins == null)
		{
			LogBreakSwitchMessage("Break floor " + floor.seqID + " not counted: hit tracker unavailable.");
			return;
		}
		int num = playerZeroTracker.hitMargins.Count - 1;
		if (num < 0)
		{
			LogBreakSwitchMessage("Break floor " + floor.seqID + " not counted: hit index unavailable.");
			return;
		}
		breakAttempts.RemoveAll((BreakAttempt attempt) => attempt.FloorId == floor.seqID);
		breakAttempts.Add(new BreakAttempt
		{
			FloorId = floor.seqID,
			HitIndex = num,
			Perfect = perfect,
			Grade = grade.ToString(),
			NoteType = noteType
		});
		LogBreakSwitchMessage("DX " + (perfect ? "Break" : "Miss") + " floor " + floor.seqID + " [" + noteType + "] hitIndex=" + num + " grade=" + grade.ToString() + ".");
		TriggerBreakFeedback(floor, perfect);
	}

	internal static bool IsAutomaticBreakTransition(scrFloor target, scrPlayer player)
	{
		if ((UnityEngine.Object)(object)target == null)
		{
			return false;
		}
		if ((!((UnityEngine.Object)(object)player != null) || (!player.auto && !player.holding)) && !target.auto)
		{
			return target.freeroamGenerated;
		}
		return true;
	}

	internal static string DescribeBreakNoteType(scrFloor source, scrFloor target, scrPlayer player)
	{
		if ((UnityEngine.Object)(object)target == null)
		{
			return "Unknown";
		}
		List<string> list = new List<string>();
		if (target.midSpin)
		{
			list.Add("Midspin");
		}
		if ((UnityEngine.Object)(object)source != null && source.midSpin)
		{
			list.Add("AfterMidspin");
		}
		if (target.holdLength >= 0)
		{
			list.Add("Hold");
		}
		if ((UnityEngine.Object)(object)source != null && source.holdLength >= 0)
		{
			list.Add("HoldRelease");
		}
		if ((UnityEngine.Object)(object)player != null && player.holding)
		{
			list.Add("HoldChain");
		}
		if (target.tapsNeeded > 1)
		{
			list.Add("Multitap x" + target.tapsNeeded);
		}
		if (target.auto)
		{
			list.Add("Auto");
		}
		if (target.freeroam)
		{
			list.Add("Freeroam");
		}
		if (target.freeroamGenerated)
		{
			list.Add("FreeroamGrid");
		}
		if (target.isSwirl)
		{
			list.Add("Swirl");
		}
		if (target.numPlanets > 2)
		{
			list.Add("Multiplanet x" + target.numPlanets);
		}
		if (target.isportal)
		{
			list.Add("Portal");
		}
		if (list.Count != 0)
		{
			return string.Join("+", list.ToArray());
		}
		return "Normal";
	}

	internal static void LogBreakSwitchMessage(string message)
	{
		modEntry.Logger.Log(message);
	}

	private static void PruneBreakAttempts()
	{
		if (breakAttempts.Count == 0)
		{
			return;
		}
		scrMarginTracker playerZeroTracker = GetPlayerZeroTracker();
		if (playerZeroTracker == null)
		{
			breakAttempts.Clear();
			return;
		}
		int hitCount = playerZeroTracker.hitMargins?.Count ?? 0;
		breakAttempts.RemoveAll((BreakAttempt a) => a.HitIndex >= hitCount);
	}

	private static scrMarginTracker GetPlayerZeroTracker()
	{
		scrMarginTracker[] marginTrackers = scrMistakesManager.marginTrackers;
		if (marginTrackers == null || marginTrackers.Length == 0)
		{
			return null;
		}
		return marginTrackers[0];
	}

	private static void GetBreakSummary(out int perfectCount, out int completedCount)
	{
		PruneBreakAttempts();
		Dictionary<int, BreakAttempt> dictionary = new Dictionary<int, BreakAttempt>();
		foreach (BreakAttempt breakAttempt in breakAttempts)
		{
			if (!dictionary.TryGetValue(breakAttempt.FloorId, out var value) || breakAttempt.HitIndex > value.HitIndex)
			{
				dictionary[breakAttempt.FloorId] = breakAttempt;
			}
		}
		completedCount = dictionary.Count;
		perfectCount = dictionary.Values.Count((BreakAttempt a) => a.Perfect);
	}

	private static void EnsureVisualTextures()
	{
		if (roundedPanelTexture == null)
		{
			roundedPanelTexture = CreateRoundedPanelTexture(64, 64, 14f);
		}
		if (softGlowTexture == null)
		{
			softGlowTexture = CreateGlowTexture(96, ring: false);
		}
		if (ringGlowTexture == null)
		{
			ringGlowTexture = CreateGlowTexture(96, ring: true);
		}
	}

	private static Texture2D CreateGlowTexture(int size, bool ring)
	{
		Texture2D texture2D = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		Color32[] array = new Color32[size * size];
		Vector2 b = new Vector2((float)(size - 1) * 0.5f, (float)(size - 1) * 0.5f);
		float num = (float)size * 0.48f;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num2 = Vector2.Distance(new Vector2(j, i), b) / num;
				float num4;
				if (ring)
				{
					float num3 = Mathf.Abs(num2 - 0.78f);
					num4 = Mathf.Clamp01(1f - num3 / 0.16f) * Mathf.Clamp01(1f - num2);
				}
				else
				{
					num4 = Mathf.Clamp01(1f - num2) * Mathf.Clamp01(1f - num2);
				}
				array[i * size + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(num4 * 255f));
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply();
		texture2D.wrapMode = TextureWrapMode.Clamp;
		return texture2D;
	}

	private static Texture2D CreateRoundedPanelTexture(int width, int height, float radius)
	{
		Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
		Color32[] array = new Color32[width * height];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				float num = Mathf.Max(radius - (float)j, (float)j - ((float)(width - 1) - radius), 0f);
				float num2 = Mathf.Max(radius - (float)i, (float)i - ((float)(height - 1) - radius), 0f);
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				float num4 = 1f - Mathf.Clamp01((num3 - radius + 1f) / 2f);
				array[i * width + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(num4 * 255f));
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply();
		texture2D.wrapMode = TextureWrapMode.Clamp;
		return texture2D;
	}

	private static void DrawGameplayInfo()
	{
		if (settings != null && settings.enableDXNotes && settings.showCAcuracy && markedFloors.Count != 0)
		{
			GetBreakSummary(out var perfectCount, out var _);
			string text = ((float)perfectCount * 1f / (float)markedFloors.Count).ToString("0.0000") + "%";
			string text2 = ((settings.cAccuracyLayout == 1) ? (text + " | DX-Accuracy") : ("DX-Accuracy | " + text));
			EnsureVisualTextures();
			float num = Mathf.Clamp(settings.cAccuracyScale, 0.5f, 3f);
			cAccuracyStyle.fontSize = Mathf.RoundToInt(20f * num);
			Vector2 vector = cAccuracyStyle.CalcSize(new GUIContent(text2));
			vector.x += 28f * num;
			vector.y = Mathf.Max(vector.y + 12f * num, 34f * num);
			float x = Mathf.Clamp(settings.cAccuracyX, 0f, Mathf.Max(0f, (float)Screen.width - vector.x));
			float y = Mathf.Clamp(settings.cAccuracyY, 0f, Mathf.Max(0f, (float)Screen.height - vector.y));
			Rect position = new Rect(x, y, vector.x, vector.y);
			Rect position2 = new Rect(position.x + 3f * num, position.y + 3f * num, position.width, position.height);
			GUI.color = new Color(0f, 0f, 0f, 0.38f);
			GUI.DrawTexture(position2, roundedPanelTexture, ScaleMode.StretchToFill, alphaBlend: true);
			GUI.color = new Color(0.035f, 0.045f, 0.065f, 0.88f);
			GUI.DrawTexture(position, roundedPanelTexture, ScaleMode.StretchToFill, alphaBlend: true);
			Color trackPrimaryColor = GetTrackPrimaryColor();
			trackPrimaryColor.a = 0.95f;
			GUI.color = trackPrimaryColor;
			GUI.DrawTexture(new Rect((settings.cAccuracyLayout == 1) ? (position.xMax - 8f * num) : (position.x + 4f * num), position.y + 5f * num, 4f * num, position.height - 10f * num), Texture2D.whiteTexture);
			GUI.color = new Color(0f, 0f, 0f, 0.8f);
			GUI.Label(new Rect(position.x + 1.5f * num, position.y + 1.5f * num, position.width, position.height), text2, cAccuracyStyle);
			GUI.color = Color.white;
			GUI.Label(position, text2, cAccuracyStyle);
		}
	}

	private static Color GetTrackPrimaryColor()
	{
		scrFloor scrFloor = null;
		scrController instance = scrController.instance;
		if ((UnityEngine.Object)(object)instance != null)
		{
			scrFloor = instance.currFloor;
		}
		if ((UnityEngine.Object)(object)scrFloor == null)
		{
			scrLevelMaker instance2 = scrLevelMaker.instance;
			if ((UnityEngine.Object)(object)instance2 != null && instance2.listFloors != null)
			{
				scrFloor = instance2.listFloors.FirstOrDefault((scrFloor candidate) => (UnityEngine.Object)(object)candidate != null && (UnityEngine.Object)(object)candidate.floorRenderer != null && markedFloors.Contains(candidate.seqID));
			}
		}
		Color result = (((UnityEngine.Object)(object)scrFloor != null && (UnityEngine.Object)(object)scrFloor.floorRenderer != null) ? scrFloor.floorRenderer.color : new Color(1f, 0.58f, 0.1f));
		result.a = 0.95f;
		return result;
	}

	internal static float GetBreakXAccuracyBonus(scrMarginTracker tracker)
	{
		if (settings == null || !settings.enableDXNotes || !settings.mergeDXAccuracy || tracker == null || markedFloors.Count == 0)
		{
			return 0f;
		}
		scrMarginTracker[] marginTrackers = scrMistakesManager.marginTrackers;
		if (marginTrackers == null || marginTrackers.Length == 0 || marginTrackers[0] != tracker)
		{
			return 0f;
		}
		GetBreakSummary(out var perfectCount, out var _);
		if (perfectCount <= 0)
		{
			return 0f;
		}
		return CalculateBreakXAccuracyBonus(perfectCount);
	}

	private static float CalculateBreakXAccuracyBonus(int perfectCount)
	{
		if (perfectCount <= 0 || markedFloors.Count <= 0)
		{
			return 0f;
		}
		float bonus = Mathf.Clamp01((float)perfectCount / (float)markedFloors.Count) * BreakMaxXAccuracyBonus;
		if (perfectCount >= markedFloors.Count)
		{
			bonus += BreakAccuracyDisplayEpsilon;
		}
		return Mathf.Min(BreakMaxXAccuracyBonus + BreakAccuracyDisplayEpsilon, bonus);
	}

	private static void GetResultBreakStatistics(scrMarginTracker tracker, out int perfectCount, out int totalCount)
	{
		if (markedFloors.Count == 0)
		{
			RefreshMarkerCache();
		}
		totalCount = markedFloors.Count;
		if (markedFloors.Count == 0)
		{
			perfectCount = 0;
			return;
		}
		Dictionary<int, BreakAttempt> dictionary = new Dictionary<int, BreakAttempt>();
		foreach (BreakAttempt breakAttempt in breakAttempts)
		{
			if (!dictionary.TryGetValue(breakAttempt.FloorId, out var value) || breakAttempt.HitIndex > value.HitIndex)
			{
				dictionary[breakAttempt.FloorId] = breakAttempt;
			}
		}
		perfectCount = dictionary.Values.Count((BreakAttempt a) => a.Perfect);
		modEntry.Logger.Log("Detailed result DX: " + perfectCount + "/" + totalCount + " = " + ((decimal)perfectCount * 100m / (decimal)totalCount).ToString("0.00", CultureInfo.InvariantCulture) + "%, merge=" + settings.mergeDXAccuracy + " (tracker=" + ((tracker == null) ? "null" : "ok") + ").");
	}

	internal static string AddBreakBonusToResultText(string resultText, scrMarginTracker tracker)
	{
		if (settings == null || !settings.enableDXNotes || string.IsNullOrEmpty(resultText) || !Persistence.showXAccuracy)
		{
			return resultText;
		}
		string text = RDString.Get("xAccuracy");
		if (markedFloors.Count == 0)
		{
			RefreshMarkerCache();
		}
		if (markedFloors.Count == 0)
		{
			return resultText;
		}
		string text2 = (string.IsNullOrEmpty(text) ? "(?:X-精准度|X-Accuracy)" : Regex.Escape(text));
		Match match = Regex.Match(resultText, text2 + "[^\\r\\n]*?\\d+\\.\\d{2}");
		int startIndex = (match.Success ? (match.Index + match.Length) : 0);
		int num = resultText.IndexOf('%', startIndex);
		if (num < 0)
		{
			return resultText;
		}
		int num2 = resultText.IndexOf('\n', startIndex);
		if (match.Success && num2 >= 0 && num2 < num)
		{
			return resultText;
		}
		GetResultBreakStatistics(tracker, out var perfectCount, out var _);
		float resultBreakBonus = CalculateBreakXAccuracyBonus(perfectCount);
		if (settings.mergeDXAccuracy)
		{
			string value = "(+" + (resultBreakBonus * 100f).ToString("0.00", CultureInfo.InvariantCulture) + "%)";
			return resultText.Insert(num + 1, value);
		}
		decimal baseXAccuracyPercent = ((tracker == null) ? 0m : ((decimal)tracker.percentXAcc * 100m));
		decimal dxAccuracyPercent = Math.Min(101m, baseXAccuracyPercent + (decimal)resultBreakBonus * 100m);
		string value2 = Localized("DX-精准度: ", "DX-Accuracy: ") + dxAccuracyPercent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
		if (Math.Round(dxAccuracyPercent, 2, MidpointRounding.AwayFromZero) >= 101m)
		{
			value2 = "<color=#FFDA00>" + value2 + "</color>";
		}
		resultText = ((num2 >= 0) ? resultText.Insert(num2 + 1, value2 + "\n") : (resultText + "\n" + value2));
		return resultText;
	}

	private static void DrawEditorInfo()
	{
		float num = Mathf.Clamp((float)Screen.width / 1920f, 0.75f, 1.5f);
		infoStyle.fontSize = Mathf.RoundToInt(16f * num);
		string text = "ADOFAI DX  |  " + L("DX 音符: ", "DX notes: ") + markedFloors.Count + L("  |  选中音符后按 " + GetShortcutDisplay() + " 添加或移除", "  |  Select notes and press " + GetShortcutDisplay());
		Rect position = new Rect(18f, (float)Screen.height - 44f, Mathf.Min((float)Screen.width - 36f, 720f * num), 30f * num);
		GUI.color = new Color(0f, 0f, 0f, 0.72f);
		GUI.DrawTexture(position, Texture2D.whiteTexture);
		GUI.color = Color.white;
		GUI.Label(position, "  " + text, infoStyle);
	}

	private static void DrawEditorControl(scnEditor editor)
	{
		float num = Mathf.Clamp((float)Screen.width / 1920f, 0.75f, 1.5f);
		Rect position = new Rect((float)Screen.width - 70f * num, (float)Screen.height * 0.5f - 24f * num, 50f * num, 48f * num);
		Color backgroundColor = GUI.backgroundColor;
		GUI.backgroundColor = new Color(1f, 0.58f, 0.1f, 1f);
		if (GUI.Button(position, "DX", badgeStyle))
		{
			ToggleMarkerOnSelectedFloor(editor);
		}
		GUI.backgroundColor = backgroundColor;
	}

	private static void EnsureStyles()
	{
		if (badgeStyle == null || infoStyle == null || noticeStyle == null || breakFeedbackStyle == null || cAccuracyStyle == null)
		{
			badgeStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Bold,
				normal = 
				{
					textColor = Color.white
				},
				clipping = TextClipping.Clip
			};
			infoStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleLeft,
				fontStyle = FontStyle.Bold,
				normal = 
				{
					textColor = Color.white
				},
				clipping = TextClipping.Clip
			};
			noticeStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Bold,
				normal = 
				{
					textColor = Color.white
				},
				wordWrap = true,
				clipping = TextClipping.Clip
			};
			breakFeedbackStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Bold,
				normal = 
				{
					textColor = Color.white
				},
				clipping = TextClipping.Clip
			};
			cAccuracyStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Bold,
				normal = 
				{
					textColor = Color.white
				},
				clipping = TextClipping.Clip
			};
		}
	}
}
internal sealed class AdofaiDxOverlay : MonoBehaviour
{
	private void Update()
	{
		AdofaiDxMod.UpdateFromOverlay();
	}

	private void OnGUI()
	{
		AdofaiDxMod.DrawOverlay();
	}

	internal void LoadCustomAudio(string path, bool perfect)
	{
		StartCoroutine(LoadCustomAudioRoutine(path, perfect));
	}

	private IEnumerator LoadCustomAudioRoutine(string path, bool perfect)
	{
		CoroutineWithData loader;
		try
		{
			AudioManager instance = AudioManager.Instance;
			if ((UnityEngine.Object)(object)instance == null)
			{
				throw new InvalidOperationException("AudioManager is unavailable");
			}
			IEnumerator target = instance.FindOrLoadAudioClipExternal(path, mp3Streaming: false, 0f, stream: false);
			loader = new CoroutineWithData(this, target);
		}
		catch (Exception ex)
		{
			AdofaiDxMod.SetCustomAudioClip(perfect, null, "自定义音效加载失败: " + ex.Message);
			yield break;
		}
		float loadStarted = Time.unscaledTime;
		yield return loader.coroutine;
		if (Time.unscaledTime - loadStarted > 30f)
		{
			AdofaiDxMod.SetCustomAudioClip(perfect, null, AdofaiDxMod.Localized("自定义音效加载超过 30 秒，已取消", "Custom audio load exceeded 30 seconds"));
			yield break;
		}
		object result = loader.result;
		object obj = result?.GetType().GetField("clip")?.GetValue(result);
		if (obj != null)
		{
			object obj2 = obj.GetType().GetProperty("length")?.GetValue(obj, null);
			if (((obj2 == null) ? 0f : Convert.ToSingle(obj2)) > 30f)
			{
				if (obj is UnityEngine.Object obj3)
				{
					UnityEngine.Object.Destroy(obj3);
				}
				obj = null;
			}
		}
		AdofaiDxMod.SetCustomAudioClip(perfect, obj, (obj != null) ? AdofaiDxMod.Localized("自定义音效已加载: " + path, "Custom audio loaded: " + path) : AdofaiDxMod.Localized("自定义音效加载失败或超过 30 秒: " + path, "Custom audio failed or exceeds 30 seconds: " + path));
	}
}
[HarmonyPatch(typeof(scrPlanet), "MoveToNextFloor")]
internal static class BreakJudgementPatch
{
	private static void Prefix(scrPlanet __instance, scrFloor __0, HitMargin __2)
	{
		if ((UnityEngine.Object)(object)__instance == null || (UnityEngine.Object)(object)__instance.currfloor == null || (UnityEngine.Object)(object)__0 == null || (UnityEngine.Object)(object)__instance.player == null)
		{
			return;
		}
		scrFloor source = __instance.currfloor;
		scrFloor target = __0;
		if (!AdofaiDxMod.IsBreakFloor(target.seqID))
		{
			return;
		}
		string text = AdofaiDxMod.DescribeBreakNoteType(source, target, __instance.player);
		if (AdofaiDxMod.IsAutomaticBreakTransition(target, __instance.player))
		{
			AdofaiDxMod.RecordBreakHit(target, perfect: true, HitMargin.Perfect, text + "+Automatic");
			return;
		}
		scrConductor instance = scrConductor.instance;
		if ((UnityEngine.Object)(object)instance == null)
		{
			return;
		}
		object value = ((object)instance).GetType().GetField("song").GetValue(instance);
		if (value != null)
		{
			System.Reflection.PropertyInfo property = value.GetType().GetProperty("pitch");
			if (!(property == null))
			{
				double num = scrMisc.GetAdjustedAngleBoundaryInDeg(HitMarginGeneral.Perfect, (float)((double)instance.bpm * __instance.planetarySystem.speed), Convert.ToSingle(property.GetValue(value, null)), target.marginScale) * 0.3333333432674408;
				double num2 = (__instance.cachedAngle - __instance.targetExitAngle) * (__instance.planetarySystem.isCW ? 1.0 : (-1.0)) * 57.295780181884766;
				bool flag = Math.Abs(num2) <= num;
				HitMargin grade;
				if (flag)
				{
					double num3 = num / 3.0;
					grade = ((Math.Abs(num2) <= num3) ? HitMargin.Perfect : ((num2 < 0.0) ? HitMargin.EarlyPerfect : HitMargin.LatePerfect));
				}
				else
				{
					grade = ((num2 < 0.0) ? HitMargin.VeryEarly : HitMargin.VeryLate);
				}
				AdofaiDxMod.RecordBreakHit(target, flag, grade, text);
			}
		}
	}
}
public sealed class AdofaiDxSettings : UnityModManager.ModSettings
{
	public float perfectSoundVolume = 0.8f;

	public float failedSoundVolume = 0.6f;

	public float soundVolume = 0.8f;

	public bool volumeMerged;

	public float effectOpacity = 0.85f;

	public float effectScale = 1f;

	public string perfectEffectColor = "#33FFA6";

	public string failedEffectColor = "#FF4438";

	public string perfectEffectText = "BREAK!";

	public string failedEffectText = "BREAK MISS";

	public bool sectionCustomImage;

	public bool useCustomPerfectImage;

	public string customPerfectImagePath = "";

	public bool useCustomFailedImage;

	public string customFailedImagePath = "";

	public int soundStyle;

	public int soundStyleVersion;

	public int perfectBuiltinSound;

	public int failedBuiltinSound;

	public int mergedSoundPresetVersion;

	public int effectStyle;

	public int effectStyleVersion;

	public int language;

	public int languageVersion;

	public bool enableDXNotes = true;

	public bool sectionSound = true;

	public bool sectionEffect = true;

	public bool sectionMarker = true;

	public bool showGameplayDXMarker = true;

	public bool gameplayDXFloat = true;

	public float gameplayDXFloatAmplitude = 1f;

	public bool gameplayDXColorAdaptive;

	public float dxColorVariation = 1f;

	public int dxNoteColorR = 255;

	public int dxNoteColorG = 107;

	public int dxNoteColorB = 13;

	public bool gameplayDXGlow = true;

	public float gameplayDXGlowIntensity = 0.8f;

	public bool sectionCAcuracy = true;

	public bool mergeDXAccuracy = true;

	public bool sectionCustomAudio;

	public bool sectionAudioPresets;

	public bool useCustomPerfectSound;

	public string customPerfectSoundPath = "";

	public bool useCustomFailedSound;

	public string customFailedSoundPath = "";

	public bool sectionShortcut = true;

	public int hotkeyKeyCode = 99;

	public bool hotkeyCtrl = true;

	public bool hotkeyShift = true;

	public bool hotkeyAlt;

	public bool showCAcuracy = true;

	public float cAccuracyX = 1800f;

	public float cAccuracyY = 950f;

	public float cAccuracyScale = 1f;

	public int cAccuracyLayout;

	public int cAccuracyReferenceWidth = 1920;

	public int cAccuracyReferenceHeight = 1080;

	public bool cAccuracyPositionMigrated;

	public override void Save(UnityModManager.ModEntry modEntry)
	{
		UnityModManager.ModSettings.Save(this, modEntry);
	}
}
[HarmonyPatch(typeof(scrController), "StartLoadingScene")]
internal static class DXScreenTransitionStartPatch
{
	private static void Prefix()
	{
		AdofaiDxMod.BeginScreenTransition();
	}
}
[HarmonyPatch(typeof(scrLoader), "WipeFromBlack")]
internal static class DXScreenTransitionEndPatch
{
	private static void Prefix()
	{
		AdofaiDxMod.EndScreenTransition();
	}
}
[HarmonyPatch(typeof(scrMarginTracker), "get_percentXAcc")]
internal static class BreakXAccuracyPatch
{
	private static void Postfix(scrMarginTracker __instance, ref float __result)
	{
		__result += AdofaiDxMod.GetBreakXAccuracyBonus(__instance);
	}
}
[HarmonyPatch(typeof(DetailedResults), "GenerateResults")]
internal static class BreakXAccuracyTextPatch
{
	private static void Postfix(scrMarginTracker __0, ref string __result)
	{
		__result = AdofaiDxMod.AddBreakBonusToResultText(__result, __0);
	}
}
}
