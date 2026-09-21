using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PortfolioBuilder
{
    static TMP_FontAsset font;
    static readonly Color Ink = Hex("163C3A"), Muted = Hex("617771"), Background = Hex("F2F5EF"), Accent = Hex("167B65");
    static PortfolioConfig config;
    public static void Build()
    {
        config = PortfolioConfig.Load();
        Directory.CreateDirectory("Assets/Game/Generated");
        PlayerSettings.companyName = "Ufuk Bayhan";
        PlayerSettings.productName = config.title;
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.ufukbayhan." + config.repo.Replace("-", ""));
        CreateFont();
        var correct = Feedback("Correct", "Doğru!", Accent, false, false);
        var wrong = Feedback("Wrong", "Tekrar dene", Hex("B64D43"), true, false);
        var final = Feedback("Final", "Harika! Yeni seviye", Accent, false, true);
        Menu();
        foreach (var mode in config.modes) Game(mode, correct, wrong, final);
        EditorBuildSettings.scenes = new[] { "ModeSelect" }.Concat(config.modes.Select(m=>m.scene)).Select(n=>new EditorBuildSettingsScene("Assets/Game/Scenes/"+n+".unity",true)).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ModeSelect.unity");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(), locationPathName="Builds/Windows/Game.exe", target=BuildTarget.StandaloneWindows64, options=BuildOptions.Development });
        if(report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed");
        Debug.Log("PORTFOLIO_BUILD_OK");
    }
    static string Help()
    {
#if SEQUENCE
        return "Kutulara sayıları yaz. Enter ile cevabını kontrol et.";
#elif CARDS
        return "İki kart aç. Birbirini tamamlayan çiftleri bul.";
#else
        return "Yönergeyi takip et. Doğru balonlara tıkla.";
#endif
    }
    static void Menu()
    {
        var root=SceneCanvas();
        Text(root,"Eyebrow","MATEMATİK ATÖLYESİ",72,48,1200,30,18,Muted,true);
        Text(root,"Title",config.turkish,68,98,1300,100,66,Ink,true);
        Text(root,"Intro",Help(),72,214,1296,48,26,Muted);
        int cols=config.modes.Length<=3?3:4;
        float w=(1296-24*(cols-1))/cols;
        for(int i=0;i<config.modes.Length;i++)
        {
            float x=72+(i%cols)*(w+24), y=320+(i/cols)*210;
            var button=Button(root,"Mode_"+config.modes[i].scene,"",x,y,w,180,Color.white,Ink,28);
            Panel(button.transform,"Accent",0,0,5,180,Accent);
            Text(button.transform,"Index",(i+1).ToString("00"),24,14,w-48,60,40,Accent,true);
            Text(button.transform,"Name",config.modes[i].title,24,88,w-48,72,27,Ink,true);
            var nav=button.gameObject.AddComponent<ModeNavigation>();nav.targetScene=config.modes[i].scene;
            UnityEventTools.AddPersistentListener(button.onClick,nav.Open);
        }
        Text(root,"Footer",config.modes.Length+" OYUN MODU  /  BİR MOD SEÇ VE BAŞLA",72,796,1296,40,20,Muted,true);
        Save("ModeSelect");
    }
    static void Game(PortfolioMode mode,GameObject correct,GameObject wrong,GameObject final)
    {
        var root=SceneCanvas();
        var go=new GameObject("StemGameManager");var stem=go.AddComponent<StemGameManager>();
        stem.stemGameType=(StemGameType)mode.stemGameType; stem.step=mode.step;
        stem.isRandom=mode.isRandom!=0;stem.isDynamicMode=mode.isDynamicMode!=0;stem.isReverseMode=mode.isReverseMode!=0;stem.isEvent=false;
        stem.numberRangeXY=new Vector2Int(mode.x,mode.y);stem.answerBoxesString=mode.operators;
        stem.WinSFX=correct;stem.WrongSFX=wrong;stem.WinSFXFinal=final;
        var back=Button(root,"Modes","<  Modlar",72,44,166,48,Color.white,Ink,22);UnityEventTools.AddPersistentListener(back.onClick,stem.LoadBackScene);
        Text(root,"Heading",mode.title,270,38,850,62,38,Ink,true);
        var level=Text(root,"Level","SEVİYE 01",1180,44,220,48,23,Accent,true);
        Panel(root,"Rule",72,116,1296,2,Hex("DCE5DB"));
        stem.yonergeText=Text(root,"Instruction","",72,140,1296,106,34,Ink,true,TextAlignmentOptions.Center);
        var board=Rect(root,"Board",72,272,1296,474);
#if SEQUENCE
        var layout=board.gameObject.AddComponent<HorizontalLayoutGroup>();layout.childAlignment=TextAnchor.MiddleCenter;layout.spacing=12;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        stem.trueObjects=new[]{board.gameObject};stem.etcPrefab=new[]{NumberTile(),InputTile()};
        Text(root,"Tip", "Sayı yaz → Enter ile onayla",72,755,1296,34,21,Muted,false,TextAlignmentOptions.Center);
#elif CARDS
        stem.cardMatchGameTargetType=(CardMatchGameType)mode.cardMatchGameTargetType;
        var grid=board.gameObject.AddComponent<GridLayoutGroup>();grid.childAlignment=TextAnchor.MiddleCenter;
        stem.trueObjects=new[]{board.gameObject};stem.etcPrefab=new[]{Card()};
#else
        // Centered coordinates match the original moving-bubble algorithms.
        board.pivot=new Vector2(.5f,.5f);board.anchoredPosition=new Vector2(720,-505);
        board.gameObject.AddComponent<RectMask2D>();
        stem.trueObjects=new[]{board.gameObject};stem.etcPrefab=new[]{Bubble()};
#if BUBBLE
        stem.bubbleGameMode=(BubbleGameMode)mode.bubbleGameMode;
#else
        stem.targetType=(OddOrEven)mode.targetType;
        for(int i=0;i<6;i++) { var spawn=Rect(board,"Spawn"+i,0,0,1,1);spawn.anchorMin=spawn.anchorMax=spawn.pivot=new Vector2(.5f,.5f);spawn.anchoredPosition=new Vector2(-500+i*200,-280); }
#endif
#endif
        var hud=go.AddComponent<SessionHud>();hud.manager=stem;hud.level=level;
        hud.timer=Text(root,"Timer","",72,814,260,36,19,Muted);
        hud.mistakes=Text(root,"Mistakes","",365,814,260,36,19,Muted);
        var restart=Button(root,"Restart","Yeniden başla",1140,804,228,48,Color.white,Ink,20);UnityEventTools.AddPersistentListener(restart.onClick,hud.Restart);
        Save(mode.scene);
    }
    static GameObject Store(RectTransform rt,string name)
    {
        var result=PrefabUtility.SaveAsPrefabAsset(rt.gameObject,"Assets/Game/Generated/"+name+".prefab");UnityEngine.Object.DestroyImmediate(rt.gameObject);return result;
    }
    static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(8,8);r.offsetMax=new Vector2(-8,-8); }
#if SEQUENCE
    static GameObject NumberTile()
    {
        var r=Panel(null,"NumberTile",0,0,120,112,Ink);var l=r.gameObject.AddComponent<LayoutElement>();l.preferredHeight=112;
        var t=Text(r,"Value","0",0,0,120,112,36,Color.white,true,TextAlignmentOptions.Center);Stretch(t.rectTransform);
        return Store(r,"NumberTile");
    }
    static GameObject InputTile()
    {
        var r=Panel(null,"InputTile",0,0,120,112,Color.white);r.GetComponent<Image>().raycastTarget=true;var l=r.gameObject.AddComponent<LayoutElement>();l.preferredHeight=112;
        var input=r.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=r.GetComponent<Image>();input.contentType=TMP_InputField.ContentType.Standard;input.characterLimit=8;
        var viewport=Rect(r,"Viewport",8,8,104,96);Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
        var t=Text(viewport,"Text","",0,0,104,96,36,Ink,true,TextAlignmentOptions.Center);Stretch(t.rectTransform);
        var placeholder=Text(viewport,"Placeholder","?",0,0,104,96,36,Muted,false,TextAlignmentOptions.Center);Stretch(placeholder.rectTransform);
        input.textViewport=viewport;input.textComponent=t;input.placeholder=placeholder;
        return Store(r,"InputTile");
    }
#endif
#if CARDS
    static GameObject Card()
    {
        var r=Panel(null,"Card",0,0,180,160,Accent);r.GetComponent<Image>().raycastTarget=true;r.gameObject.AddComponent<Button>().targetGraphic=r.GetComponent<Image>();
        // The game expects its first child to contain the revealed face.
        var face=Panel(r,"Face",0,0,180,160,Color.white);Stretch(face);var t=Text(face,"Label","",0,0,180,160,30,Ink,true,TextAlignmentOptions.Center);Stretch(t.rectTransform);
        t.textWrappingMode = TextWrappingModes.Normal; t.fontSizeMin = 18; return Store(r,"Card");
    }
#endif
#if BUBBLE || ODDEVEN
    static GameObject Bubble()
    {
        var r=Panel(null,"Bubble",0,0,106,106,Accent);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        string path="Assets/Game/Generated/Bubble.asset";
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(!sprite){var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);for(int y=0;y<128;y++)for(int x=0;x<128;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(64,64));texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(63-d)));}texture.Apply();sprite=Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,.5f),128);AssetDatabase.CreateAsset(sprite,path);AssetDatabase.AddObjectToAsset(texture,sprite);}
        var image=r.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=true;
        r.gameObject.AddComponent<Button>().targetGraphic=image;r.gameObject.AddComponent<MovableObject>();
        var t=Text(r,"Value","0",0,0,106,106,35,Color.white,true,TextAlignmentOptions.Center);Stretch(t.rectTransform);
#if BUBBLE
        string explosionName="Exploding";
#else
        string explosionName="BalloonExploding";
#endif
        var pop=Text(r,explosionName,"+",0,0,106,106,48,Accent,true,TextAlignmentOptions.Center);Stretch(pop.rectTransform);pop.gameObject.SetActive(false);
        return Store(r,"Bubble");
    }
#endif
        private static void CreateFont()
    {
        const string path = "Assets/Game/Generated/InterfaceFont.asset";
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font) return;
        font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"));
        font.name = "Interface Font";
        AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
    }

    private static RectTransform SceneCanvas()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10);
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = Background;
        camera.GetComponent<Camera>().orthographic = true;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var root = Canvas("Interface", 0);
        var bg = Panel(root, "Background", 0, 0, 1440, 900, Background);
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
        return root;
    }

    private static RectTransform Canvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        return go.GetComponent<RectTransform>();
    }


    private static GameObject Feedback(string name, string label, Color color, bool wrong, bool final)
    {
        var root = Canvas(name + "SFX", 100);
        var card = Panel(root, "Toast", 460, 750, 520, 52, color);
        Text(card, "Message", label, 8, 0, 504, 52, 23, Color.white, true, TextAlignmentOptions.Center);
        foreach (var graphic in root.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        var fx = root.gameObject.AddComponent<FeedbackEffect>(); fx.wrong = wrong; fx.final = final; fx.lifetime = final ? 1.2f : 0.7f;
        string audioPath = "Assets/Game/Generated/" + name + ".wav";
        WriteTone(audioPath, wrong ? 220 : final ? 660 : 520, final ? 0.9f : 0.22f);
        AssetDatabase.ImportAsset(audioPath);
        var audio = root.gameObject.AddComponent<AudioSource>();
        audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath); audio.volume = 0.12f; audio.playOnAwake = true;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Game/Generated/" + name + "SFX.prefab");
        UnityEngine.Object.DestroyImmediate(root.gameObject);
        return prefab;
    }

    private static void WriteTone(string path, float frequency, float duration)
    {
        const int rate = 22050;
        int count = (int)(duration * rate);
        using var w = new BinaryWriter(File.Create(path));
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + count * 2); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(count * 2);
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float envelope = Mathf.Min(1, t * 40) * Mathf.Pow(1 - (float)i / count, 2);
            w.Write((short)(Mathf.Sin(t * frequency * Mathf.PI * 2) * envelope * 12000));
        }
    }

    private static void Save(string name)
    {
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Game/Scenes/" + name + ".unity");
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var rt = Rect(parent, name, x, y, w, h); var image = rt.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return rt;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color, bool bold = false, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var rt = Rect(parent, name, x, y, w, h); var text = rt.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.color = color; text.text = value;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; text.alignment = alignment; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true; text.fontSizeMin = size * 0.7f; text.fontSizeMax = size;
        return text;
    }

    private static Button Button(Transform parent, string name, string label, float x, float y, float w, float h, Color fill, Color color, float size)
    {
        var rt = Panel(parent, name, x, y, w, h, fill); var image = rt.GetComponent<Image>(); image.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        Text(rt, "Label", label, 8, 0, w - 16, h, size, color, true, TextAlignmentOptions.Center); return button;
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color); return color;
    }

}


